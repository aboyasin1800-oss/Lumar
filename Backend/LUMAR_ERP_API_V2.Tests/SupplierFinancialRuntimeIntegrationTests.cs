using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierFinancialRuntimeIntegrationTests
{
    [Fact]
    public async Task FinancialRuntime_PostsSupplierInvoicePaymentsAllocationsAndReversalsAtomically()
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var supplierId = await InsertSupplierAsync(connection, transaction);
            var cashAccountId = await ConfigureFoundationAsync(connection, transaction);
            var runtime = new SupplierFinancialRuntime();
            var day = new DateOnly(2026, 9, 29);

            var advance = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(
                supplierId, null, 30m, day, cashAccountId, SupplierPaymentKind.Advance, "Cash", "SUP-ADV", null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(0m, advance.AmountAllocated);

            var invoice = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(
                supplierId, "SUP-INV-1", day, day, 100m, null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(30m, invoice.AmountPaid);
            Assert.Equal(70m, invoice.OutstandingAmount);

            var laterRequest = new SupplierFinancialPaymentRequest(
                supplierId, invoice.SupplierInvoiceId, 40m, day, cashAccountId, SupplierPaymentKind.Later, "Cash", "SUP-LATER", null, Guid.NewGuid(), "es5-test");
            var later = await runtime.PayAsync(connection, transaction, laterRequest, CancellationToken.None);
            Assert.Equal(40m, later.AmountAllocated);
            Assert.True((await runtime.PayAsync(connection, transaction, laterRequest, CancellationToken.None)).IsExisting);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PayAsync(connection, transaction, laterRequest with { Amount = 41m }, CancellationToken.None));

            var immediate = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(
                supplierId, "SUP-INV-2", day, day, 20m, null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            var immediatePayment = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(
                supplierId, immediate.SupplierInvoiceId, 20m, day, cashAccountId, SupplierPaymentKind.Immediate, "Cash", "SUP-IMM", null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(20m, immediatePayment.AmountAllocated);

            var laterAllocationId = await ScalarIntAsync(connection, transaction, "SELECT SupplierPaymentAllocationId FROM dbo.SupplierPaymentAllocations WHERE SupplierPaymentId=@paymentId", later.SupplierPaymentId);
            var allocationReversalOperation = Guid.NewGuid();
            var allocationReversal = await runtime.ReverseAllocationAsync(connection, transaction, laterAllocationId, allocationReversalOperation, "SUP-LATER-REV-ALLOC", "test", "es5-test", CancellationToken.None);
            Assert.Equal(allocationReversal, await runtime.ReverseAllocationAsync(connection, transaction, laterAllocationId, allocationReversalOperation, "SUP-LATER-REV-ALLOC", "test", "es5-test", CancellationToken.None));

            var paymentReversalOperation = Guid.NewGuid();
            var paymentReversal = await runtime.ReversePaymentAsync(connection, transaction, later.SupplierPaymentId, paymentReversalOperation, "SUP-LATER-REV", "test", "es5-test", CancellationToken.None);
            Assert.Equal(paymentReversal, await runtime.ReversePaymentAsync(connection, transaction, later.SupplierPaymentId, paymentReversalOperation, "SUP-LATER-REV", "test", "es5-test", CancellationToken.None));

            Assert.Equal(30m, await ScalarDecimalAsync(connection, transaction, "SELECT AmountPaid FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoiceId", invoice.SupplierInvoiceId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", advance.AccountingEventId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", immediatePayment.AccountingEventId));
            Assert.Equal(0m, await ScalarDecimalAsync(connection, transaction, "SELECT COALESCE(SUM(DebitAmount-CreditAmount),0) FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@eventId)", immediatePayment.AccountingEventId));
            Assert.True(await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=33", 0) >= 2);

            await transaction.RollbackAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<int> InsertSupplierAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("INSERT dbo.Suppliers(SupplierCode,SupplierName,IsActive,CreatedAt) OUTPUT inserted.SupplierId VALUES(@code,N'مورد اختبار ES5',1,SYSUTCDATETIME());", connection, transaction);
        command.Parameters.AddWithValue("@code", $"ES5-{Guid.NewGuid():N}"[..16]);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ConfigureFoundationAsync(SqlConnection connection, SqlTransaction transaction)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        const string sql = """
            DECLARE @accounts table (AccountCode nvarchar(50), LedgerAccountId int);
            INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt)
            OUTPUT inserted.AccountCode,inserted.LedgerAccountId INTO @accounts
            VALUES (@cash,N'نقد',N'Asset',1,SYSUTCDATETIME()),(@liability,N'دائنون',N'Liability',1,SYSUTCDATETIME()),(@clearing,N'مشتريات',N'Asset',1,SYSUTCDATETIME()),(@advance,N'سلف موردين',N'Asset',1,SYSUTCDATETIME());
            UPDATE dbo.AccountRoleMappings SET LedgerAccountId=(SELECT LedgerAccountId FROM @accounts WHERE AccountCode=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'SupplierLiability' THEN @liability WHEN N'PurchaseClearing' THEN @clearing WHEN N'SupplierAdvance' THEN @advance END),IsEnabled=1 WHERE AccountRole IN(N'Cash',N'SupplierLiability',N'PurchaseClearing',N'SupplierAdvance');
            UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(28,29,30,31,32,33);
            INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements)
            OUTPUT inserted.CashAccountId VALUES(@cashName,0,1,SYSUTCDATETIME(),1,N'YER',(SELECT LedgerAccountId FROM @accounts WHERE AccountCode=@cash),1,1);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@cash", $"ES5C{suffix}"); command.Parameters.AddWithValue("@liability", $"ES5L{suffix}");
        command.Parameters.AddWithValue("@clearing", $"ES5P{suffix}"); command.Parameters.AddWithValue("@advance", $"ES5A{suffix}"); command.Parameters.AddWithValue("@cashName", $"ES5 Cash {suffix}");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, SqlTransaction transaction, string sql, int paymentId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@paymentId", paymentId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(SqlConnection connection, SqlTransaction transaction, string sql, long eventId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(SqlConnection connection, SqlTransaction transaction, string sql, int invoiceId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@invoiceId", invoiceId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAsync(SqlConnection connection, SqlTransaction transaction, string sql, long eventId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string ConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString") ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ES-5 tests are restricted to LUMAR_ERP_TEST.");
        return builder.ConnectionString;
    }
}