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
    public async Task Es3_EligibilityAndEmployeeRuntimes_AreAtomicAndIdempotent()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
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

    private static async Task<(int Basic,int Paid,int Unpaid,int Absent,int Future,int Inactive,int Piece)> SeedAsync(SqlConnection c, SqlTransaction t)
    {
        const string sql = """
            DECLARE @d int=(SELECT TOP 1 DepartmentId FROM dbo.Departments); IF @d IS NULL BEGIN INSERT dbo.Departments(DepartmentCode,DepartmentName,IsActive,CreatedAt) VALUES(N'ES3',N'اختبار',1,SYSUTCDATETIME()); SET @d=SCOPE_IDENTITY(); END;
            DECLARE @x table(Id int); INSERT dbo.Employees(EmployeeCode,EmployeeName,FullName,DepartmentId,BasicSalary,SalaryType,HireDate,Status,IsActive,CreatedAt) OUTPUT inserted.EmployeeID INTO @x VALUES
            (N'ES3B',N'ب',N'ب',@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME()),(N'ES3P',N'م',N'م',@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME()),(N'ES3U',N'غ',N'غ',@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME()),(N'ES3A',N'ع',N'ع',@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME()),(N'ES3F',N'ق',N'ق',@d,300,N'BasicSalary','2026-09-20',N'Active',1,SYSUTCDATETIME()),(N'ES3I',N'خ',N'خ',@d,300,N'BasicSalary','2026-09-01',N'Active',1,SYSUTCDATETIME()),(N'ES3W',N'ط',N'ط',@d,0,N'PieceWage','2026-09-01',N'Active',1,SYSUTCDATETIME());
            SELECT Id FROM @x ORDER BY Id;
            """;
        await using var cmd = new SqlCommand(sql,c,t); var ids=new List<int>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) ids.Add(r.GetInt32(0));
        await r.CloseAsync();
        await using var data = new SqlCommand("INSERT dbo.LeaveRequests(EmployeeId,LeaveType,StartDate,EndDate,RequestedDays,Status,ApprovedBy,ApprovedAt,CreatedAt,PaymentClassification) VALUES(@paid,N'اختبار','2026-09-15','2026-09-15',1,N'Approved',N'es3-test',SYSUTCDATETIME(),SYSUTCDATETIME(),N'Paid'),(@unpaid,N'اختبار','2026-09-15','2026-09-15',1,N'Approved',N'es3-test',SYSUTCDATETIME(),SYSUTCDATETIME(),N'Unpaid'); INSERT dbo.EmployeeAttendances(EmployeeId,AttendanceDate,WorkedHours,OvertimeHours,IsAbsent,CreatedAt) VALUES(@basic,'2026-09-15',8,0,0,SYSUTCDATETIME()),(@absent,'2026-09-15',0,0,1,SYSUTCDATETIME()); INSERT dbo.EmployeeStatusHistory(EmployeeId,EffectiveDate,Status,CreatedBy) VALUES(@inactive,'2026-09-01',N'Inactive',N'es3-test');",c,t);
        data.Parameters.AddWithValue("@basic",ids[0]); data.Parameters.AddWithValue("@paid",ids[1]); data.Parameters.AddWithValue("@unpaid",ids[2]); data.Parameters.AddWithValue("@absent",ids[3]); data.Parameters.AddWithValue("@inactive",ids[5]); await data.ExecuteNonQueryAsync(); return (ids[0],ids[1],ids[2],ids[3],ids[4],ids[5],ids[6]);
    }

    private static async Task<int> ConfigureAccountingAsync(SqlConnection c, SqlTransaction t)
    {
        const string sql="""DECLARE @x table(Id int); INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) OUTPUT inserted.LedgerAccountId INTO @x VALUES(N'ES3-C',N'نقد',N'Asset',1,SYSUTCDATETIME()),(N'ES3-S',N'راتب',N'Expense',1,SYSUTCDATETIME()),(N'ES3-L',N'التزام',N'Liability',1,SYSUTCDATETIME()),(N'ES3-A',N'سلفة',N'Asset',1,SYSUTCDATETIME()),(N'ES3-E',N'مصروف',N'Expense',1,SYSUTCDATETIME()); DECLARE @cash int=(SELECT MIN(Id) FROM @x),@salary int=(SELECT Id FROM @x ORDER BY Id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY),@liability int=(SELECT Id FROM @x ORDER BY Id OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY),@advance int=(SELECT Id FROM @x ORDER BY Id OFFSET 3 ROWS FETCH NEXT 1 ROWS ONLY),@expense int=(SELECT MAX(Id) FROM @x); UPDATE dbo.AccountRoleMappings SET LedgerAccountId=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'SalaryExpense' THEN @salary WHEN N'EmployeeLiability' THEN @liability WHEN N'EmployeeAdvance' THEN @advance WHEN N'EmployeeDailyExpense' THEN @expense END,IsEnabled=1 WHERE AccountRole IN(N'Cash',N'SalaryExpense',N'EmployeeLiability',N'EmployeeAdvance',N'EmployeeDailyExpense'); UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(20,21,23,24,25,33); INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements) OUTPUT inserted.CashAccountId VALUES(N'ES3',0,1,SYSUTCDATETIME(),1,N'YER',@cash,1,1);""";
        await using var cmd=new SqlCommand(sql,c,t); return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    private static async Task<int> CountAsync(SqlConnection c,SqlTransaction t,string table,long id){await using var cmd=new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE AccountingEventId=@id",c,t);cmd.Parameters.AddWithValue("@id",id);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task<decimal> ScalarAsync(SqlConnection c,SqlTransaction t,string sql){await using var cmd=new SqlCommand(sql,c,t);return Convert.ToDecimal(await cmd.ExecuteScalarAsync());}
    private static string GetConnectionString(){var b=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString")??"Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");if(!string.Equals(b.InitialCatalog,"LUMAR_ERP_TEST",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("ES-3 tests require LUMAR_ERP_TEST.");return b.ConnectionString;}
}