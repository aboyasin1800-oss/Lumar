using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeProductionPhaseOneIntegrationTests
{
    [Fact]
    public async Task CompletingOneReadyMadePiece_TransfersOnlyThatPieceAndPreventsDuplicates()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new ProductionRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new ProductionProductTypeIdentityResolver());
        var productType = await ReadProductTypeWithRouteAsync(connectionString);
        var measurementSnapshot = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["الطول"] = "42",
            ["الكتف"] = "18",
        });
        var productionName = $"RM1-Live-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var created = await repository.CreateReadyMadeOrderAsync(new ReadyMadeProductionOrderCreateDto(
            "",
            productionName,
            0m,
            0m,
            0m,
            "PHASE-RM-1 integration verification",
            [new ReadyMadeProductionOrderCreateItemDto(
                productType.NameAr,
                2,
                productType.ProductTypeId,
                null,
                null,
                null,
                null,
                0m,
                0m,
                0m,
                measurementSnapshot,
                "PHASE-RM-1")]), CancellationToken.None);

        var items = await repository.GetReadyMadeOrderItemsAsync(created.ReadyMadeProductionOrderId, CancellationToken.None);
        Assert.Single(items);
        var pieces = await repository.GetReadyMadeItemPiecesAsync(items[0].ReadyMadeProductionOrderItemId, CancellationToken.None);
        Assert.Equal(2, pieces.Count);
        var piece = pieces[0];
        var workCard = await repository.GetReadyMadeWorkCardAsync(piece.ReadyMadeProductionOrderPieceInstanceId, CancellationToken.None);
        Assert.NotNull(workCard);
        Assert.Equal(piece.TrackingCode, workCard!.TrackingCode);
        Assert.Equal(measurementSnapshot, workCard.MeasurementSnapshot);
        Assert.Equal("New", workCard.PieceStatus);
        var route = await repository.GetReadyMadePieceRouteByTrackingCodeAsync(piece.TrackingCode, CancellationToken.None);
        Assert.NotNull(route);
        Assert.Equal(productType.ProductTypeId, route!.ProductTypeId);
        Assert.Equal(productType.NameAr, route.PieceType);
        Assert.NotEmpty(route.Route);
        Console.WriteLine($"READY_MADE_ROUTE productTypeId={route.ProductTypeId} pieceType={route.PieceType} route={string.Join("->", route.Route)}");

        foreach (var stage in route.Route)
        {
            var advance = await repository.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(
                null,
                piece.TrackingCode,
                productType.NameAr,
                stage,
                null,
                null,
                "ExecutionSource=ManualTest;Operation=ReadyMadeCompletionVerification",
                productType.ProductTypeId,
                true), CancellationToken.None);

            Assert.NotNull(advance);
            Assert.True(advance!.Updated);
            Assert.True(advance.IsReadyMade);
            Assert.Equal(stage, advance.NewStatus);
        }

        var repeatedFinalStage = await repository.AdvancePieceStageAsync(new ProductionTrackingAdvanceRequestDto(
            null,
            piece.TrackingCode,
            productType.NameAr,
            route.Route[^1],
            null,
            null,
            "ExecutionSource=ManualTest;Operation=ReadyMadeDuplicateVerification",
            productType.ProductTypeId,
            true), CancellationToken.None);
        Assert.NotNull(repeatedFinalStage);
        Assert.True(repeatedFinalStage!.Updated);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT
                (SELECT COUNT(*) FROM dbo.TrackingEvents WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId) AS TrackingCount,
                (SELECT COUNT(*) FROM dbo.ReadyMadeInventoryProducts WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId AND Status = N'AvailableForSale' AND IsActive = 1) AS InventoryCount,
                (SELECT PieceStatus FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId) AS PieceStatus,
                (SELECT PieceStatus FROM dbo.ReadyMadeProductionOrderItems WHERE ReadyMadeProductionOrderItemId = @itemId) AS ItemStatus,
                (SELECT Status FROM dbo.ReadyMadeProductionOrders WHERE ReadyMadeProductionOrderId = @orderId) AS OrderStatus,
                (SELECT PieceStatus FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE ReadyMadeProductionOrderPieceInstanceId = @secondPieceId) AS SecondPieceStatus;", connection);
        command.Parameters.AddWithValue("@pieceId", piece.ReadyMadeProductionOrderPieceInstanceId);
            command.Parameters.AddWithValue("@itemId", items[0].ReadyMadeProductionOrderItemId);
            command.Parameters.AddWithValue("@secondPieceId", pieces[1].ReadyMadeProductionOrderPieceInstanceId);
        command.Parameters.AddWithValue("@orderId", created.ReadyMadeProductionOrderId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
            Assert.Equal(route.Route.Count, reader.GetInt32(0));
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal(route.Route[^1], reader.GetString(2));
            Assert.Equal("InProduction", reader.GetString(3));
            Assert.Equal("InProduction", reader.GetString(4));
            Assert.Equal("New", reader.GetString(5));
            Console.WriteLine($"READY_MADE_COMPLETION order={created.ReadyMadeProductionOrderId} item={items[0].ReadyMadeProductionOrderItemId} piece={piece.ReadyMadeProductionOrderPieceInstanceId} tracking={piece.TrackingCode} eventCount={reader.GetInt32(0)} inventoryCount={reader.GetInt32(1)} stage={reader.GetString(2)}");
    }

    private static async Task<(int ProductTypeId, string NameAr)> ReadProductTypeWithRouteAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
                await using var command = new SqlCommand(@"
            SELECT TOP (1) pt.ProductTypeId, pt.NameAr
            FROM dbo.PricingProductTypes pt
            INNER JOIN dbo.System_Settings s ON s.SettingName = N'ProductionRoutesConfig'
            WHERE pt.IsActive = 1 AND pt.NameAr IN (N'كوت', N'يلق', N'قميص')
                            AND s.SettingValue LIKE N'%""productTypeId"":' + CONVERT(nvarchar(20), pt.ProductTypeId) + N',%'
            ORDER BY CASE pt.NameAr WHEN N'كوت' THEN 0 WHEN N'يلق' THEN 1 ELSE 2 END, pt.ProductTypeId;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("لا يوجد نوع منتج فعال مرتبط بمسار إنتاج رسمي.");
        return (reader.GetInt32(0), reader.GetString(1));
    }
}
