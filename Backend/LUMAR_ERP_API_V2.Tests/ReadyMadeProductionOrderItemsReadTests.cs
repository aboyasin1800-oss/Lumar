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
    public async Task GetReadyMadeOrderItemsAsync_ReadsFirstExistingOrderWithNullableProductTypeId()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new ProductionRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new ProductionProductTypeIdentityResolver());

        var orderId = await ReadFirstOrderIdAsync(connectionString);
        var items = await repository.GetReadyMadeOrderItemsAsync(orderId, CancellationToken.None);

        Assert.NotEmpty(items);
        Assert.Equal(1, items[0].ReadyMadeProductionOrderItemId);
        Assert.Null(items[0].ProductTypeId);
        Assert.Equal("كوت", items[0].PieceType);
        Assert.Equal("New", items[0].PieceStatus);
    }

    private static async Task<int> ReadFirstOrderIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP (1) ReadyMadeProductionOrderId FROM dbo.ReadyMadeProductionOrderItems ORDER BY ReadyMadeProductionOrderItemId",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}