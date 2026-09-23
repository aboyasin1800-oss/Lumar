using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class ProductionWorkCardMetadataTests
{
    [Fact]
    public async Task GetWorkCardAsync_Should_Return_Fabric_And_Request_Metadata_From_OrderItem()
    {
        var connectionString = GetConnectionString();
        var repository = CreateProductionRepository(connectionString);

        var customerId = await GetFirstCustomerIdAsync(connectionString);
        var orderId = await InsertOrderAsync(connectionString, customerId);
        var orderItemId = await InsertOrderItemAsync(connectionString, orderId, "FAB-TEST-001", "قماش", "أبيض", "CAT-TEST-001", "طلب خاص", "طلب ثان", "ملاحظة 1", "ملاحظة 2");
        var pieceId = await InsertPieceAsync(connectionString, orderItemId);

        var card = await repository.GetWorkCardAsync(pieceId, CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("FAB-TEST-001", card!.FabricCode);
        Assert.Equal("قماش", card.FabricType);
        Assert.Equal("أبيض", card.FabricColor);
        Assert.Equal("CAT-TEST-001", card.CatalogNumber);
        Assert.Equal("طلب خاص", card.Request1);
        Assert.Equal("طلب ثان", card.Request2);
        Assert.Equal("سمبوسة", card.SpecialRequest);
        Assert.Equal("ملاحظة 1", card.Notes1);
        Assert.Equal("ملاحظة 2", card.Notes2);
    }

    private static ProductionRepository CreateProductionRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new ProductionRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new ProductionProductTypeIdentityResolver());
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

    private static async Task<int> GetFirstCustomerIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP (1) CustomerID FROM dbo.Customers ORDER BY CustomerID", connection);
        var result = await command.ExecuteScalarAsync();
        return result is null ? throw new InvalidOperationException("No customer found in database for production tests.") : Convert.ToInt32(result);
    }

    private static async Task<int> InsertOrderAsync(string connectionString, int customerId)
    {
        var timestamp = DateTime.UtcNow;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.Orders (
                OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount,
                PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, Notes, CreatedDate,
                SaleCategory)
            OUTPUT INSERTED.OrderID
            VALUES (
                @orderNumber, @customerId, @orderDate, @deliveryDate, @totalAmount, @discountAmount,
                @paidAmount, @remainingAmount, N'Normal', N'New', N'Production metadata test', @createdAt,
                N'TailoringOrder');",
            connection);

        command.Parameters.AddWithValue("@orderNumber", $"ORD-PROD-{timestamp:yyyyMMddHHmmssfff}");
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderDate", timestamp);
        command.Parameters.AddWithValue("@deliveryDate", timestamp.AddDays(7));
        command.Parameters.AddWithValue("@totalAmount", 250m);
        command.Parameters.AddWithValue("@discountAmount", 0m);
        command.Parameters.AddWithValue("@paidAmount", 0m);
        command.Parameters.AddWithValue("@remainingAmount", 250m);
        command.Parameters.AddWithValue("@createdAt", timestamp);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertOrderItemAsync(string connectionString, int orderId, string fabricCode, string fabricType, string fabricColor, string catalogNumber, string request1, string request2, string notes1, string notes2)
    {
        var timestamp = DateTime.UtcNow;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.OrderItems (
                OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, Request1,
                Request2, Notes1, Notes2, MeasurementSnapshot, TrackingCode, PieceStatus, CreatedDate)
            OUTPUT INSERTED.OrderItemID
            VALUES (
                @orderId, N'قميص', 1, @fabricCode, @fabricType, @fabricColor, @request1,
                @request2, @notes1, @notes2, @measurementSnapshot, @trackingCode, N'New', @createdAt);",
            connection);

        var measurementSnapshot = $"{{\"_catalogNumber\":\"{catalogNumber}\",\"_consumption\":\"10.5\",\"_consumptionUnit\":\"بوصة\",\"fabricCode\":\"{fabricCode}\",\"fabricType\":\"{fabricType}\",\"fabricColor\":\"{fabricColor}\",\"specialRequest\":\"سمبوسة\"}}";

        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@fabricCode", fabricCode);
        command.Parameters.AddWithValue("@fabricType", fabricType);
        command.Parameters.AddWithValue("@fabricColor", fabricColor);
        command.Parameters.AddWithValue("@request1", request1);
        command.Parameters.AddWithValue("@request2", request2);
        command.Parameters.AddWithValue("@notes1", notes1);
        command.Parameters.AddWithValue("@notes2", notes2);
        command.Parameters.AddWithValue("@measurementSnapshot", measurementSnapshot);
        command.Parameters.AddWithValue("@trackingCode", $"TRK-PROD-{timestamp:yyyyMMddHHmmssfff}");
        command.Parameters.AddWithValue("@createdAt", timestamp);

        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertPieceAsync(string connectionString, int orderItemId)
    {
        var timestamp = DateTime.UtcNow;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"
            INSERT INTO dbo.Pieces (
                OrderItemID, TrackingCode, PieceStatus, PieceNumber, CreatedDate)
            OUTPUT INSERTED.PieceID
            VALUES (
                @orderItemId, @trackingCode, N'New', 1, @createdAt);",
            connection);

        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        command.Parameters.AddWithValue("@trackingCode", $"TRK-PIECE-{timestamp:yyyyMMddHHmmssfff}");
        command.Parameters.AddWithValue("@createdAt", timestamp);

        return (int)(await command.ExecuteScalarAsync());
    }
}
