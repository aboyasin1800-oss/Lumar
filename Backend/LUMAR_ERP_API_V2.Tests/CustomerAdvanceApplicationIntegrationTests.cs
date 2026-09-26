using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CustomerAdvanceApplicationIntegrationTests
{
    [Fact]
    public async Task FullDelivery_AppliesSingleAdvance_WithoutCashMovementOrLedgerWrite()
    {
        var fixture = await CreateFixtureAsync(100m, [40m]);
        try
        {
            await fixture.Repository.DeliverAsync(fixture.OrderId, CancellationToken.None);

            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId", fixture.OrderId));
            Assert.Equal(40m, await ScalarDecimalAsync("SELECT AppliedAmount FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId", fixture.OrderId));
            await AssertAppliedPostingAsync(fixture.OrderId, 40m, 1);
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.CashMovements cm INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId WHERE ae.AccountingEventType=6 AND ae.CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId)", fixture.OrderId));
            Assert.Equal(0, await CountCustomerLedgerEntriesAsync(fixture.CustomerId, fixture.CreatedAt));
        }
        finally { await DeleteFixtureAsync(fixture); }
    }

    [Fact]
    public async Task FullDelivery_AppliesMultipleAdvancesInPaymentOrder_LeavesExcessAndIsIdempotent()
    {
        var fixture = await CreateFixtureAsync(100m, [40m, 110m]);
        try
        {
            await fixture.Repository.DeliverAsync(fixture.OrderId, CancellationToken.None);
            await fixture.Repository.DeliverAsync(fixture.OrderId, CancellationToken.None);

            Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId", fixture.OrderId));
            Assert.Equal(100m, await ScalarDecimalAsync("SELECT SUM(AppliedAmount) FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId", fixture.OrderId));
            await AssertAppliedPostingAsync(fixture.OrderId, 100m, 2);
            Assert.Equal(50m, await ScalarDecimalAsync("SELECT p.Amount-COALESCE(SUM(app.AppliedAmount),0) FROM dbo.Payments p LEFT JOIN dbo.CustomerAdvanceApplications app ON app.AdvancePaymentId=p.PaymentID WHERE p.PaymentID=@paymentId GROUP BY p.Amount", fixture.PaymentIds[1]));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.CashMovements cm INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId WHERE ae.AccountingEventType=6 AND ae.CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId)", fixture.OrderId));
            Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@orderId AND PaymentKind=N'Advance'", fixture.OrderId));
        }
        finally { await DeleteFixtureAsync(fixture); }
    }

    private static async Task AssertAppliedPostingAsync(int orderId, decimal amount, int expectedCount)
    {
        Assert.Equal(expectedCount, await CountAsync("SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.CustomerAdvanceApplications app ON app.AdvanceApplicationId=ae.CustomerAdvanceApplicationId WHERE app.OrderId=@orderId AND ae.AccountingEventType=6", orderId));
        Assert.Equal(expectedCount, await CountAsync("SELECT COUNT(*) FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId INNER JOIN dbo.CustomerAdvanceApplications app ON app.AdvanceApplicationId=ae.CustomerAdvanceApplicationId WHERE app.OrderId=@orderId AND ft.TransactionType=N'CustomerAdvanceApplied'", orderId));
        Assert.Equal(amount, await ScalarDecimalAsync("SELECT SUM(jel.DebitAmount) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.CustomerAdvanceApplications app ON app.AdvanceApplicationId=ae.CustomerAdvanceApplicationId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE app.OrderId=@orderId AND la.AccountCode=N'1160'", orderId));
        Assert.Equal(amount, await ScalarDecimalAsync("SELECT SUM(jel.CreditAmount) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.CustomerAdvanceApplications app ON app.AdvanceApplicationId=ae.CustomerAdvanceApplicationId INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId=jel.LedgerAccountId WHERE app.OrderId=@orderId AND la.AccountCode=N'1200'", orderId));
    }

    private static async Task<Fixture> CreateFixtureAsync(decimal netAmount, IReadOnlyList<decimal> advances)
    {
        var connectionString = GetConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
        var createdAt = DateTime.UtcNow;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var customerId = Convert.ToInt32(await new SqlCommand("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID", connection).ExecuteScalarAsync());
        var orderNumber = $"CAA-{Guid.NewGuid():N}";
        var paidAmount = advances.Sum();
        await using var insertOrder = new SqlCommand("INSERT INTO dbo.Orders (OrderNumber,CustomerID,OrderDate,TotalAmount,DiscountAmount,PaidAmount,RemainingAmount,UrgencyStatus,OrderStatus,CreatedDate,SaleCategory,RevenueRecognized,RevenueReversalCreated) OUTPUT INSERTED.OrderID VALUES (@number,@customerId,@created,@total,0,@paid,@remaining,N'Normal',N'ReadyForDelivery',@created,N'TailoringOrder',0,0)", connection);
        insertOrder.Parameters.AddWithValue("@number", orderNumber);
        insertOrder.Parameters.AddWithValue("@customerId", customerId);
        insertOrder.Parameters.AddWithValue("@created", createdAt);
        insertOrder.Parameters.AddWithValue("@total", netAmount);
        insertOrder.Parameters.AddWithValue("@paid", paidAmount);
        insertOrder.Parameters.AddWithValue("@remaining", Math.Max(0m, netAmount - paidAmount));
        var orderId = Convert.ToInt32(await insertOrder.ExecuteScalarAsync());
        var paymentIds = new List<int>();
        foreach (var advance in advances)
        {
            await using var transaction = connection.BeginTransaction();
            await using var payment = new SqlCommand("INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,CreatedDate,PaymentKind) OUTPUT INSERTED.PaymentID VALUES (@orderId,@created,@amount,N'Cash',@reference,@created,N'Advance')", connection, transaction);
            payment.Parameters.AddWithValue("@orderId", orderId);
            payment.Parameters.AddWithValue("@created", createdAt);
            payment.Parameters.AddWithValue("@amount", advance);
            payment.Parameters.AddWithValue("@reference", $"TEST-{Guid.NewGuid():N}");
            var paymentId = Convert.ToInt32(await payment.ExecuteScalarAsync());
            await AccountingEventPostingGateway.PostAsync(connection, transaction, AccountingEventType.CustomerAdvance, advance, paymentId, null, null, null, null, null, CancellationToken.None);
            await transaction.CommitAsync();
            paymentIds.Add(paymentId);
        }
        return new Fixture(repository, orderId, orderNumber, customerId, paymentIds, createdAt);
    }

    private static async Task DeleteFixtureAsync(Fixture fixture)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
