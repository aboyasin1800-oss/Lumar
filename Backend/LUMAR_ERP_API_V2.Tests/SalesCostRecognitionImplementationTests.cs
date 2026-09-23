using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class SalesCostRecognitionImplementationTests
{
    [Fact]
    public async Task RecognizeDeliveryRevenueAsync_Should_Create_DeliveryCost_For_Delivered_Order()
    {
        var connectionString = GetConnectionString();
        var repository = CreateOrderRepository(connectionString);

        var testOrderNumber = $"ORD-TEST-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var orderId = await InsertDeliveredTestOrderAsync(connectionString, testOrderNumber, 1425.50m, 125.00m, 100.00m);
        var orderItemId = await InsertOrderItemAsync(connectionString, orderId, $"TEST-{DateTime.UtcNow:HHmmssfff}", "قميص", 1, "FAB-DELIVERY", "قماش", "أبيض");
        await InsertOrderFabricAsync(connectionString, orderItemId, "FAB-DELIVERY", "قماش", "أبيض", 350.00m);

        var result = await repository.RecognizeDeliveryRevenueAsync(orderId, CancellationToken.None);
        var transaction = await QuerySingleTransactionAsync(connectionString, $"{testOrderNumber}:DeliveryCost", "DeliveryCost");

        Assert.NotNull(result);
        Assert.NotNull(transaction);
        Assert.Equal($"{testOrderNumber}:DeliveryCost", transaction!.ReferenceNumber);
        Assert.Equal("DeliveryCost", transaction.TransactionType);
        Assert.Equal(350.00m, transaction.Amount);

        Console.WriteLine($"DeliveryCost created: Id={transaction.FinancialTransactionId}, ReferenceNumber={transaction.ReferenceNumber}, Amount={transaction.Amount}, TransactionType={transaction.TransactionType}");
    }

    [Fact]
    public async Task RecordReadyMadeSaleCostAsync_Should_Create_ReadyMadeCost_For_Sold_Product_Only_Once()
    {
        var connectionString = GetConnectionString();
        var repository = CreateInventoryRepository(connectionString);

        var productionNumber = $"RMP-TEST-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var productId = await InsertSoldReadyMadeProductAsync(connectionString, productionNumber, 680.00m);

        var first = await repository.RecordReadyMadeSaleCostAsync(productId, CancellationToken.None);
        var second = await repository.RecordReadyMadeSaleCostAsync(productId, CancellationToken.None);

        var reference = await QueryReadyMadeCostReferenceAsync(connectionString, productId);
        var count = await CountReadyMadeCostRowsAsync(connectionString, reference);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(reference);
        Assert.Equal(1, count);

        var transaction = await QuerySingleTransactionAsync(connectionString, reference!, "ReadyMadeCost");
        Assert.NotNull(transaction);
        Assert.Equal(680.00m, transaction!.Amount);

        Console.WriteLine($"ReadyMadeCost created: Id={transaction.FinancialTransactionId}, ReferenceNumber={transaction.ReferenceNumber}, Amount={transaction.Amount}, TransactionType={transaction.TransactionType}");
    }

    [Fact]
    public async Task FinancialTransactionJournalPoster_Should_Create_Balanced_Journal_For_ReadyMadeCost()
    {
        var connectionString = GetConnectionString();
        var reference = $"JOURNAL-TEST-{DateTime.UtcNow:yyyyMMddHHmmssfff}:ReadyMadeCost";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await InsertFinancialTransactionAsync(connection, transaction, reference, "ReadyMadeCost", 725.00m, "Ready-made sale cost recognized");

            var created = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(connection, transaction, reference, "ReadyMadeCost", 725.00m, "Ready-made sale cost recognized", CancellationToken.None);
            Assert.True(created);

            var existingEntry = await QueryJournalEntryAsync(connection, transaction, reference);
            Assert.NotNull(existingEntry);
            Assert.Equal(reference, existingEntry!.ReferenceNumber);

            var lines = await QueryJournalLinesAsync(connection, transaction, existingEntry.JournalEntryId);
            Assert.Equal(2, lines.Count);
            Assert.Equal(725.00m, lines.Sum(l => l.DebitAmount));
            Assert.Equal(725.00m, lines.Sum(l => l.CreditAmount));

            var createdAgain = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(connection, transaction, reference, "ReadyMadeCost", 725.00m, "Ready-made sale cost recognized", CancellationToken.None);
            Assert.False(createdAgain);
        }
        finally
        {
            transaction.Rollback();
        }
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

    private static InventoryRepository CreateInventoryRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new InventoryRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static async Task<int> InsertDeliveredTestOrderAsync(string connectionString, string orderNumber, decimal totalAmount, decimal discountAmount, decimal advancePayment)
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
                @paidAmount, @remainingAmount, N'Normal', N'Delivered', N'Delivery Cost Test', @createdAt,
                N'Custom', 0, NULL);",
            connection);

        command.Parameters.AddWithValue("@orderNumber", orderNumber);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderDate", DateTime.UtcNow);
        command.Parameters.AddWithValue("@deliveryDate", DateTime.UtcNow);
        command.Parameters.AddWithValue("@totalAmount", totalAmount);
        command.Parameters.AddWithValue("@discountAmount", discountAmount);
        command.Parameters.AddWithValue("@paidAmount", advancePayment);
        command.Parameters.AddWithValue("@remainingAmount", totalAmount - discountAmount - advancePayment);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertOrderItemAsync(string connectionString, int orderId, string trackingCode, string pieceType, int quantity, string fabricCode, string fabricType, string fabricColor)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.OrderItems (
                OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor,
                MeasurementSnapshot, TrackingCode, PieceStatus, CreatedDate)
            OUTPUT INSERTED.OrderItemID
            VALUES (
                @orderId, @pieceType, @quantity, @fabricCode, @fabricType, @fabricColor,
                N'Integration test snapshot', @trackingCode, N'New', @createdAt);",
            connection);

        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@pieceType", pieceType);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@fabricCode", fabricCode);
        command.Parameters.AddWithValue("@fabricType", fabricType);
        command.Parameters.AddWithValue("@fabricColor", fabricColor);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task InsertOrderFabricAsync(string connectionString, int orderItemId, string fabricCode, string fabricType, string fabricColor, decimal totalCost)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.OrderItemFabrics (
                OrderItemID, InventoryItemID, FabricCode, FabricType, FabricColor,
                Quantity, Unit, UnitCost, TotalCost, ConsumedQuantity, CreatedDate)
            VALUES (
                @orderItemId, NULL, @fabricCode, @fabricType, @fabricColor, 1, N'Piece', @unitCost,
                @totalCost, 0, @createdAt);",
            connection);

        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@fabricCode", fabricCode);
        command.Parameters.AddWithValue("@fabricType", fabricType);
        command.Parameters.AddWithValue("@fabricColor", fabricColor);
        command.Parameters.AddWithValue("@unitCost", totalCost);
        command.Parameters.AddWithValue("@totalCost", totalCost);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InsertSoldReadyMadeProductAsync(string connectionString, string productionNumber, decimal actualCost)
    {
        var now = DateTime.UtcNow;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var productionOrderId = await InsertReadyMadeProductionOrderAsync(connection, productionNumber, now, actualCost);
        var itemId = await InsertReadyMadeProductionItemAsync(connection, productionOrderId, now, actualCost);
        var pieceInstanceId = await InsertReadyMadeProductionPieceInstanceAsync(connection, itemId, now);

        await using var insertProduct = new SqlCommand(
            @"
            INSERT INTO dbo.ReadyMadeInventoryProducts (
                ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadeProductionOrderPieceInstanceId,
                ProductionOrderNumber, ProductionName, PieceType, PieceNumber, TrackingCode, FabricCode, FabricType,
                FabricColor, CatalogNumber, FabricUnit, FabricWidth, FabricWidthUnit, ActualCost, SuggestedSellingPrice,
                MeasurementSnapshot, ReadyForSaleAt, Status, Source, Notes, IsActive, CreatedAt)
            OUTPUT INSERTED.ReadyMadeInventoryProductId
            VALUES (
                @productionOrderId, @itemId, @pieceInstanceId,
                @productionNumber, @productionName, @pieceType, 1, @trackingCode, @fabricCode, @fabricType,
                @fabricColor, @catalogNumber, N'Piece', NULL, NULL, @actualCost, @actualCost,
                N'Integration test snapshot', @readyForSaleAt, N'Sold', N'IntegrationTest', N'Integration test ready-made sale cost', 1, @createdAt);",
            connection);

        insertProduct.Parameters.AddWithValue("@productionOrderId", productionOrderId);
        insertProduct.Parameters.AddWithValue("@itemId", itemId);
        insertProduct.Parameters.AddWithValue("@pieceInstanceId", pieceInstanceId);
        insertProduct.Parameters.AddWithValue("@productionNumber", productionNumber);
        insertProduct.Parameters.AddWithValue("@productionName", $"ReadyMadeSaleTest-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        insertProduct.Parameters.AddWithValue("@pieceType", "قميص");
        insertProduct.Parameters.AddWithValue("@trackingCode", $"TRK-SOLD-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        insertProduct.Parameters.AddWithValue("@fabricCode", "FAB-SOLD-TEST");
        insertProduct.Parameters.AddWithValue("@fabricType", "قماش");
        insertProduct.Parameters.AddWithValue("@fabricColor", "أبيض");
        insertProduct.Parameters.AddWithValue("@catalogNumber", "CAT-SOLD-TEST");
        insertProduct.Parameters.AddWithValue("@actualCost", actualCost);
        insertProduct.Parameters.AddWithValue("@readyForSaleAt", now);
        insertProduct.Parameters.AddWithValue("@createdAt", now);

        return (int)(await insertProduct.ExecuteScalarAsync());
    }

    private static async Task<int> InsertReadyMadeProductionOrderAsync(SqlConnection connection, string productionNumber, DateTime now, decimal actualCost)
    {
        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.ReadyMadeProductionOrders (
                ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice,
                Status, Notes, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderId
            VALUES (
                @productionNumber, @productionName, @actualCost, 0, @actualCost,
                N'Completed', N'Integration test ready-made sale cost', @createdAt);",
            connection);

        command.Parameters.AddWithValue("@productionNumber", productionNumber);
        command.Parameters.AddWithValue("@productionName", $"ReadyMadeSaleTest-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        command.Parameters.AddWithValue("@actualCost", actualCost);
        command.Parameters.AddWithValue("@createdAt", now);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertReadyMadeProductionItemAsync(SqlConnection connection, int productionOrderId, DateTime now, decimal actualCost)
    {
        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.ReadyMadeProductionOrderItems (
                ReadyMadeProductionOrderId, PieceType, Quantity, FabricCode, FabricType, FabricColor,
                CatalogNumber, FabricCost, PieceCost, LineTotal, MeasurementSnapshot, PieceStatus, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderItemId
            VALUES (
                @productionOrderId, N'قميص', 1, N'FAB-SOLD-TEST', N'قماش', N'أبيض', N'CAT-SOLD-TEST',
                @actualCost, @actualCost, @actualCost, N'Integration test snapshot', N'Ready', @createdAt);",
            connection);

        command.Parameters.AddWithValue("@productionOrderId", productionOrderId);
        command.Parameters.AddWithValue("@actualCost", actualCost);
        command.Parameters.AddWithValue("@createdAt", now);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertReadyMadeProductionPieceInstanceAsync(SqlConnection connection, int itemId, DateTime now)
    {
        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.ReadyMadeProductionOrderPieceInstances (
                ReadyMadeProductionOrderItemId, PieceNumber, TrackingCode, PieceStatus, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderPieceInstanceId
            VALUES (
                @itemId, 1, @trackingCode, N'Ready', @createdAt);",
            connection);

        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@trackingCode", $"TRK-SOLD-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        command.Parameters.AddWithValue("@createdAt", now);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> GetFirstCustomerIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP(1) CustomerID FROM dbo.Customers ORDER BY CustomerID", connection);
        var value = await command.ExecuteScalarAsync();
        return value is int customerId ? customerId : throw new InvalidOperationException("No customer exists for integration test.");
    }

    private static async Task<FinancialTransactionRow?> QuerySingleTransactionAsync(string connectionString, string referenceNumber, string transactionType)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            @"SELECT TOP(1) FinancialTransactionId, ReferenceNumber, TransactionType, Amount, Description, CreatedAt FROM dbo.FinancialTransactions WHERE ReferenceNumber = @referenceNumber AND TransactionType = @transactionType ORDER BY FinancialTransactionId DESC",
            connection);

        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        command.Parameters.AddWithValue("@transactionType", transactionType);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new FinancialTransactionRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetString(4),
            reader.GetDateTime(5));
    }

    private static async Task<string?> QueryReadyMadeCostReferenceAsync(string connectionString, int readyMadeInventoryProductId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            @"SELECT TOP(1) ReferenceNumber FROM dbo.FinancialTransactions WHERE TransactionType = N'ReadyMadeCost' AND ReferenceNumber LIKE @pattern ORDER BY FinancialTransactionId DESC",
            connection);

        command.Parameters.AddWithValue("@pattern", $"%Product:{readyMadeInventoryProductId}%");
        var value = await command.ExecuteScalarAsync();
        return value is string reference ? reference : null;
    }

    private static async Task<int> CountReadyMadeCostRowsAsync(string connectionString, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return 0;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            @"SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE ReferenceNumber = @reference AND TransactionType = N'ReadyMadeCost'",
            connection);

        command.Parameters.AddWithValue("@reference", reference);
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task InsertFinancialTransactionAsync(SqlConnection connection, SqlTransaction transaction, string reference, string transactionType, decimal amount, string description)
    {
        await using var command = new SqlCommand(
            @"INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt) VALUES (@reference, @transactionType, @amount, @description, @createdAt)",
            connection,
            transaction);

        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@description", description);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<JournalEntryRow?> QueryJournalEntryAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber)
    {
        await using var command = new SqlCommand(
            @"SELECT TOP(1) JournalEntryId, ReferenceNumber, Description, EntryDate, CreatedAt FROM dbo.JournalEntries WHERE ReferenceNumber = @referenceNumber ORDER BY JournalEntryId DESC",
            connection,
            transaction);

        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new JournalEntryRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDateTime(3),
            reader.GetDateTime(4));
    }

    private static async Task<List<JournalLineRow>> QueryJournalLinesAsync(SqlConnection connection, SqlTransaction transaction, int journalEntryId)
    {
        var rows = new List<JournalLineRow>();
        await using var command = new SqlCommand(
            @"SELECT JournalEntryLineId, JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId ORDER BY JournalEntryLineId",
            connection,
            transaction);

        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new JournalLineRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetString(5)));
        }

        return rows;
    }

    private sealed record FinancialTransactionRow(int FinancialTransactionId, string ReferenceNumber, string TransactionType, decimal Amount, string Description, DateTime CreatedAt);
    private sealed record JournalEntryRow(int JournalEntryId, string ReferenceNumber, string Description, DateTime EntryDate, DateTime CreatedAt);
    private sealed record JournalLineRow(int JournalEntryLineId, int JournalEntryId, int LedgerAccountId, decimal DebitAmount, decimal CreditAmount, string Description);
}
