using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class OrderCancellationFinancialMovementIntegrationTests
{
    [Fact]
    public async Task Cancellation_UsesNetRevenue_SplitsRefundsByPaymentKind_AndIsIdempotent()
    {
        var fixture = await CreateFixtureAsync();
        try
        {
            await fixture.Repository.CancelOrderAsync(fixture.OrderId, "اختبار عقد العكس", "Test", CancellationToken.None);
            await fixture.Repository.CancelOrderAsync(fixture.OrderId, "اختبار عقد العكس", "Test", CancellationToken.None);

            Assert.Equal(40m, await ScalarDecimalAsync("SELECT Amount FROM dbo.FinancialTransactions WHERE ReferenceNumber=@advanceReference AND TransactionType=N'CustomerAdvanceRefund'", fixture.AdvanceReference));
            Assert.Equal(60m, await ScalarDecimalAsync("SELECT Amount FROM dbo.FinancialTransactions WHERE ReferenceNumber=@paymentReference AND TransactionType=N'CustomerPaymentRefund'", fixture.PaymentReference));
            Assert.Equal(130m, await ScalarDecimalAsync("SELECT Amount FROM dbo.FinancialTransactions WHERE ReferenceNumber=@reversalReference AND TransactionType=N'RevenueReversal'", fixture.ReversalReference));
            Assert.Equal(3, await ScalarIntByReferencesAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber IN (@advanceReference,@paymentReference,@reversalReference)", fixture.AdvanceReference, fixture.PaymentReference, fixture.ReversalReference));
            Assert.Equal(1, await ScalarIntByOrderAsync("SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@orderId AND PaymentKind=N'Refund' AND ReferenceNo=@referenceNumber", fixture.OrderId, fixture.AdvanceReference));
            Assert.Equal(1, await ScalarIntByOrderAsync("SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@orderId AND PaymentKind=N'Refund' AND ReferenceNo=@referenceNumber", fixture.OrderId, fixture.PaymentReference));
            Assert.Equal(40m, await ScalarDecimalAsync("SELECT SUM(jel.DebitAmount) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@advanceReference AND la.AccountCode=N'1160'", fixture.AdvanceReference));
            Assert.Equal(60m, await ScalarDecimalAsync("SELECT SUM(jel.DebitAmount) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@paymentReference AND la.AccountCode=N'1200'", fixture.PaymentReference));
            Assert.Equal(130m, await ScalarDecimalAsync("SELECT SUM(jel.DebitAmount) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE je.ReferenceNumber=@reversalReference AND la.AccountCode=N'4200'", fixture.ReversalReference));
        }
        finally
        {
            await DeleteFixtureAsync(fixture);
        }
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connectionString = GetConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var customerId = Convert.ToInt32(await new SqlCommand("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID", connection).ExecuteScalarAsync());
        var orderNumber = $"CXL-{Guid.NewGuid():N}";
        var createdAt = DateTime.UtcNow;
        await using var order = new SqlCommand("INSERT INTO dbo.Orders (OrderNumber,CustomerID,OrderDate,TotalAmount,DiscountAmount,PaidAmount,RemainingAmount,UrgencyStatus,OrderStatus,CreatedDate,SaleCategory,RevenueRecognized,RevenueRecognizedAt,RevenueReversalCreated) OUTPUT INSERTED.OrderID VALUES (@number,@customerId,@date,150,20,100,30,N'Normal',N'Delivered',@date,N'TailoringOrder',1,@date,0)", connection);
        order.Parameters.AddWithValue("@number", orderNumber);
        order.Parameters.AddWithValue("@customerId", customerId);
        order.Parameters.AddWithValue("@date", createdAt);
        var orderId = Convert.ToInt32(await order.ExecuteScalarAsync());
        var advanceReference = $"{orderNumber}:CustomerAdvanceRefund";
        var paymentReference = $"{orderNumber}:CustomerPaymentRefund";
        var reversalReference = $"{orderNumber}:RevenueReversal";
        await using var payment = new SqlCommand("INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,CreatedDate,PaymentKind) VALUES (@orderId,@date,40,N'Cash',@advance,@date,N'Advance'),(@orderId,@date,60,N'Cash',@payment,@date,N'DebtCollection')", connection);
        payment.Parameters.AddWithValue("@orderId", orderId);
        payment.Parameters.AddWithValue("@date", createdAt);
        payment.Parameters.AddWithValue("@advance", $"{orderNumber}:advance-original");
        payment.Parameters.AddWithValue("@payment", $"{orderNumber}:payment-original");
        await payment.ExecuteNonQueryAsync();
        return new Fixture(repository, orderId, advanceReference, paymentReference, reversalReference);
    }

    private static async Task DeleteFixtureAsync(Fixture fixture)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.ReferenceNumber LIKE @orderPrefix;
DELETE FROM dbo.JournalEntries WHERE ReferenceNumber LIKE @orderPrefix;
DELETE FROM dbo.FinancialTransactions WHERE ReferenceNumber LIKE @orderPrefix;
DELETE FROM dbo.CustomerLedgerEntries WHERE ReferenceNumber LIKE @orderPrefix;
DELETE FROM dbo.Payments WHERE OrderID=@orderId;
DELETE FROM dbo.Orders WHERE OrderID=@orderId;", connection);
        command.Parameters.AddWithValue("@orderPrefix", fixture.AdvanceReference[..fixture.AdvanceReference.IndexOf(':')]+":%");
        command.Parameters.AddWithValue("@orderId", fixture.OrderId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<decimal> ScalarDecimalAsync(string sql, string referenceNumber)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@advanceReference", referenceNumber);
        command.Parameters.AddWithValue("@paymentReference", referenceNumber);
        command.Parameters.AddWithValue("@reversalReference", referenceNumber);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ScalarIntByOrderAsync(string sql, int orderId, string referenceNumber)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ScalarIntByReferencesAsync(string sql, string advanceReference, string paymentReference, string reversalReference)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@advanceReference", advanceReference);
        command.Parameters.AddWithValue("@paymentReference", paymentReference);
        command.Parameters.AddWithValue("@reversalReference", reversalReference);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    private sealed record Fixture(OrderRepository Repository, int OrderId, string AdvanceReference, string PaymentReference, string ReversalReference);
}
