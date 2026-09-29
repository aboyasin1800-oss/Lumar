using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeePaymentIntegrationTests
{
    [Fact]
    public async Task Payments_ConcurrentTransactions_AllowOnlyOnePaymentAgainstOneBalance()
    {
        var fixture = await CreateConcurrentFixtureAsync();
        try
        {
            var firstPosted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = AttemptPaymentAsync(fixture, Guid.NewGuid(), "CON-1", firstPosted, releaseFirst.Task, null);
            await firstPosted.Task;
            var second = AttemptPaymentAsync(fixture, Guid.NewGuid(), "CON-2", null, Task.CompletedTask, secondStarted);
            await secondStarted.Task;
            releaseFirst.SetResult();
            var results = await Task.WhenAll(first, second);
            Assert.Equal(1, results.Count(result => result));

            await using var verify = new SqlConnection(ConnectionString()); await verify.OpenAsync();
            await using var balance = new SqlCommand("SELECT COALESCE(SUM(BalanceEffect),0) FROM dbo.EmployeeLedgerEntries WHERE EmployeeId=@employeeId AND Status=N'Posted'", verify);
            balance.Parameters.AddWithValue("@employeeId", fixture.EmployeeId);
            Assert.Equal(0m, Convert.ToDecimal(await balance.ExecuteScalarAsync()));
            await using var payments = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeePayments WHERE EmployeeId=@employeeId", verify);
            payments.Parameters.AddWithValue("@employeeId", fixture.EmployeeId);
            Assert.Equal(1, Convert.ToInt32(await payments.ExecuteScalarAsync()));
        }
        finally { await CleanupConcurrentFixtureAsync(fixture); }
    }

    [Fact]
    public async Task Payments_RejectZeroNegativeAndFutureOnlyBalances()
    {
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var (basic, _, inactive) = await SeedAsync(connection, transaction); var cash = await ConfigureAsync(connection, transaction);
            var service = new EmployeePaymentService(); var day = new DateOnly(2026, 9, 15);
            var zero = new EmployeePaymentRequest(basic, 1m, day, day, cash, "Cash", "ZERO", Guid.NewGuid(), "es4b-test");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayAsync(connection, transaction, zero, CancellationToken.None));
            await new EmployeeLedgerWriter().PostAsync(connection, transaction, inactive, new LedgerEntryRequest("Advance", 5m, -5m, DateTime.UtcNow, day, "test", inactive, Guid.NewGuid(), "NEG", "es4b-test"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayAsync(connection, transaction, zero with { EmployeeId = inactive, SourceOperationId = Guid.NewGuid(), ReferenceNumber = "NEG" }, CancellationToken.None));
            await AccrueAsync(new EmployeeLedgerWriter(), connection, transaction, basic, 20m, day.AddDays(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PayAsync(connection, transaction, zero with { SourceOperationId = Guid.NewGuid(), ReferenceNumber = "FUT" }, CancellationToken.None));
            Assert.Equal(0, await CountByOperationAsync(connection, transaction, "dbo.EmployeePayments", zero.SourceOperationId));
            await transaction.RollbackAsync();
        }
        catch { if (transaction.Connection is not null) await transaction.RollbackAsync(); throw; }
    }

    [Fact]
    public async Task Payments_RollbackLeavesNoPartialRecordsFromIndependentConnection()
    {
        var operation = Guid.NewGuid(); int employeeId;
        await using (var connection = new SqlConnection(ConnectionString()))
        {
            await connection.OpenAsync(); await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
            var ids = await SeedAsync(connection, transaction); employeeId = ids.Item1;
            var cash = await ConfigureAsync(connection, transaction); var day = new DateOnly(2026, 9, 15);
            await AccrueAsync(new EmployeeLedgerWriter(), connection, transaction, employeeId, 10m, day);
            await new EmployeePaymentService().PayAsync(connection, transaction, new EmployeePaymentRequest(employeeId, 10m, day, day, cash, "Cash", "ROLLBACK", operation, "es4b-test"), CancellationToken.None);
            await transaction.RollbackAsync();
        }
        await using var verifyConnection = new SqlConnection(ConnectionString()); await verifyConnection.OpenAsync();
        foreach (var table in new[] { "dbo.EmployeePayments", "dbo.EmployeeLedgerEntries", "dbo.AccountingEvents", "dbo.CashMovements" })
        {
            await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE SourceOperationId=@operation", verifyConnection);
            command.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation;
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task Payments_ValidateBalance_PostAtomically_SettleAndReverse()
    {
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var (basic,piece,inactive) = await SeedAsync(connection, transaction);
            var cash = await ConfigureAsync(connection, transaction);
            var ledger = new EmployeeLedgerWriter(); var day = new DateOnly(2026,9,15);
            await AccrueAsync(ledger,connection,transaction,basic,100m,day); await AccrueAsync(ledger,connection,transaction,piece,50m,day);
            await ledger.PostAsync(connection,transaction,inactive,new LedgerEntryRequest("Advance",10m,-10m,DateTime.UtcNow,day,"test",1,Guid.NewGuid(),"NEG","es4-test"),CancellationToken.None);
            var service = new EmployeePaymentService();
            var first = new EmployeePaymentRequest(basic,40m,day,day,cash,"Cash","PAY-1",Guid.NewGuid(),"es4-test");
            var partial = await service.PayAsync(connection,transaction,first,CancellationToken.None);
            Assert.Equal(100m,partial.BalanceBefore); Assert.Equal(60m,partial.BalanceAfter);
            Assert.True((await service.PayAsync(connection,transaction,first,CancellationToken.None)).IsExisting);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PayAsync(connection,transaction,first with { Amount=41m },CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PayAsync(connection,transaction,first with { SourceOperationId=Guid.NewGuid(),Amount=61m },CancellationToken.None));
            var full=await service.PayAsync(connection,transaction,new EmployeePaymentRequest(basic,60m,day,day,cash,"Cash","PAY-2",Guid.NewGuid(),"es4-test"),CancellationToken.None);
            Assert.Equal(0m,full.BalanceAfter); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PayAsync(connection,transaction,first with { SourceOperationId=Guid.NewGuid(),Amount=1m },CancellationToken.None));
            var piecePayment=await service.PayAsync(connection,transaction,new EmployeePaymentRequest(piece,50m,day,day,cash,"Cash","PAY-P",Guid.NewGuid(),"es4-test"),CancellationToken.None); Assert.Equal(0m,piecePayment.BalanceAfter);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PayAsync(connection,transaction,new EmployeePaymentRequest(inactive,1m,day,day,cash,"Cash","PAY-N",Guid.NewGuid(),"es4-test"),CancellationToken.None));
            var reversal=await service.ReverseAsync(connection,transaction,partial.EmployeePaymentId,Guid.NewGuid(),"PAY-1-R","test reversal","es4-test",CancellationToken.None);
            Assert.Equal(40m,reversal.BalanceAfter); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReverseAsync(connection,transaction,partial.EmployeePaymentId,Guid.NewGuid(),"PAY-1-R2","duplicate","es4-test",CancellationToken.None));
            foreach(var eventId in new[]{partial.AccountingEventId,full.AccountingEventId,piecePayment.AccountingEventId,reversal.AccountingEventId}) { Assert.Equal(1,await CountAsync(connection,transaction,"dbo.FinancialTransactions",eventId)); Assert.Equal(1,await CountAsync(connection,transaction,"dbo.JournalEntries",eventId)); }
            Assert.Equal(1,await CountAsync(connection,transaction,"dbo.CashMovements",partial.AccountingEventId)); Assert.Equal(1,await CountAsync(connection,transaction,"dbo.CashMovements",reversal.AccountingEventId));
            Assert.Equal(0m,await ScalarAsync(connection,transaction,"SELECT SUM(DebitAmount-CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@id)",partial.AccountingEventId));
            await transaction.RollbackAsync();
        } catch { if(transaction.Connection is not null) await transaction.RollbackAsync(); throw; }
    }
    private static async Task AccrueAsync(EmployeeLedgerWriter w,SqlConnection c,SqlTransaction t,int employee,decimal amount,DateOnly day)=>await w.PostAsync(c,t,employee,new LedgerEntryRequest("SalaryAccrual",amount,amount,DateTime.UtcNow,day,"test",employee,Guid.NewGuid(),"ACC-"+employee,"es4-test"),CancellationToken.None);
    private static async Task<(int,int,int)> SeedAsync(SqlConnection c,SqlTransaction t){const string sql="""DECLARE @d int=(SELECT TOP 1 DepartmentId FROM dbo.Departments);IF @d IS NULL BEGIN INSERT dbo.Departments(DepartmentCode,DepartmentName,IsActive,CreatedAt) VALUES(N'ES4',N'اختبار',1,SYSUTCDATETIME());SET @d=SCOPE_IDENTITY();END;DECLARE @x table(Id int);INSERT dbo.Employees(EmployeeCode,EmployeeName,FullName,DepartmentId,BasicSalary,SalaryType,HireDate,Status,IsActive,CreatedAt) OUTPUT inserted.EmployeeID INTO @x VALUES(N'ES4B',N'ب',N'ب',@d,100,N'BasicSalary','2026-01-01',N'Active',1,SYSUTCDATETIME()),(N'ES4P',N'ق',N'ق',@d,0,N'PieceWage','2026-01-01',N'Active',1,SYSUTCDATETIME()),(N'ES4I',N'خ',N'خ',@d,100,N'BasicSalary','2026-01-01',N'Inactive',0,SYSUTCDATETIME());SELECT Id FROM @x ORDER BY Id;""";await using var cmd=new SqlCommand(sql,c,t);var ids=new List<int>();await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())ids.Add(r.GetInt32(0));return(ids[0],ids[1],ids[2]);}
    private static async Task<int> ConfigureAsync(SqlConnection c,SqlTransaction t){const string sql="""DECLARE @x table(Id int);INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) OUTPUT inserted.LedgerAccountId INTO @x VALUES(N'ES4C',N'نقد',N'Asset',1,SYSUTCDATETIME()),(N'ES4L',N'التزام',N'Liability',1,SYSUTCDATETIME());DECLARE @cash int=(SELECT MIN(Id) FROM @x),@liability int=(SELECT MAX(Id) FROM @x);UPDATE dbo.AccountRoleMappings SET LedgerAccountId=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'EmployeeLiability' THEN @liability END,IsEnabled=1 WHERE AccountRole IN(N'Cash',N'EmployeeLiability');UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(26,33);INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements) OUTPUT inserted.CashAccountId VALUES(N'ES4',0,1,SYSUTCDATETIME(),1,N'YER',@cash,1,1);""";await using var cmd=new SqlCommand(sql,c,t);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task<int> CountAsync(SqlConnection c,SqlTransaction t,string table,long id){await using var cmd=new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE AccountingEventId=@id",c,t);cmd.Parameters.AddWithValue("@id",id);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task<int> CountByOperationAsync(SqlConnection c,SqlTransaction t,string table,Guid operation){await using var cmd=new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE SourceOperationId=@operation",c,t);cmd.Parameters.Add("@operation",SqlDbType.UniqueIdentifier).Value=operation;return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private sealed record ConcurrentFixture(string Suffix,int EmployeeId,int CashAccountId,int? PreviousCashMapping,bool PreviousCashEnabled,int? PreviousLiabilityMapping,bool PreviousLiabilityEnabled,bool PreviousPaymentEnabled,bool PreviousReversalEnabled);
    private static async Task<bool> AttemptPaymentAsync(ConcurrentFixture fixture,Guid operation,string reference,TaskCompletionSource? posted,Task release,TaskCompletionSource? started){await using var c=new SqlConnection(ConnectionString());await c.OpenAsync();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);try{started?.SetResult();await new EmployeePaymentService().PayAsync(c,t,new EmployeePaymentRequest(fixture.EmployeeId,100m,new DateOnly(2026,9,15),new DateOnly(2026,9,15),fixture.CashAccountId,"Cash",reference,operation,"es4b-concurrent"),CancellationToken.None);posted?.SetResult();await release;await t.CommitAsync();return true;}catch{if(t.Connection is not null)await t.RollbackAsync();return false;}}
    private static async Task<ConcurrentFixture> CreateConcurrentFixtureAsync(){var suffix=Guid.NewGuid().ToString("N")[..10];await using var c=new SqlConnection(ConnectionString());await c.OpenAsync();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);try{var state=await ReadMappingStateAsync(c,t);var employee=await InsertConcurrentEmployeeAsync(c,t,suffix);var ids=await InsertConcurrentAccountsAsync(c,t,suffix);await using(var map=new SqlCommand("UPDATE dbo.AccountRoleMappings SET LedgerAccountId=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'EmployeeLiability' THEN @liability END,IsEnabled=1 WHERE AccountRole IN(N'Cash',N'EmployeeLiability'); UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(26,33);",c,t)){map.Parameters.AddWithValue("@cash",ids.cash);map.Parameters.AddWithValue("@liability",ids.liability);await map.ExecuteNonQueryAsync();}var cash=await InsertConcurrentCashAsync(c,t,ids.cash,suffix);await AccrueAsync(new EmployeeLedgerWriter(),c,t,employee,100m,new DateOnly(2026,9,15));await t.CommitAsync();return new(suffix,employee,cash,state.cashId,state.cashEnabled,state.liabilityId,state.liabilityEnabled,state.paymentEnabled,state.reversalEnabled);}catch{if(t.Connection is not null)await t.RollbackAsync();throw;}}
    private static async Task<(int? cashId,bool cashEnabled,int? liabilityId,bool liabilityEnabled,bool paymentEnabled,bool reversalEnabled)> ReadMappingStateAsync(SqlConnection c,SqlTransaction t){await using var cmd=new SqlCommand("SELECT AccountRole,LedgerAccountId,IsEnabled FROM dbo.AccountRoleMappings WHERE AccountRole IN(N'Cash',N'EmployeeLiability'); SELECT AccountingEventType,IsEnabled FROM dbo.AccountingEventDefinitions WHERE AccountingEventType IN(26,33);",c,t);await using var r=await cmd.ExecuteReaderAsync();int? cash=null,liability=null;var cashEnabled=false;var liabilityEnabled=false;while(await r.ReadAsync()){if(r.GetString(0)=="Cash"){cash=r.IsDBNull(1)?null:r.GetInt32(1);cashEnabled=r.GetBoolean(2);}else{liability=r.IsDBNull(1)?null:r.GetInt32(1);liabilityEnabled=r.GetBoolean(2);}}await r.NextResultAsync();var payment=false;var reversal=false;while(await r.ReadAsync()){if(r.GetByte(0)==26)payment=r.GetBoolean(1);else reversal=r.GetBoolean(1);}return(cash,cashEnabled,liability,liabilityEnabled,payment,reversal);}
    private static async Task<int> InsertConcurrentEmployeeAsync(SqlConnection c,SqlTransaction t,string suffix){const string sql="DECLARE @d int=(SELECT TOP 1 DepartmentId FROM dbo.Departments); INSERT dbo.Employees(EmployeeCode,EmployeeName,FullName,DepartmentId,BasicSalary,SalaryType,HireDate,Status,IsActive,CreatedAt) OUTPUT inserted.EmployeeID VALUES(@code,N'اختبار',N'اختبار',@d,100,N'BasicSalary','2026-01-01',N'Active',1,SYSUTCDATETIME());";await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@code","C"+suffix);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task<(int cash,int liability)> InsertConcurrentAccountsAsync(SqlConnection c,SqlTransaction t,string suffix){const string sql="INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES(@cash,N'نقد',N'Asset',1,SYSUTCDATETIME()),(@liability,N'التزام',N'Liability',1,SYSUTCDATETIME()); SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode IN(@cash,@liability) ORDER BY AccountCode;";await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@cash","C4"+suffix);cmd.Parameters.AddWithValue("@liability","L4"+suffix);var ids=new List<int>();await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())ids.Add(r.GetInt32(0));return(ids[0],ids[1]);}
    private static async Task<int> InsertConcurrentCashAsync(SqlConnection c,SqlTransaction t,int ledger,string suffix){await using var cmd=new SqlCommand("INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements) OUTPUT inserted.CashAccountId VALUES(@name,0,1,SYSUTCDATETIME(),1,N'YER',@ledger,1,1)",c,t);cmd.Parameters.AddWithValue("@name","ES4C"+suffix);cmd.Parameters.AddWithValue("@ledger",ledger);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
    private static async Task CleanupConcurrentFixtureAsync(ConcurrentFixture f){await using var c=new SqlConnection(ConnectionString());await c.OpenAsync();await using var cmd=new SqlCommand("UPDATE dbo.EmployeePayments SET AccountingEventId=NULL WHERE EmployeeId=@employee; DELETE cm FROM dbo.CashMovements cm JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId JOIN dbo.EmployeeLedgerEntries le ON le.EmployeeLedgerEntryId=ae.SourceId WHERE ae.SourceType=N'EmployeeLedgerEntry' AND le.EmployeeId=@employee; DELETE jl FROM dbo.JournalEntryLines jl JOIN dbo.JournalEntries j ON j.JournalEntryId=jl.JournalEntryId JOIN dbo.EmployeeLedgerEntries le ON le.EmployeeLedgerEntryId=(SELECT SourceId FROM dbo.AccountingEvents WHERE AccountingEventId=j.AccountingEventId) WHERE le.EmployeeId=@employee; DELETE ft FROM dbo.FinancialTransactions ft JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId JOIN dbo.EmployeeLedgerEntries le ON le.EmployeeLedgerEntryId=ae.SourceId WHERE ae.SourceType=N'EmployeeLedgerEntry' AND le.EmployeeId=@employee; DELETE j FROM dbo.JournalEntries j JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=j.AccountingEventId JOIN dbo.EmployeeLedgerEntries le ON le.EmployeeLedgerEntryId=ae.SourceId WHERE ae.SourceType=N'EmployeeLedgerEntry' AND le.EmployeeId=@employee; DELETE ae FROM dbo.AccountingEvents ae JOIN dbo.EmployeeLedgerEntries le ON le.EmployeeLedgerEntryId=ae.SourceId WHERE ae.SourceType=N'EmployeeLedgerEntry' AND le.EmployeeId=@employee; DELETE FROM dbo.EmployeeLedgerEntries WHERE EmployeeId=@employee; DELETE FROM dbo.EmployeePayments WHERE EmployeeId=@employee; UPDATE dbo.AccountRoleMappings SET LedgerAccountId=CASE AccountRole WHEN N'Cash' THEN @cashMap ELSE @liabilityMap END,IsEnabled=CASE AccountRole WHEN N'Cash' THEN @cashEnabled ELSE @liabilityEnabled END WHERE AccountRole IN(N'Cash',N'EmployeeLiability'); UPDATE dbo.AccountingEventDefinitions SET IsEnabled=CASE AccountingEventType WHEN 26 THEN @payment ELSE @reversal END WHERE AccountingEventType IN(26,33); DELETE FROM dbo.CashAccounts WHERE CashAccountId=@cash; DELETE FROM dbo.LedgerAccounts WHERE AccountCode IN(@cashCode,@liabilityCode); DELETE FROM dbo.Employees WHERE EmployeeID=@employee;",c);cmd.Parameters.AddWithValue("@employee",f.EmployeeId);cmd.Parameters.AddWithValue("@cash",f.CashAccountId);cmd.Parameters.AddWithValue("@cashMap",f.PreviousCashMapping??(object)DBNull.Value);cmd.Parameters.AddWithValue("@liabilityMap",f.PreviousLiabilityMapping??(object)DBNull.Value);cmd.Parameters.AddWithValue("@cashEnabled",f.PreviousCashEnabled);cmd.Parameters.AddWithValue("@liabilityEnabled",f.PreviousLiabilityEnabled);cmd.Parameters.AddWithValue("@payment",f.PreviousPaymentEnabled);cmd.Parameters.AddWithValue("@reversal",f.PreviousReversalEnabled);cmd.Parameters.AddWithValue("@cashCode","C4"+f.Suffix);cmd.Parameters.AddWithValue("@liabilityCode","L4"+f.Suffix);await cmd.ExecuteNonQueryAsync();}
    private static async Task<decimal> ScalarAsync(SqlConnection c,SqlTransaction t,string sql,long id){await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@id",id);return Convert.ToDecimal(await cmd.ExecuteScalarAsync());}
    private static string ConnectionString(){var b=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString")??"Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");if(!string.Equals(b.InitialCatalog,"LUMAR_ERP_TEST",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("ES-4 tests require LUMAR_ERP_TEST.");return b.ConnectionString;}
}