DELETE jel FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId WHERE ae.OrderId=@orderId OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID=@orderId) OR ae.CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId);
DELETE je FROM dbo.JournalEntries je INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId WHERE ae.OrderId=@orderId OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID=@orderId) OR ae.CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId);
DELETE ft FROM dbo.FinancialTransactions ft INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=ft.AccountingEventId WHERE ae.OrderId=@orderId OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID=@orderId) OR ae.CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId);
DELETE FROM dbo.AccountingEvents WHERE CustomerAdvanceApplicationId IN (SELECT AdvanceApplicationId FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId);
EXEC sys.sp_set_session_context @key=N'CustomerAdvanceApplicationWriter', @value=1;
DELETE FROM dbo.CustomerAdvanceApplications WHERE OrderId=@orderId;
EXEC sys.sp_set_session_context @key=N'CustomerAdvanceApplicationWriter', @value=NULL;
DELETE FROM dbo.AccountingEvents WHERE OrderId=@orderId OR PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID=@orderId);
DELETE FROM dbo.Payments WHERE OrderID=@orderId;
DELETE FROM dbo.Orders WHERE OrderID=@orderId;", connection);
        command.Parameters.AddWithValue("@orderId", fixture.OrderId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(string sql, int value)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", value);
        command.Parameters.AddWithValue("@customerId", value);
        command.Parameters.AddWithValue("@paymentId", value);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.AddMinutes(1));
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountCustomerLedgerEntriesAsync(int customerId, DateTime createdAt)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.CustomerLedgerEntries WHERE CustomerID=@customerId AND CreatedAt>=@createdAt", connection);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@createdAt", createdAt);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(string sql, int value)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", value);
        command.Parameters.AddWithValue("@paymentId", value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static string GetConnectionString() => "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    private sealed record Fixture(OrderRepository Repository, int OrderId, string OrderNumber, int CustomerId, IReadOnlyList<int> PaymentIds, DateTime CreatedAt);
}