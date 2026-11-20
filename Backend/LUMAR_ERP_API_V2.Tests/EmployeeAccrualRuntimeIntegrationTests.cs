using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeAccrualRuntimeIntegrationTests
{
    [Fact]
    public async Task EligibilityWriter_RollbackLeavesNoOfficialRecord()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await EnsureEs3SchemaAsync(connection);
        var operation = Guid.NewGuid();
        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var ids = await SeedAsync(connection, transaction);
            await new EmployeeDailyEligibilityWriter().ResolveAndWriteAsync(connection, transaction, ids.Basic, new DateOnly(2026, 9, 15), operation, "es3-test", CancellationToken.None);
            await transaction.RollbackAsync();
        }
        await using var verify = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeeDailyEligibility WHERE SourceOperationId=@operation", connection);
        verify.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation;
        Assert.Equal(0, Convert.ToInt32(await verify.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task EmployeeLedgerReader_UsesActiveTransactionForBalanceReads()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await EnsureEs3SchemaAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var ids = await SeedAsync(connection, transaction);
            var cashId = await ConfigureAccountingAsync(connection, transaction);
            var day = new DateOnly(2026, 9, 15);
            var runtime = new EmployeeAccrualRuntime();
            var operationId = Guid.NewGuid();

            var eventId = await runtime.PostDailyExpenseAsync(connection, transaction, ids.Basic, day, 5m, cashId, operationId, "ES3-EXP-TX", "es3-test", CancellationToken.None);
            var balance = await new EmployeeLedgerReader().GetBalanceAsync(connection, transaction, ids.Basic, CancellationToken.None);

            Assert.Equal(0m, balance.NetBalance);
            Assert.Equal(0m, balance.Accruals);
            Assert.Equal(0m, balance.Advances);
            Assert.Equal(0m, balance.DeductibleExpenses);
            Assert.Equal(0m, balance.Payments);
            Assert.Equal(1, await CountAsync(connection, transaction, "dbo.AccountingEvents", eventId));
            Assert.Equal(1, await CountAsync(connection, transaction, "dbo.CashMovements", eventId));
            await transaction.RollbackAsync();
        }
        catch
        {
            if (transaction.Connection is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task Es3_EligibilityAndEmployeeRuntimes_AreAtomicAndIdempotent()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await EnsureEs3SchemaAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var ids = await SeedAsync(connection, transaction);
            var resolver = new EmployeeDailyEligibilityResolver();
            var writer = new EmployeeDailyEligibilityWriter();
            var runtime = new EmployeeAccrualRuntime();
            var day = new DateOnly(2026, 9, 15);

            Assert.Equal("EligibleWorkedDay", (await resolver.ResolveAsync(connection, transaction, ids.Basic, day, CancellationToken.None)).Status);
            Assert.Equal("EligiblePaidLeave", (await resolver.ResolveAsync(connection, transaction, ids.Paid, day, CancellationToken.None)).Status);
            Assert.Equal("IneligibleUnpaidLeave", (await resolver.ResolveAsync(connection, transaction, ids.Unpaid, day, CancellationToken.None)).Status);
            Assert.Equal("IneligibleAbsence", (await resolver.ResolveAsync(connection, transaction, ids.Absent, day, CancellationToken.None)).Status);
            Assert.Equal("IneligibleBeforeStartDate", (await resolver.ResolveAsync(connection, transaction, ids.Future, day, CancellationToken.None)).Status);
            Assert.Equal("IneligibleInactiveEmployee", (await resolver.ResolveAsync(connection, transaction, ids.Inactive, day, CancellationToken.None)).Status);
            Assert.Equal("NotApplicablePieceWage", (await resolver.ResolveAsync(connection, transaction, ids.Piece, day, CancellationToken.None)).Status);

            var eligibility = await writer.ResolveAndWriteAsync(connection, transaction, ids.Basic, day, Guid.NewGuid(), "es3-test", CancellationToken.None);
            var retryEligibility = await writer.ResolveAndWriteAsync(connection, transaction, ids.Basic, day, Guid.NewGuid(), "es3-test", CancellationToken.None);
            Assert.True(retryEligibility.IsExisting);
            Assert.Equal(eligibility.Id, retryEligibility.Id);
            await using (var change = new SqlCommand("UPDATE dbo.EmployeeAttendances SET IsAbsent=1 WHERE EmployeeId=@id AND AttendanceDate=@day", connection, transaction))
            {
                change.Parameters.AddWithValue("@id", ids.Basic); change.Parameters.AddWithValue("@day", day.ToDateTime(TimeOnly.MinValue)); await change.ExecuteNonQueryAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => writer.ResolveAndWriteAsync(connection, transaction, ids.Basic, day, Guid.NewGuid(), "es3-test", CancellationToken.None));

            var cashId = await ConfigureAccountingAsync(connection, transaction);
            var salaryOperation = Guid.NewGuid();
            var salaryEvent = await runtime.PostDailySalaryAccrualAsync(connection, transaction, ids.Paid, day, 10m, salaryOperation, "ES3-SAL", "es3-test", CancellationToken.None);
            Assert.Equal(salaryEvent, await runtime.PostDailySalaryAccrualAsync(connection, transaction, ids.Paid, day, 10m, salaryOperation, "ES3-SAL", "es3-test", CancellationToken.None));
            var bonusEvent = await runtime.PostSeasonalBonusAsync(connection, transaction, ids.Basic, "Winter", 2026, 25m, Guid.NewGuid(), "ES3-BON", "es3-test", CancellationToken.None);
            var advanceEvent = await runtime.PostAdvanceAsync(connection, transaction, ids.Basic, day, 15m, cashId, Guid.NewGuid(), "ES3-ADV", "es3-test", CancellationToken.None);
            var salariedExpense = await runtime.PostDailyExpenseAsync(connection, transaction, ids.Basic, day, 5m, cashId, Guid.NewGuid(), "ES3-EXP-S", "es3-test", CancellationToken.None);
            var pieceExpense = await runtime.PostDailyExpenseAsync(connection, transaction, ids.Piece, day, 5m, cashId, Guid.NewGuid(), "ES3-EXP-P", "es3-test", CancellationToken.None);
            foreach (var eventId in new[] { salaryEvent, bonusEvent, advanceEvent, salariedExpense, pieceExpense })
            {
                Assert.Equal(1, await CountAsync(connection, transaction, "dbo.FinancialTransactions", eventId));
                Assert.Equal(1, await CountAsync(connection, transaction, "dbo.JournalEntries", eventId));
            }
            Assert.Equal(3, await CountAsync(connection, transaction, "dbo.CashMovements", advanceEvent) + await CountAsync(connection, transaction, "dbo.CashMovements", salariedExpense) + await CountAsync(connection, transaction, "dbo.CashMovements", pieceExpense));
            Assert.Equal(0m, await ScalarAsync(connection, transaction, "SELECT BalanceEffect FROM dbo.EmployeeLedgerEntries WHERE EntryType=N'SalariedEmployeeDailyExpense'"));
            var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, advanceEvent, Guid.NewGuid(), "ES3-ADV-R", "es3 reversal", "es3-test", CancellationToken.None);
            Assert.Equal(1, await CountAsync(connection, transaction, "dbo.CashMovements", reversal.AccountingEventId));
            await transaction.RollbackAsync();
        }
        catch { if (transaction.Connection is not null) await transaction.RollbackAsync(); throw; }
    }

    private static async Task EnsureEs3SchemaAsync(SqlConnection c)
    {
        await using var ensure = new SqlCommand(@"
            IF COL_LENGTH(N'dbo.LeaveRequests', N'PaymentClassification') IS NULL
            BEGIN
                ALTER TABLE dbo.LeaveRequests ADD PaymentClassification nvarchar(10) NULL;
            END;
            IF NOT EXISTS (
                SELECT 1
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.LeaveRequests')
                  AND name = N'CK_LeaveRequests_PaymentClassification')
            BEGIN
                EXEC(N'ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT CK_LeaveRequests_PaymentClassification CHECK(PaymentClassification IN(N''Paid'',N''Unpaid'') OR PaymentClassification IS NULL);');
            END;
            IF OBJECT_ID(N'dbo.EmployeeStatusHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeStatusHistory(
                    EmployeeStatusHistoryId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    EffectiveDate date NOT NULL,
                    Status nvarchar(20) NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeStatusHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeStatusHistory_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT UQ_EmployeeStatusHistory_Employee_Date UNIQUE(EmployeeId,EffectiveDate),
                    CONSTRAINT CK_EmployeeStatusHistory_Status CHECK(Status IN(N'Active',N'Inactive',N'Terminated')));
                CREATE INDEX IX_EmployeeStatusHistory_Employee_Date ON dbo.EmployeeStatusHistory(EmployeeId,EffectiveDate DESC);
            END;
            IF OBJECT_ID(N'dbo.EmployeeDailyEligibility', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeDailyEligibility(
                    EmployeeDailyEligibilityId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    EligibilityDate date NOT NULL,
                    EligibilityStatus nvarchar(40) NOT NULL,
                    LeaveRequestId int NULL,
                    EmployeeAttendanceId int NULL,
                    SourceOperationId uniqueidentifier NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeDailyEligibility_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeDailyEligibility_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT FK_EmployeeDailyEligibility_Leave FOREIGN KEY(LeaveRequestId) REFERENCES dbo.LeaveRequests(LeaveRequestId),
                    CONSTRAINT FK_EmployeeDailyEligibility_Attendance FOREIGN KEY(EmployeeAttendanceId) REFERENCES dbo.EmployeeAttendances(EmployeeAttendanceId),
                    CONSTRAINT UQ_EmployeeDailyEligibility_Employee_Date UNIQUE(EmployeeId,EligibilityDate),
                    CONSTRAINT UQ_EmployeeDailyEligibility_Operation UNIQUE(SourceOperationId),
                    CONSTRAINT CK_EmployeeDailyEligibility_Status CHECK(EligibilityStatus IN(N'EligibleWorkedDay',N'EligiblePaidLeave',N'IneligibleUnpaidLeave',N'IneligibleAbsence',N'IneligibleBeforeStartDate',N'IneligibleInactiveEmployee',N'NotApplicablePieceWage')));
            END;
            IF OBJECT_ID(N'dbo.EmployeeDailyAccruals', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeDailyAccruals(
                    EmployeeDailyAccrualId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    AccrualDate date NOT NULL,
                    EligibilityId bigint NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    SourceOperationId uniqueidentifier NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeDailyAccruals_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeDailyAccruals_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT FK_EmployeeDailyAccruals_Eligibility FOREIGN KEY(EligibilityId) REFERENCES dbo.EmployeeDailyEligibility(EmployeeDailyEligibilityId),
                    CONSTRAINT UQ_EmployeeDailyAccruals_EmployeeDate UNIQUE(EmployeeId,AccrualDate),
                    CONSTRAINT UQ_EmployeeDailyAccruals_Operation UNIQUE(SourceOperationId),
                    CONSTRAINT CK_EmployeeDailyAccruals_Amount CHECK(Amount>0));
            END;
            IF OBJECT_ID(N'dbo.EmployeeSeasonalBonuses', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeSeasonalBonuses(
                    EmployeeSeasonalBonusId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    Season nvarchar(50) NOT NULL,
                    SeasonYear int NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    SourceOperationId uniqueidentifier NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeSeasonalBonuses_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeSeasonalBonuses_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT UQ_EmployeeSeasonalBonuses_EmployeeSeason UNIQUE(EmployeeId,Season,SeasonYear),
                    CONSTRAINT UQ_EmployeeSeasonalBonuses_Operation UNIQUE(SourceOperationId),
                    CONSTRAINT CK_EmployeeSeasonalBonuses_Amount CHECK(Amount>0));
            END;
            IF OBJECT_ID(N'dbo.EmployeeAdvances', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeAdvances(
                    EmployeeAdvanceId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    AdvanceDate date NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    CashAccountId int NOT NULL,
                    SourceOperationId uniqueidentifier NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeAdvances_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeAdvances_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT FK_EmployeeAdvances_Cash FOREIGN KEY(CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),
                    CONSTRAINT UQ_EmployeeAdvances_Operation UNIQUE(SourceOperationId),
                    CONSTRAINT CK_EmployeeAdvances_Amount CHECK(Amount>0));
            END;
            IF OBJECT_ID(N'dbo.EmployeeDailyExpenses', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmployeeDailyExpenses(
                    EmployeeDailyExpenseId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    ExpenseDate date NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    CashAccountId int NOT NULL,
                    SourceOperationId uniqueidentifier NOT NULL,
                    CreatedBy nvarchar(100) NOT NULL,
                    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EmployeeDailyExpenses_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_EmployeeDailyExpenses_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),
                    CONSTRAINT FK_EmployeeDailyExpenses_Cash FOREIGN KEY(CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),
                    CONSTRAINT UQ_EmployeeDailyExpenses_Operation UNIQUE(SourceOperationId),
                    CONSTRAINT CK_EmployeeDailyExpenses_Amount CHECK(Amount>0));
            END;
        ", c);
        await ensure.ExecuteNonQueryAsync();
    }

    private static async Task<(int Basic,int Paid,int Unpaid,int Absent,int Future,int Inactive,int Piece)> SeedAsync(SqlConnection c, SqlTransaction t)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        const string sql = """
            DECLARE @d int=(SELECT TOP 1 DepartmentId FROM dbo.Departments); IF @d IS NULL BEGIN INSERT dbo.Departments(DepartmentCode,DepartmentName,IsActive,CreatedAt) VALUES(N'ES3',N'اختبار',1,SYSUTCDATETIME()); SET @d=SCOPE_IDENTITY(); END;
            DECLARE @x table(Id int); INSERT dbo.Employees(EmployeeCode,EmployeeName,FullName,DepartmentId,BasicSalary,SalaryType,HireDate,Status,IsActive,CreatedAt,PieceWageRate,OvertimeHourlyRate) OUTPUT inserted.EmployeeID INTO @x VALUES
            (@codeBasic,@nameBasic,@fullBasic,@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME(),0,25),(@codePaid,@namePaid,@fullPaid,@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME(),0,25),(@codeUnpaid,@nameUnpaid,@fullUnpaid,@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME(),0,25),(@codeAbsent,@nameAbsent,@fullAbsent,@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME(),0,25),(@codeFuture,@nameFuture,@fullFuture,@d,300,N'BasicSalary','2026-09-20',N'Active',1,SYSUTCDATETIME(),0,25),(@codeInactive,@nameInactive,@fullInactive,@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME(),0,25),(@codePiece,@namePiece,@fullPiece,@d,0,N'PieceWage','2026-09-01',N'Active',1,SYSUTCDATETIME(),100,0);
            SELECT Id FROM @x ORDER BY Id;
            """;
        await using var cmd = new SqlCommand(sql,c,t);
        cmd.Parameters.AddWithValue("@codeBasic", $"ES3B{suffix}");
        cmd.Parameters.AddWithValue("@nameBasic", "ب");
        cmd.Parameters.AddWithValue("@fullBasic", "ب");
        cmd.Parameters.AddWithValue("@codePaid", $"ES3P{suffix}");
        cmd.Parameters.AddWithValue("@namePaid", "م");
        cmd.Parameters.AddWithValue("@fullPaid", "م");
        cmd.Parameters.AddWithValue("@codeUnpaid", $"ES3U{suffix}");
        cmd.Parameters.AddWithValue("@nameUnpaid", "غ");
        cmd.Parameters.AddWithValue("@fullUnpaid", "غ");
        cmd.Parameters.AddWithValue("@codeAbsent", $"ES3A{suffix}");
        cmd.Parameters.AddWithValue("@nameAbsent", "ع");
        cmd.Parameters.AddWithValue("@fullAbsent", "ع");
        cmd.Parameters.AddWithValue("@codeFuture", $"ES3F{suffix}");
        cmd.Parameters.AddWithValue("@nameFuture", "ق");
        cmd.Parameters.AddWithValue("@fullFuture", "ق");
        cmd.Parameters.AddWithValue("@codeInactive", $"ES3I{suffix}");
        cmd.Parameters.AddWithValue("@nameInactive", "خ");
        cmd.Parameters.AddWithValue("@fullInactive", "خ");
        cmd.Parameters.AddWithValue("@codePiece", $"ES3W{suffix}");
        cmd.Parameters.AddWithValue("@namePiece", "ط");
        cmd.Parameters.AddWithValue("@fullPiece", "ط");
        var ids=new List<int>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) ids.Add(r.GetInt32(0));
        await r.CloseAsync();
        await using var data = new SqlCommand("INSERT dbo.LeaveRequests(EmployeeId,LeaveType,StartDate,EndDate,RequestedDays,Status,ApprovedBy,ApprovedAt,CreatedAt,PaymentClassification) VALUES(@paid,N'اختبار','2026-09-15','2026-09-15',1,N'Approved',N'es3-test',SYSUTCDATETIME(),SYSUTCDATETIME(),N'Paid'),(@unpaid,N'اختبار','2026-09-15','2026-09-15',1,N'Approved',N'es3-test',SYSUTCDATETIME(),SYSUTCDATETIME(),N'Unpaid'); INSERT dbo.EmployeeAttendances(EmployeeId,AttendanceDate,WorkedHours,OvertimeHours,IsAbsent,CreatedAt) VALUES(@basic,'2026-09-15',8,0,0,SYSUTCDATETIME()),(@absent,'2026-09-15',0,0,1,SYSUTCDATETIME()); INSERT dbo.EmployeeStatusHistory(EmployeeId,EffectiveDate,Status,CreatedBy) VALUES(@inactive,'2026-09-01',N'Inactive',N'es3-test');",c,t);
        data.Parameters.AddWithValue("@basic",ids[0]); data.Parameters.AddWithValue("@paid",ids[1]); data.Parameters.AddWithValue("@unpaid",ids[2]); data.Parameters.AddWithValue("@absent",ids[3]); data.Parameters.AddWithValue("@inactive",ids[5]); await data.ExecuteNonQueryAsync(); return (ids[0],ids[1],ids[2],ids[3],ids[4],ids[5],ids[6]);
    }

    private static async Task<int> ConfigureAccountingAsync(SqlConnection c, SqlTransaction t)
    {
        const string sql = """
            DECLARE @cash int = (SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'ES3-C');
            DECLARE @salary int = (SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'ES3-S');
            DECLARE @liability int = (SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'ES3-L');
            DECLARE @advance int = (SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'ES3-A');
            DECLARE @expense int = (SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'ES3-E');

            IF @cash IS NULL BEGIN INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(N'ES3-C',N'نقد',N'Asset',1,SYSUTCDATETIME()); SET @cash = SCOPE_IDENTITY(); END;
            IF @salary IS NULL BEGIN INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(N'ES3-S',N'راتب',N'Expense',1,SYSUTCDATETIME()); SET @salary = SCOPE_IDENTITY(); END;
            IF @liability IS NULL BEGIN INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(N'ES3-L',N'التزام',N'Liability',1,SYSUTCDATETIME()); SET @liability = SCOPE_IDENTITY(); END;
            IF @advance IS NULL BEGIN INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(N'ES3-A',N'سلفة',N'Asset',1,SYSUTCDATETIME()); SET @advance = SCOPE_IDENTITY(); END;
            IF @expense IS NULL BEGIN INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(N'ES3-E',N'مصروف',N'Expense',1,SYSUTCDATETIME()); SET @expense = SCOPE_IDENTITY(); END;

            UPDATE dbo.AccountRoleMappings SET LedgerAccountId=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'SalaryExpense' THEN @salary WHEN N'EmployeeLiability' THEN @liability WHEN N'EmployeeAdvance' THEN @advance WHEN N'EmployeeDailyExpense' THEN @expense END, IsEnabled=1 WHERE AccountRole IN(N'Cash',N'SalaryExpense',N'EmployeeLiability',N'EmployeeAdvance',N'EmployeeDailyExpense');
            UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(20,21,23,24,25,33);

            DECLARE @cashAccountId int = (SELECT CashAccountId FROM dbo.CashAccounts WHERE CashAccountType=1 AND CurrencyCode = N'YER' AND AccountName = N'ES3');
            IF @cashAccountId IS NULL BEGIN INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements) VALUES(N'ES3',0,1,SYSUTCDATETIME(),1,N'YER',@cash,1,1); SET @cashAccountId = SCOPE_IDENTITY(); END;
            SELECT @cashAccountId;
            """;
        await using var cmd = new SqlCommand(sql, c, t);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    private static async Task<int> CountAsync(SqlConnection c,SqlTransaction t,string table,long id){await using var cmd=new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE AccountingEventId=@id",c,t);cmd.Parameters.AddWithValue("@id",id);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task<decimal> ScalarAsync(SqlConnection c,SqlTransaction t,string sql){await using var cmd=new SqlCommand(sql,c,t);return Convert.ToDecimal(await cmd.ExecuteScalarAsync());}
    private static string GetConnectionString(){var b=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString")??"Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");if(!string.Equals(b.InitialCatalog,"LUMAR_ERP_TEST",StringComparison.OrdinalIgnoreCase)&&!string.Equals(b.InitialCatalog,"LUMAR_ERP_ES_VALIDATION",StringComparison.OrdinalIgnoreCase)&&!string.Equals(b.InitialCatalog,"LUMAR_ERP_ES_VALIDATION_R2",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("ES-3 tests require an approved ES validation database including LUMAR_ERP_ES_VALIDATION_R2.");return b.ConnectionString;}
}