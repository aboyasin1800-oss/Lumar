using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeProductionOrderItemsReadTests
{
    [Fact]
    public async Task GetReadyMadeOrderItemsAsync_ReadsSeededOrderWithNullableProductTypeId()
    {
        var connectionString = GetValidationConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new ProductionRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new ProductionProductTypeIdentityResolver());

        var seed = await SeedAsync(connectionString);
        try
        {
            var items = await repository.GetReadyMadeOrderItemsAsync(seed.OrderId, CancellationToken.None);

            var item = Assert.Single(items);
            Assert.Equal(seed.ItemId, item.ReadyMadeProductionOrderItemId);
            Assert.Null(item.ProductTypeId);
            Assert.Equal("كوت", item.PieceType);
            Assert.Equal("New", item.PieceStatus);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    private static string GetValidationConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must target the validation database.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_CUSTOMERS_ONLY_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ready-made production order item tests are restricted to the validation database.");
        return builder.ConnectionString;
    }

    private static async Task<(int OrderId, int ItemId)> SeedAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(@"
                INSERT INTO dbo.ReadyMadeProductionOrders
                    (ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice, Status, Notes, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderId
                VALUES (@number, N'اختبار قراءة إنتاج جاهز', 100, 0, 100, N'New', N'Fixture test', SYSUTCDATETIME());", connection, transaction);
            command.Parameters.AddWithValue("@number", $"RM-READ-{suffix}");
            var orderId = Convert.ToInt32(await command.ExecuteScalarAsync());

            command.CommandText = @"
                INSERT INTO dbo.ReadyMadeProductionOrderItems
                    (ReadyMadeProductionOrderId, PieceType, Quantity, MeasurementSnapshot, PieceStatus, CreatedAt, ProductTypeId)
                OUTPUT INSERTED.ReadyMadeProductionOrderItemId
                VALUES (@orderId, N'كوت', 1, N'{}', N'New', SYSUTCDATETIME(), NULL);";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@orderId", orderId);
            var itemId = Convert.ToInt32(await command.ExecuteScalarAsync());
            await transaction.CommitAsync();
            return (orderId, itemId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, (int OrderId, int ItemId) seed)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DELETE FROM dbo.ReadyMadeProductionOrderItems WHERE ReadyMadeProductionOrderItemId = @itemId;
            DELETE FROM dbo.ReadyMadeProductionOrders WHERE ReadyMadeProductionOrderId = @orderId;", connection);
        command.Parameters.AddWithValue("@itemId", seed.ItemId);
        command.Parameters.AddWithValue("@orderId", seed.OrderId);
        await command.ExecuteNonQueryAsync();
    }
}