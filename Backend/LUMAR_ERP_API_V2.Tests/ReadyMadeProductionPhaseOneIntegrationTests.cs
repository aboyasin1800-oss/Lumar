using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
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
        var readOnlyConnections = new ReadOnlySqlConnectionFactory(options);
        var operationalConnections = new OperationalSqlConnectionFactory(options);
        var consumptionRules = new ConsumptionRulesRepository(readOnlyConnections, operationalConnections, NullLogger<ConsumptionRulesRepository>.Instance);
        var pricingEngine = new PricingEngineService(
            new InventoryService(new InventoryRepository(readOnlyConnections, operationalConnections)),
            new PieceCostManagementService(new PieceCostManagementRepository(readOnlyConnections, operationalConnections)),
            new PricingProfitSettingsService(new PricingProfitSettingsRepository(operationalConnections)));
        var repository = new ProductionRepository(
            readOnlyConnections,
            operationalConnections,
            new ProductionProductTypeIdentityResolver(),
            consumptionRules,
            pricingEngine);
        var productType = await ReadProductTypeWithRouteAsync(connectionString);
        if (!await HasCompleteOfficialCostMatrixAsync(connectionString, productType.ProductTypeId))
        {
            Console.WriteLine($"READY_MADE_PRICING_NOT_EXECUTED productTypeId={productType.ProductTypeId}; السبب: لا توجد مصفوفة تكلفة تشغيل رسمية مكتملة.");
            return;
        }
        var fabricCode = await InsertTestFabricAsync(connectionString);
        var measurementSnapshot = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["length"] = "42",
            ["sleeve"] = "18",
            ["chest"] = "40",
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
                fabricCode,
                "قماش اختبار",
                null,
                null,
                0m,
                0m,
                0m,
                measurementSnapshot,
                "PHASE-RM-1")],
            $"PHASE-RM-1-{productionName}"), CancellationToken.None);

        var items = await repository.GetReadyMadeOrderItemsAsync(created.ReadyMadeProductionOrderId, CancellationToken.None);
        Assert.Single(items);
        var pieces = await repository.GetReadyMadeItemPiecesAsync(items[0].ReadyMadeProductionOrderItemId, CancellationToken.None);
        Assert.Equal(2, pieces.Count);
        var piece = pieces[0];
        var workCard = await repository.GetReadyMadeWorkCardAsync(piece.ReadyMadeProductionOrderPieceInstanceId, CancellationToken.None);
        Assert.NotNull(workCard);
        Assert.Equal(piece.TrackingCode, workCard!.TrackingCode);
        Assert.Contains("_pricing", workCard.MeasurementSnapshot);
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

        await using var priceCommand = new SqlCommand(@"
            SELECT TOP (1) ReadyMadeInventoryProductId, ActualCost, SuggestedSellingPrice, Status
            FROM dbo.ReadyMadeInventoryProducts
            WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId;
            SELECT COUNT(*) FROM dbo.InventoryTransactions
            WHERE ReferenceNumber = @reference AND TransactionType = N'Consumption';", connection);
        priceCommand.Parameters.AddWithValue("@pieceId", piece.ReadyMadeProductionOrderPieceInstanceId);
        priceCommand.Parameters.AddWithValue("@reference", $"{created.ProductionOrderNumber}:Fabric:{fabricCode}");
        await using var priceReader = await priceCommand.ExecuteReaderAsync();
        Assert.True(await priceReader.ReadAsync());
        Assert.True(priceReader.GetDecimal(1) > 0m);
        Assert.True(priceReader.GetDecimal(2) > 0m);
        Assert.Equal("AvailableForSale", priceReader.GetString(3));
        Assert.True(await priceReader.NextResultAsync());
        Assert.True(await priceReader.ReadAsync());
        Assert.Equal(1, priceReader.GetInt32(0));
    }

    private static async Task<string> InsertTestFabricAsync(string connectionString)
    {
        var code = (DateTime.UtcNow.Ticks % 1_000_000_000).ToString();
        var numericCode = int.Parse(code);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string inventoryItemSql = @"
                INSERT INTO dbo.InventoryItems
                    (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, FabricCategory, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
                VALUES (@code, N'قماش اختبار التسعير', N'Fabric', N'Yard', 100, 100, 0, 1, SYSUTCDATETIME(), N'Fabric', 36, N'Inch', 10, 360);";
            await using var inventoryItem = new SqlCommand(inventoryItemSql, connection, transaction);
            inventoryItem.Parameters.AddWithValue("@code", code);
            await inventoryItem.ExecuteNonQueryAsync();

            const string fabricInventorySql = @"
                INSERT INTO dbo.Fabrics_Inventory
                    (FabricCode, FabricName, Unit, Color, QuantityYard, QuantityInch, TotalRollCost, PricePerYard, PricePerInch, UsedQuantity, AvailableQuantity)
                VALUES (@code, N'قماش اختبار التسعير', N'ياردة', N'أبيض', 100, 3600, 36000, 360, 10, 0, 100);";
            await using var fabricInventory = new SqlCommand(fabricInventorySql, connection, transaction);
            fabricInventory.Parameters.AddWithValue("@code", numericCode);
            await fabricInventory.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return code;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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

    private static async Task<bool> HasCompleteOfficialCostMatrixAsync(string connectionString, int productTypeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            WITH active_matrix AS (
                SELECT TOP (1) CostMatrixId
                FROM dbo.PricingCostMatrices
                WHERE ProductTypeId=@productTypeId AND Channel=N'Tailoring' AND Status=N'Active' AND IsActive=1
                ORDER BY Version DESC, CostMatrixId DESC)
            SELECT CASE WHEN COUNT(DISTINCT ci.Code)=4 AND MIN(m.UnitCost)>0 THEN 1 ELSE 0 END
            FROM active_matrix am
            INNER JOIN dbo.PricingCostMatrixItems m ON m.CostMatrixId=am.CostMatrixId
            INNER JOIN dbo.PricingCostItems ci ON ci.CostItemId=m.CostItemId AND ci.IsActive=1
            WHERE ci.Code IN (N'OP_SEWING',N'OP_CONSUMABLES',N'OP_IRON_PACK',N'OP_FIXED');", connection);
        command.Parameters.AddWithValue("@productTypeId", productTypeId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }
}
