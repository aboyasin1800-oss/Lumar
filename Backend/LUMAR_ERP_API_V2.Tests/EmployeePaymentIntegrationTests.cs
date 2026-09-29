using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeePaymentIntegrationTests
{
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
    private static async Task<decimal> ScalarAsync(SqlConnection c,SqlTransaction t,string sql,long id){await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@id",id);return Convert.ToDecimal(await cmd.ExecuteScalarAsync());}
    private static string ConnectionString(){var b=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString")??"Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");if(!string.Equals(b.InitialCatalog,"LUMAR_ERP_TEST",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("ES-4 tests require LUMAR_ERP_TEST.");return b.ConnectionString;}
}