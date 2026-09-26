using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class CustomerPaymentCollectionTests
{
    [Fact]
    public async Task CollectCustomerPaymentAsync_Should_Create_Advance_Payment_Before_Delivery_And_DebtCollection_After_Delivery()
    {
        var connectionString = GetConnectionString();
        var repository = CreateOrderRepository(connectionString);

        var advanceOrderNumber = $"ORD-COLL-ADV-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var advanceOrderId = await InsertOrderAsync(connectionString, advanceOrderNumber, 850.00m, 0m, 120.00m, "New", null, false);

        var advanceResult = await repository.CollectCustomerPaymentAsync(advanceOrderId, 330.00m, "Cash", 1, "RCPT-ADV-1", "دفعة مقدمة", CancellationToken.None);
        Assert.NotNull(advanceResult);
        Assert.Equal(400.00m, advanceResult!.RemainingAmount);

        var advancePayment = await QueryPaymentAsync(connectionString, advanceOrderId, "RCPT-ADV-1");
        Assert.NotNull(advancePayment);
        Assert.Equal("Advance", advancePayment!.PaymentKind);

        var advanceTransaction = await QueryFinancialTransactionAsync(connectionString, "RCPT-ADV-1");
        Assert.NotNull(advanceTransaction);
        Assert.Equal("CustomerAdvance", advanceTransaction!.TransactionType);
        Assert.Equal(330.00m, advanceTransaction.Amount);

        var debtOrderNumber = $"ORD-COLL-DEBT-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var debtOrderId = await InsertOrderAsync(connectionString, debtOrderNumber, 850.00m, 0m, 120.00m, "Delivered", DateTime.UtcNow, true);

        var debtResult = await repository.CollectCustomerPaymentAsync(debtOrderId, 730.00m, "Cash", 1, "RCPT-DEBT-1", "تحصيل ذمة", CancellationToken.None);
        Assert.NotNull(debtResult);
        Assert.Equal(0m, debtResult!.RemainingAmount);

        var debtPayment = await QueryPaymentAsync(connectionString, debtOrderId, "RCPT-DEBT-1");
        Assert.NotNull(debtPayment);
        Assert.Equal("DebtCollection", debtPayment!.PaymentKind);
        Assert.Equal(730.00m, debtPayment.Amount);

        var debtTransaction = await QueryFinancialTransactionAsync(connectionString, "RCPT-DEBT-1");
        Assert.NotNull(debtTransaction);
        Assert.Equal("CustomerPayment", debtTransaction!.TransactionType);
        Assert.Equal(730.00m, debtTransaction.Amount);
    }

    [Fact]
    public async Task CollectCustomerPaymentAsync_Should_Reject_Replayed_Request_Using_Same_Reference_But_Accept_Same_Amount_With_Different_Reference()
    {
        var connectionString = GetConnectionString();
        var repository = CreateOrderRepository(connectionString);

        var orderNumber = $"ORD-COLL-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var orderId = await InsertOrderAsync(connectionString, orderNumber, 1200.00m, 0m, 0.00m, "New", null, false);

        var first = await repository.CollectCustomerPaymentAsync(orderId, 350.00m, "Cash", 1, "RCPT-TEST-2", "تحصيل أول", CancellationToken.None);
        Assert.NotNull(first);

        var aboveLimit = await repository.CollectCustomerPaymentAsync(orderId, 900.00m, "Cash", 1, "RCPT-TEST-3", "تحصيل زائد", CancellationToken.None);
        Assert.Null(aboveLimit);

        var duplicate = await repository.CollectCustomerPaymentAsync(orderId, 350.00m, "Cash", 1, "RCPT-TEST-2", "إعادة إرسال نفس المرجع", CancellationToken.None);
        Assert.Null(duplicate);

        var sameAmountDifferentReference = await repository.CollectCustomerPaymentAsync(orderId, 350.00m, "Cash", 1, "RCPT-TEST-2B", "دفعة جديدة مشروعة بنفس المبلغ", CancellationToken.None);
        Assert.NotNull(sameAmountDifferentReference);
        Assert.Equal(700.00m, sameAmountDifferentReference!.PaidAmount);
    }

    [Fact]
    public async Task SettleCustomerBalanceAsync_Should_HandleFullAndPartialCollections_AndBlockUnapprovedDiscountAndDonation()
    {
        var connectionString = GetConnectionString();
        var repository = CreateOrderRepository(connectionString);

        var fullOrderNumber = $"ORD-SETTLE-FULL-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var fullOrderId = await InsertOrderAsync(connectionString, fullOrderNumber, 1000.00m, 0m, 0m, "Delivered", DateTime.UtcNow, true);
        var fullResult = await repository.SettleCustomerBalanceAsync(fullOrderId, 1000.00m, 0m, "Cash", 1, $"{fullOrderNumber}:Receipt", "تحصيل كامل", CancellationToken.None);
        Assert.NotNull(fullResult);
        Assert.Equal(1000.00m, fullResult!.PaidAmount);
        Assert.Equal(0m, fullResult.RemainingAmount);
        Assert.Equal(1000.00m, (await QueryPaymentAsync(connectionString, fullOrderId, $"{fullOrderNumber}:Receipt"))!.Amount);
        Assert.Equal(1000.00m, (await QueryCustomerLedgerAsync(connectionString, fullOrderId, $"{fullOrderNumber}:Receipt"))!.CreditAmount);

        var fullDiscountOrderNumber = $"ORD-SETTLE-FULL-DISCOUNT-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var fullDiscountOrderId = await InsertOrderAsync(connectionString, fullDiscountOrderNumber, 1000.00m, 0m, 0m, "Delivered", DateTime.UtcNow, true);
        var fullDiscountReference = $"{fullDiscountOrderNumber}:Receipt";
        var fullDiscountException = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SettleCustomerBalanceAsync(fullDiscountOrderId, 700.00m, 300.00m, "Cash", 1, fullDiscountReference, "تحصيل كامل مع خصم", CancellationToken.None));
        Assert.Equal("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.", fullDiscountException.Message);

        var partialOrderNumber = $"ORD-SETTLE-PARTIAL-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var partialOrderId = await InsertOrderAsync(connectionString, partialOrderNumber, 1000.00m, 0m, 0m, "Delivered", DateTime.UtcNow, true);
        var partialReference = $"{partialOrderNumber}:Receipt";
        var partialResult = await repository.SettleCustomerBalanceAsync(partialOrderId, 400.00m, 0m, "Cash", 1, partialReference, "تحصيل جزئي", CancellationToken.None);
        Assert.NotNull(partialResult);
        Assert.Equal(400.00m, partialResult!.PaidAmount);
        Assert.Equal(600.00m, partialResult.RemainingAmount);
        Assert.Equal(400.00m, (await QueryCustomerLedgerAsync(connectionString, partialOrderId, partialReference))!.CreditAmount);

        var partialDiscountOrderNumber = $"ORD-SETTLE-PARTIAL-DISCOUNT-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var partialDiscountOrderId = await InsertOrderAsync(connectionString, partialDiscountOrderNumber, 1000.00m, 0m, 0m, "Delivered", DateTime.UtcNow, true);
        var partialDiscountReference = $"{partialDiscountOrderNumber}:Receipt";
        var partialDiscountException = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SettleCustomerBalanceAsync(partialDiscountOrderId, 400.00m, 100.00m, "Cash", 1, partialDiscountReference, "تحصيل جزئي مع خصم", CancellationToken.None));
        Assert.Equal("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.", partialDiscountException.Message);

        var donationOrderNumber = $"ORD-SETTLE-DONATION-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var donationOrderId = await InsertOrderAsync(connectionString, donationOrderNumber, 1000.00m, 0m, 0m, "Delivered", DateTime.UtcNow, true);
        var donationException = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.WaiveRemainingBalanceAsync(donationOrderId, CancellationToken.None));
        Assert.Equal("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.", donationException.Message);
    }

    private static string GetConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("Lumar__ConnectionString");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    private static OrderRepository CreateOrderRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static async Task<int> InsertOrderAsync(string connectionString, string orderNumber, decimal totalAmount, decimal discountAmount, decimal paidAmount, string orderStatus, DateTime? deliveryDate, bool revenueRecognized)
    {
        var customerId = await GetFirstCustomerIdAsync(connectionString);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.Orders (
                OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount,
                PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, Notes, CreatedDate,
                SaleCategory, RevenueRecognized, RevenueRecognizedAt)
            OUTPUT INSERTED.OrderID
            VALUES (
                @orderNumber, @customerId, @orderDate, @deliveryDate, @totalAmount, @discountAmount,
                @paidAmount, @remainingAmount, N'Normal', @orderStatus, N'Customer payment collection test', @createdAt,
                N'TailoringOrder', @revenueRecognized, @revenueRecognizedAt);",
            connection);

        command.Parameters.AddWithValue("@orderNumber", orderNumber);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderDate", DateTime.UtcNow);
        command.Parameters.AddWithValue("@deliveryDate", deliveryDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@totalAmount", totalAmount);
        command.Parameters.AddWithValue("@discountAmount", discountAmount);
        command.Parameters.AddWithValue("@paidAmount", paidAmount);
        command.Parameters.AddWithValue("@remainingAmount", totalAmount - discountAmount - paidAmount);
        command.Parameters.AddWithValue("@orderStatus", orderStatus);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@revenueRecognized", revenueRecognized);
        command.Parameters.AddWithValue("@revenueRecognizedAt", revenueRecognized ? DateTime.UtcNow : (object)DBNull.Value);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> GetFirstCustomerIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID", connection);
        var result = await command.ExecuteScalarAsync();
        return result is int id ? id : throw new InvalidOperationException("No customers available for test.");
    }

    private static async Task<PaymentRow?> QueryPaymentAsync(string connectionString, int orderId, string referenceNumber)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT PaymentID, OrderID, Amount, PaymentMethod, ReferenceNo, PaymentKind, CreatedDate FROM dbo.Payments WHERE OrderID = @orderId AND ReferenceNo = @referenceNumber ORDER BY PaymentID DESC",
            connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new PaymentRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDateTime(6));
    }

    private static async Task<CustomerLedgerRow?> QueryCustomerLedgerAsync(string connectionString, int orderId, string referenceNumber)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            @"
            SELECT TOP(1) cle.CustomerLedgerEntryId, cle.CustomerID, cle.ReferenceNumber, cle.DebitAmount, cle.CreditAmount, cle.BalanceAfterTransaction, cle.CreatedAt
            FROM dbo.CustomerLedgerEntries cle
            INNER JOIN dbo.Orders o ON o.CustomerID = cle.CustomerID
            WHERE o.OrderID = @orderId AND cle.ReferenceNumber = @referenceNumber
            ORDER BY cle.CustomerLedgerEntryId DESC",
            connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CustomerLedgerRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDateTime(6));
    }

    private static async Task<FinancialTransactionRow?> QueryFinancialTransactionAsync(string connectionString, string referenceNumber)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP(1) FinancialTransactionId, ReferenceNumber, TransactionType, Amount, Description, CreatedAt FROM dbo.FinancialTransactions WHERE ReferenceNumber = @referenceNumber ORDER BY FinancialTransactionId DESC",
            connection);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new FinancialTransactionRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetDateTime(5));
    }

    private sealed record PaymentRow(int PaymentId, int OrderId, decimal Amount, string? PaymentMethod, string ReferenceNumber, string PaymentKind, DateTime CreatedDate);
    private sealed record CustomerLedgerRow(int CustomerLedgerEntryId, int CustomerId, string ReferenceNumber, decimal DebitAmount, decimal CreditAmount, decimal BalanceAfterTransaction, DateTime CreatedAt);
    private sealed record FinancialTransactionRow(int FinancialTransactionId, string ReferenceNumber, string TransactionType, decimal Amount, string? Description, DateTime CreatedAt);
}
