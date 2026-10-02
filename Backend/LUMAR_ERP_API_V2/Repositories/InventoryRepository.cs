using System.Globalization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class InventoryRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections, ILogger<InventoryRepository>? logger = null) : IInventoryRepository, IGoodsReceiptTransactionRuntime
{
    public Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken ct) => QueryAsync("SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice FROM dbo.InventoryItems ORDER BY ItemName, InventoryItemID", MapItem, null, ct);
    public async Task<InventoryItemDto?> GetItemByIdAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice FROM dbo.InventoryItems WHERE InventoryItemID = @id", MapItem, id, ct)).SingleOrDefault();
    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken ct) => QueryAsync("SELECT TransactionID, InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost FROM dbo.InventoryTransactions ORDER BY CreatedAt DESC, TransactionID DESC", reader => new InventoryTransactionDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.NullableString("ReferenceNumber"), reader.NullableString("Notes"), reader.GetDateTime(6), reader.NullableDecimal("TotalCostImpact"), reader.NullableDecimal("UnitCost")), null, ct);
    public Task<IReadOnlyList<InventoryWarehouseDto>> GetWarehousesAsync(CancellationToken ct) => QueryAsync("SELECT WarehouseId,WarehouseCode,WarehouseName,IsActive FROM dbo.Warehouses WHERE IsActive=1 ORDER BY WarehouseName,WarehouseId", reader => new InventoryWarehouseDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetBoolean(3)), null, ct);
    public Task<IReadOnlyList<GoodsReceiptDifferenceDto>> GetGoodsReceiptDifferencesAsync(int goodsReceiptId, CancellationToken ct) => QueryAsync("SELECT d.GoodsReceiptDifferenceId,d.GoodsReceiptItemId,d.DifferenceType,d.ExpectedQuantity,d.ActualQuantity,d.ExpectedUnitCost,d.ActualUnitCost FROM dbo.GoodsReceiptDifferences d INNER JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=d.GoodsReceiptItemId WHERE i.GoodsReceiptId=@id ORDER BY d.GoodsReceiptDifferenceId", reader => new GoodsReceiptDifferenceDto(reader.GetInt64(0),reader.GetInt32(1),reader.GetString(2),reader.NullableDecimal("ExpectedQuantity"),reader.NullableDecimal("ActualQuantity"),reader.NullableDecimal("ExpectedUnitCost"),reader.NullableDecimal("ActualUnitCost")), goodsReceiptId, ct);
    public async Task<IReadOnlyList<PendingGoodsReceiptStorageDto>> GetPendingGoodsReceiptStorageAsync(CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        var hasGoodsReceiptItemType = await ColumnExistsAsync(connection, null, "dbo.GoodsReceiptItems", "ItemType", ct);
        var hasSupplierInvoiceItemType = await ColumnExistsAsync(connection, null, "dbo.SupplierInvoiceLines", "ItemType", ct);
        var hasGoodsReceiptRollCount = await ColumnExistsAsync(connection, null, "dbo.GoodsReceiptItems", "RollCount", ct);

        var sql = BuildPendingGoodsReceiptStorageQuery(hasGoodsReceiptItemType, hasSupplierInvoiceItemType, hasGoodsReceiptRollCount);
        return await QueryAsync(sql, reader => new PendingGoodsReceiptStorageDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDateTime(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetDecimal(10),
            reader.GetString(11),
            reader.GetDecimal(12),
            reader.IsDBNull(13) ? null : reader.GetInt32(13),
            reader.IsDBNull(14) ? null : reader.GetInt64(14)), null, ct);
    }

    internal static string BuildPendingGoodsReceiptStorageQuery(bool hasGoodsReceiptItemType, bool hasSupplierInvoiceItemType, bool hasGoodsReceiptRollCount)
    {
        var itemTypeExpression = hasGoodsReceiptItemType && hasSupplierInvoiceItemType
            ? "COALESCE(i.ItemType, sil.ItemType)"
            : hasGoodsReceiptItemType
                ? "i.ItemType"
                : hasSupplierInvoiceItemType
                    ? "sil.ItemType"
                    : "CAST(N'Legacy' AS nvarchar(30))";

        var unitExpression = $"CASE {itemTypeExpression} WHEN N'Fabric' THEN N'ياردة' ELSE N'قطعة' END";
        var filterExpression = hasGoodsReceiptItemType || hasSupplierInvoiceItemType
            ? $"{itemTypeExpression} IN (N'Fabric',N'ImportedProduct',N'UsedTool')"
            : "1 = 0";
        var rollCountExpression = hasGoodsReceiptRollCount ? "i.RollCount" : "CAST(NULL AS int)";

        return $@"
        SELECT i.GoodsReceiptItemId,
               r.GoodsReceiptId,
               r.SupplierId,
               s.SupplierName,
               r.ReceiptNumber,
               r.ReceiptDate,
               {itemTypeExpression} AS ItemType,
               i.ItemName,
               i.ReceivedQuantity,
               COALESCE(a.StoredQuantity, 0) AS StoredQuantity,
               i.ReceivedQuantity - COALESCE(a.StoredQuantity, 0) AS RemainingQuantity,
               {unitExpression} AS Unit,
               i.UnitCost,
               {rollCountExpression} AS RollCount,
               i.SupplierInvoiceLineId
        FROM dbo.GoodsReceiptItems i
        INNER JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId
        INNER JOIN dbo.Suppliers s ON s.SupplierId=r.SupplierId
        LEFT JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId=i.SupplierInvoiceLineId
        OUTER APPLY
        (
            SELECT SUM(x.StoredQuantity) AS StoredQuantity
            FROM dbo.GoodsReceiptItemStorageAllocations x
            WHERE x.GoodsReceiptItemId=i.GoodsReceiptItemId
        ) a
        WHERE r.ReceiptStatus=N'Confirmed'
          AND ISNULL(i.LineStatus,N'Confirmed')=N'Confirmed'
          AND {filterExpression}
          AND i.ReceivedQuantity - COALESCE(a.StoredQuantity, 0) > 0
        ORDER BY r.ReceiptDate, i.GoodsReceiptItemId";
    }
    public Task<IReadOnlyList<InventoryWarehouseSummaryDto>> GetWarehouseSummariesAsync(CancellationToken ct) => QueryAsync(@"
        WITH FoundationValues AS
        (
            SELECT InventoryClassId, SUM(OperationalValue) AS CurrentValue
            FROM dbo.InventoryItemFoundation
            GROUP BY InventoryClassId
        ),
        OfficialReceiptValues AS
        (
            SELECT rp.InventoryClassId, SUM(rl.OperationalAmount) AS InputValue
            FROM dbo.InventoryReceiptPostings rp
            INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryReceiptPostingId = rp.InventoryReceiptPostingId
            GROUP BY rp.InventoryClassId
        ),
        LegacyInventoryItems AS
        (
            SELECT i.InventoryItemID,
                   i.IsActive,
                   i.CurrentQuantity,
                   i.Category,
                   i.ItemName,
                   i.FabricCategory,
                   i.Unit,
                   i.InchPrice,
                   i.YardPrice
            FROM dbo.InventoryItems i
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.InventoryItemFoundation f
                WHERE f.InventoryItemId = i.InventoryItemID
            )
        ),
        LegacyFabricItems AS
        (
            SELECT InventoryItemID,
                   IsActive,
                   CurrentQuantity,
                   CASE
                       WHEN Unit LIKE N'%Inch%' OR Unit LIKE N'%بوص%' THEN COALESCE(InchPrice, YardPrice / 36.0)
                       WHEN Unit LIKE N'%Yard%' OR Unit LIKE N'%يارد%' THEN COALESCE(YardPrice, InchPrice * 36.0)
                       ELSE COALESCE(YardPrice, InchPrice)
                   END AS UnitCost
            FROM LegacyInventoryItems
            WHERE Category = N'Fabric'
               OR ItemName LIKE N'%fabric%'
               OR ItemName LIKE N'%cloth%'
               OR ItemName LIKE N'%textile%'
               OR ItemName LIKE N'%قماش%'
               OR ItemName LIKE N'%نسيج%'
               OR ItemName LIKE N'%بوليستر%'
               OR ItemName LIKE N'%هندي%'
               OR ItemName LIKE N'%انجليزي%'
               OR ItemName LIKE N'%قطن%'
               OR FabricCategory LIKE N'%fabric%'
               OR FabricCategory LIKE N'%cloth%'
               OR FabricCategory LIKE N'%textile%'
               OR FabricCategory LIKE N'%قماش%'
               OR FabricCategory LIKE N'%نسيج%'
               OR FabricCategory LIKE N'%بوليستر%'
               OR FabricCategory LIKE N'%هندي%'
               OR FabricCategory LIKE N'%انجليزي%'
               OR FabricCategory LIKE N'%قطن%'
               OR Category LIKE N'%fabric%'
               OR Category LIKE N'%cloth%'
               OR Category LIKE N'%textile%'
               OR Category LIKE N'%قماش%'
               OR Category LIKE N'%نسيج%'
               OR Category LIKE N'%بوليستر%'
               OR Category LIKE N'%هندي%'
               OR Category LIKE N'%انجليزي%'
               OR Category LIKE N'%قطن%'
        ),
        LegacyToolItems AS
        (
            SELECT InventoryItemID,
                   IsActive,
                   CurrentQuantity,
                   COALESCE(YardPrice, InchPrice) AS UnitCost
            FROM LegacyInventoryItems
            WHERE Category LIKE N'%Tool%'
               OR Category LIKE N'%Accessory%'
               OR Category LIKE N'%Thread%'
               OR Category LIKE N'%Button%'
               OR Category LIKE N'%Packing%'
               OR Category LIKE N'%Glue%'
               OR Category LIKE N'%Sewing%'
               OR Category LIKE N'%Needle%'
               OR Category LIKE N'%Machine%'
               OR Category LIKE N'%Equipment%'
               OR ItemName LIKE N'%خيط%'
               OR ItemName LIKE N'%زر%'
               OR ItemName LIKE N'%سحاب%'
               OR ItemName LIKE N'%لاصق%'
               OR ItemName LIKE N'%تغليف%'
               OR ItemName LIKE N'%أداة%'
               OR ItemName LIKE N'%اداة%'
               OR ItemName LIKE N'%مستلزم%'
               OR Category LIKE N'%أداة%'
               OR Category LIKE N'%اداة%'
               OR Category LIKE N'%مستلزم%'
        ),
        LegacyFabricInputs AS
        (
            SELECT COALESCE(SUM(COALESCE(NULLIF(t.OperationalCostImpact, 0), NULLIF(t.TotalCostImpact, 0), t.Quantity * t.UnitCost)), 0) AS InputValue
            FROM dbo.InventoryTransactions t
            INNER JOIN LegacyFabricItems i ON i.InventoryItemID = t.InventoryItemID
            WHERE t.TransactionType IN (N'InitialBalance', N'Receive')
              AND t.Quantity > 0
              AND t.ReadyMadeInventoryProductId IS NULL
              AND t.ImportedReadyMadeInventoryReceiptId IS NULL
              AND t.ImportedReadyMadeSaleCostPostingId IS NULL
        ),
        LegacyFabricCurrent AS
        (
            SELECT COALESCE(SUM(CASE WHEN IsActive = 1 THEN CurrentQuantity * COALESCE(UnitCost, 0) ELSE 0 END), 0) AS CurrentValue
            FROM LegacyFabricItems
        ),
        LegacyToolInputs AS
        (
            SELECT COALESCE(SUM(COALESCE(NULLIF(t.OperationalCostImpact, 0), NULLIF(t.TotalCostImpact, 0), t.Quantity * t.UnitCost)), 0) AS InputValue
            FROM dbo.InventoryTransactions t
            INNER JOIN LegacyToolItems i ON i.InventoryItemID = t.InventoryItemID
            WHERE t.TransactionType IN (N'InitialBalance', N'Receive', N'Renewal')
              AND t.Quantity > 0
              AND t.ReadyMadeInventoryProductId IS NULL
              AND t.ImportedReadyMadeInventoryReceiptId IS NULL
              AND t.ImportedReadyMadeSaleCostPostingId IS NULL
        ),
        LegacyToolCurrent AS
        (
            SELECT COALESCE(SUM(CASE WHEN IsActive = 1 THEN CurrentQuantity * COALESCE(UnitCost, 0) ELSE 0 END), 0) AS CurrentValue
            FROM LegacyToolItems
        ),
        ReadyProductCosts AS
        (
            SELECT p.IsActive,
                   p.Status,
                   COALESCE(p.ActualCost, receiptCost.Cost, 0) AS Cost
            FROM dbo.ReadyMadeInventoryProducts p
            OUTER APPLY
            (
                SELECT TOP (1)
                       COALESCE(NULLIF(t.OperationalCostImpact, 0), NULLIF(t.TotalCostImpact, 0), t.Quantity * t.UnitCost) AS Cost
                FROM dbo.InventoryTransactions t
                WHERE t.ReadyMadeInventoryProductId = p.ReadyMadeInventoryProductId
                  AND t.TransactionType = N'Receive'
                ORDER BY t.TransactionID
            ) receiptCost
        ),
        ImportedReceiptValues AS
        (
            SELECT ImportedReadyMadeProductId, SUM(OperationalAmount) AS InputValue
            FROM dbo.ImportedReadyMadeInventoryReceipts
            GROUP BY ImportedReadyMadeProductId
        ),
        ImportedSaleValues AS
        (
            SELECT ImportedReadyMadeProductId, SUM(OperationalAmount) AS SaleCostValue
            FROM dbo.ImportedReadyMadeSaleCostPostings
            GROUP BY ImportedReadyMadeProductId
        ),
        ImportedProductCosts AS
        (
            SELECT p.IsActive,
                   CASE WHEN r.ImportedReadyMadeProductId IS NULL THEN p.Quantity * p.PurchasePrice ELSE r.InputValue END AS InputValue,
                   CASE
                       WHEN r.ImportedReadyMadeProductId IS NULL THEN
                           CASE WHEN p.IsActive = 1 AND p.Quantity > 0 THEN p.Quantity * p.PurchasePrice ELSE 0 END
                       ELSE
                           CASE WHEN r.InputValue - COALESCE(s.SaleCostValue, 0) > 0 THEN r.InputValue - COALESCE(s.SaleCostValue, 0) ELSE 0 END
                   END AS CurrentValue
            FROM dbo.ImportedReadyMadeProducts p
            LEFT JOIN ImportedReceiptValues r ON r.ImportedReadyMadeProductId = p.ImportedReadyMadeProductId
            LEFT JOIN ImportedSaleValues s ON s.ImportedReadyMadeProductId = p.ImportedReadyMadeProductId
        )
        SELECT N'fabric' AS WarehouseKey,
               COALESCE((SELECT InputValue FROM OfficialReceiptValues WHERE InventoryClassId = 1), 0) + (SELECT InputValue FROM LegacyFabricInputs) AS TotalInputValue,
               COALESCE((SELECT CurrentValue FROM FoundationValues WHERE InventoryClassId = 1), 0) + (SELECT CurrentValue FROM LegacyFabricCurrent) AS CurrentInventoryValue
        UNION ALL
        SELECT N'readyMade',
               COALESCE((SELECT SUM(Cost) FROM ReadyProductCosts), 0),
               COALESCE((SELECT SUM(CASE WHEN IsActive = 1 AND Status <> N'Sold' THEN Cost ELSE 0 END) FROM ReadyProductCosts), 0)
        UNION ALL
        SELECT N'imported',
               COALESCE((SELECT SUM(InputValue) FROM ImportedProductCosts), 0),
               COALESCE((SELECT SUM(CurrentValue) FROM ImportedProductCosts), 0)
        UNION ALL
        SELECT N'tools',
               COALESCE((SELECT InputValue FROM OfficialReceiptValues WHERE InventoryClassId = 2), 0) + (SELECT InputValue FROM LegacyToolInputs),
               COALESCE((SELECT CurrentValue FROM FoundationValues WHERE InventoryClassId = 2), 0) + (SELECT CurrentValue FROM LegacyToolCurrent);", reader => new InventoryWarehouseSummaryDto(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2)), null, ct);
    public async Task<IReadOnlyList<FabricDto>> GetFabricsAsync(CancellationToken ct)
    {
        const string sql = @"
            SELECT 'Fabrics' AS SourceTable,
                   FabricID,
                   FabricCode,
                   FabricName,
                   FabricPrice,
                   IsActive,
                   CAST(NULL AS int) AS InventoryFabricCode,
                   CAST(NULL AS nvarchar(100)) AS InventoryFabricName,
                   CAST(NULL AS nvarchar(20)) AS Unit,
                   CAST(NULL AS nvarchar(50)) AS Color,
                   CAST(NULL AS nvarchar(100)) AS CatalogNumber,
                   CAST(NULL AS decimal(10,2)) AS QuantityYard,
                   CAST(NULL AS decimal(10,2)) AS QuantityInch,
                   CAST(NULL AS decimal(18,2)) AS TotalRollCost,
                   CAST(NULL AS decimal(18,2)) AS PricePerYard,
                   CAST(NULL AS decimal(18,2)) AS PricePerInch,
                   CAST(NULL AS decimal(10,2)) AS UsedQuantity,
                   CAST(NULL AS decimal(10,2)) AS AvailableQuantity
            FROM dbo.Fabrics

            UNION ALL

            SELECT 'Fabrics_Inventory' AS SourceTable,
                   NULL AS FabricID,
                   CAST(FabricCode AS nvarchar(50)) AS FabricCode,
                   FabricName,
                   NULL AS FabricPrice,
                   NULL AS IsActive,
                   FabricCode AS InventoryFabricCode,
                   FabricName AS InventoryFabricName,
                   Unit,
                   Color,
                   CAST(NULL AS nvarchar(100)) AS CatalogNumber,
                   QuantityYard,
                   QuantityInch,
                   TotalRollCost,
                   PricePerYard,
                   PricePerInch,
                   UsedQuantity,
                   AvailableQuantity
            FROM dbo.Fabrics_Inventory

            UNION ALL

            SELECT 'InventoryItems' AS SourceTable,
                   NULL AS FabricID,
                   ItemCode AS FabricCode,
                   ItemName AS FabricName,
                   NULL AS FabricPrice,
                   NULL AS IsActive,
                   InventoryItemID AS InventoryFabricCode,
                   ItemName AS InventoryFabricName,
                   Unit,
                   FabricColor AS Color,
                   Barcode AS CatalogNumber,
                   CurrentQuantity AS QuantityYard,
                   CurrentQuantity * 36 AS QuantityInch,
                   NULL AS TotalRollCost,
                   YardPrice AS PricePerYard,
                   CASE WHEN YardPrice IS NOT NULL THEN YardPrice / 36 ELSE NULL END AS PricePerInch,
                   0 AS UsedQuantity,
                   AvailableQuantity
            FROM dbo.InventoryItems i
            WHERE EXISTS (
                SELECT 1
                FROM dbo.InventoryItemFoundation f
                WHERE f.InventoryItemId = i.InventoryItemID
                  AND f.InventoryClassId = 1
            )
            ORDER BY FabricName";

        return await QueryAsync(sql, reader => new FabricDto(reader.GetString(0), reader.NullableInt32("FabricID"), reader.NullableString("FabricCode"), reader.NullableString("FabricName"), reader.NullableDecimal("FabricPrice"), reader.NullableBoolean("IsActive"), reader.NullableInt32("InventoryFabricCode"), reader.NullableString("InventoryFabricName"), reader.NullableString("Unit"), reader.NullableString("Color"), reader.NullableString("CatalogNumber"), reader.NullableDecimal("QuantityYard"), reader.NullableDecimal("QuantityInch"), reader.NullableDecimal("TotalRollCost"), reader.NullableDecimal("PricePerYard"), reader.NullableDecimal("PricePerInch"), reader.NullableDecimal("UsedQuantity"), reader.NullableDecimal("AvailableQuantity")), null, ct);
    }
    public Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMadeAsync(CancellationToken ct) => QueryAsync("SELECT r.ReadyMadeInventoryProductId, r.ReadyMadeProductionOrderId, r.ReadyMadeProductionOrderItemId, r.ReadyMadeProductionOrderPieceInstanceId, r.ProductTypeId, r.ProductionOrderNumber, r.ProductionName, r.PieceType, r.PieceNumber, r.TrackingCode, r.FabricCode, r.FabricType, r.FabricColor, r.CatalogNumber, r.FabricUnit, r.FabricWidth, r.FabricWidthUnit, r.ActualCost, r.SuggestedSellingPrice, r.MeasurementSnapshot, r.ReadyForSaleAt, r.Status, r.Source, r.Notes, r.IsActive, r.CreatedAt, pt.NameAr AS ProductTypeName FROM dbo.ReadyMadeInventoryProducts r LEFT JOIN dbo.PricingProductTypes pt ON pt.ProductTypeId=r.ProductTypeId ORDER BY r.ReadyForSaleAt DESC, r.ReadyMadeInventoryProductId DESC", MapReadyMade, null, ct);
    public async Task<ReadyMadeProductDto?> GetReadyMadeByIdAsync(int readyMadeInventoryProductId, CancellationToken ct) => (await QueryAsync("SELECT r.ReadyMadeInventoryProductId, r.ReadyMadeProductionOrderId, r.ReadyMadeProductionOrderItemId, r.ReadyMadeProductionOrderPieceInstanceId, r.ProductTypeId, r.ProductionOrderNumber, r.ProductionName, r.PieceType, r.PieceNumber, r.TrackingCode, r.FabricCode, r.FabricType, r.FabricColor, r.CatalogNumber, r.FabricUnit, r.FabricWidth, r.FabricWidthUnit, r.ActualCost, r.SuggestedSellingPrice, r.MeasurementSnapshot, r.ReadyForSaleAt, r.Status, r.Source, r.Notes, r.IsActive, r.CreatedAt, pt.NameAr AS ProductTypeName FROM dbo.ReadyMadeInventoryProducts r LEFT JOIN dbo.PricingProductTypes pt ON pt.ProductTypeId=r.ProductTypeId WHERE r.ReadyMadeInventoryProductId = @id", MapReadyMade, readyMadeInventoryProductId, ct)).SingleOrDefault();
    private static ReadyMadeProductDto MapReadyMade(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.NullableInt32("ProductTypeId"), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetString(9), reader.NullableString("FabricCode"), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.NullableString("CatalogNumber"), reader.NullableString("FabricUnit"), reader.NullableString("FabricWidth"), reader.NullableString("FabricWidthUnit"), reader.NullableDecimal("ActualCost"), reader.NullableDecimal("SuggestedSellingPrice"), reader.NullableString("MeasurementSnapshot"), reader.GetDateTime(20), reader.GetString(21), reader.GetString(22), reader.NullableString("Notes"), reader.GetBoolean(24), reader.GetDateTime(25), reader.NullableString("ProductTypeName"));
    public Task<ReadyMadeProductDto?> RecordReadyMadeSaleCostAsync(int readyMadeInventoryProductId, CancellationToken ct) =>
        Task.FromException<ReadyMadeProductDto?>(new InvalidOperationException("هذه العملية غير متاحة حتى اكتمال عقد الربط المحاسبي."));

    private async Task<ReadyMadeProductDto?> RecordReadyMadeSaleCostLegacyAsync(int readyMadeInventoryProductId, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            const string selectSql = "SELECT ReadyMadeInventoryProductId, ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadeProductionOrderPieceInstanceId, ProductTypeId, ProductionOrderNumber, ProductionName, PieceType, PieceNumber, TrackingCode, FabricCode, FabricType, FabricColor, CatalogNumber, FabricUnit, FabricWidth, FabricWidthUnit, ActualCost, SuggestedSellingPrice, MeasurementSnapshot, ReadyForSaleAt, Status, Source, Notes, IsActive, CreatedAt FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK,HOLDLOCK) WHERE ReadyMadeInventoryProductId = @id";
            await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
            selectCommand.Parameters.AddWithValue("@id", readyMadeInventoryProductId);
            await using var reader = await selectCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var readyMadeProductionOrderId = reader.GetInt32(1);
            var readyMadeProductionOrderItemId = reader.GetInt32(2);
            var productStatus = reader.GetString(21);
            var actualCost = reader.IsDBNull(17) ? 0m : reader.GetDecimal(17);

            await reader.DisposeAsync();

            if (!string.Equals(productStatus, "Sold", StringComparison.OrdinalIgnoreCase) || actualCost <= 0m)
            {
                await transaction.CommitAsync(ct);
                return await GetReadyMadeByIdAsync(readyMadeInventoryProductId, ct);
            }

            var reference = $"Order:{readyMadeProductionOrderId}:Line:{readyMadeProductionOrderItemId}:Product:{readyMadeInventoryProductId}:ReadyMadeCost";
            const string financialSql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) SELECT @reference,@transactionType,@amount,@description,@createdAt WHERE NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @reference AND TransactionType = N'ReadyMadeCost')";
            await using (var financial = new SqlCommand(financialSql, connection, transaction))
            {
                financial.Parameters.AddWithValue("@reference", reference);
                financial.Parameters.AddWithValue("@transactionType", "ReadyMadeCost");
                financial.Parameters.AddWithValue("@amount", actualCost);
                financial.Parameters.AddWithValue("@description", $"Ready-made sale cost for product {readyMadeInventoryProductId}");
                financial.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                await financial.ExecuteNonQueryAsync(ct);
            }

            await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection,
                transaction,
                reference,
                "ReadyMadeCost",
                actualCost,
                $"Ready-made sale cost for product {readyMadeInventoryProductId}",
                ct);

            await transaction.CommitAsync(ct);
            return await GetReadyMadeByIdAsync(readyMadeInventoryProductId, ct);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore rollback failures after the original exception is already active.
            }

            throw;
        }
    }
    public Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImportedAsync(CancellationToken ct) => QueryAsync("SELECT ImportedReadyMadeProductId, ProductName, ProductType, ProductCode, Unit, Quantity, PurchasePrice, SellingPrice, IsActive, AlertThreshold, Notes, Category, CreatedAt, UpdatedAt FROM dbo.ImportedReadyMadeProducts ORDER BY ProductName, ImportedReadyMadeProductId", reader => new ImportedReadyMadeProductDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetBoolean(8), reader.NullableDecimal("AlertThreshold"), reader.NullableString("Notes"), reader.GetString(11), reader.GetDateTime(12), reader.NullableDateTime("UpdatedAt")), null, ct);
    public Task<IReadOnlyList<InventoryItemDto>> GetToolsAsync(CancellationToken ct) => QueryAsync("SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice FROM dbo.InventoryItems WHERE Category LIKE '%Tool%' OR Category LIKE '%Accessory%' OR Category LIKE '%Thread%' OR Category LIKE '%Button%' OR Category LIKE '%Packing%' OR Category LIKE '%Glue%' OR ItemName LIKE '%خيط%' OR ItemName LIKE '%زر%' OR ItemName LIKE '%سحاب%' OR ItemName LIKE '%لاصق%' OR ItemName LIKE '%تغليف%' OR ItemName LIKE '%أداة%' OR ItemName LIKE '%مستلزم%' ORDER BY ItemName, InventoryItemID", MapItem, null, ct);

    public async Task<InventoryItemDto?> UpsertToolItemAsync(CreateToolItemDto tool, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var now = DateTime.UtcNow;
            var productName = tool.ProductName.Trim();
            var productType = string.IsNullOrWhiteSpace(tool.ProductType) ? "أداة" : tool.ProductType.Trim();
            var unit = tool.Unit.Trim();
            var quantity = tool.Quantity;
            var unitPrice = tool.UnitPrice;
            var supplierId = tool.SupplierId;
            var invoiceNumber = string.IsNullOrWhiteSpace(tool.InvoiceNumber) ? "N/A" : tool.InvoiceNumber.Trim();
            var notes = string.IsNullOrWhiteSpace(tool.Notes) ? null : tool.Notes.Trim();

            var candidateCode = string.IsNullOrWhiteSpace(tool.ProductCode) ? await GetNextToolCodeAsync(connection, transaction, ct) : tool.ProductCode.Trim();
            var existing = await FindExistingToolAsync(connection, transaction, candidateCode, productName, productType, ct);

            if (existing is not null && tool.RenewExisting)
            {
                var updatedQuantity = existing.CurrentQuantity + quantity;
                var updatedAvailable = existing.AvailableQuantity + quantity;
                using var updateCmd = new SqlCommand(@"
                    UPDATE dbo.InventoryItems
                    SET ItemName = @name,
                        Category = @type,
                        Unit = @unit,
                        CurrentQuantity = @qty,
                        AvailableQuantity = @available,
                        UpdatedAt = @now,
                        Barcode = ISNULL(@invoice, Barcode),
                        ItemCode = @code,
                        IsActive = 1
                    WHERE InventoryItemID = @id", connection, transaction);
                updateCmd.Parameters.AddWithValue("@id", existing.InventoryItemId);
                updateCmd.Parameters.AddWithValue("@name", productName);
                updateCmd.Parameters.AddWithValue("@type", productType);
                updateCmd.Parameters.AddWithValue("@unit", unit);
                updateCmd.Parameters.AddWithValue("@qty", updatedQuantity);
                updateCmd.Parameters.AddWithValue("@available", updatedAvailable);
                updateCmd.Parameters.AddWithValue("@now", now);
                updateCmd.Parameters.AddWithValue("@invoice", invoiceNumber);
                updateCmd.Parameters.AddWithValue("@code", candidateCode);
                await updateCmd.ExecuteNonQueryAsync(ct);

                var renewalTransactionId = await InsertToolTransactionAsync(connection, transaction, existing.InventoryItemId, candidateCode, productName, quantity, unit, unitPrice, supplierId, invoiceNumber, notes, now, "Renewal", null, null, ct);
                await InsertStorageAllocationAsync(connection, transaction, tool.GoodsReceiptItemId, "UsedTool", quantity, tool.StorageOperationId, existing.InventoryItemId, renewalTransactionId, null, null, null, ct);
                await transaction.CommitAsync(ct);
                return await GetItemByIdAsync(existing.InventoryItemId, ct);
            }

            if (existing is not null && !tool.RenewExisting)
            {
                throw new InvalidOperationException($"الأداة '{productName}' موجودة بالفعل في المخزون.");
            }

            var itemId = await InsertToolInventoryItemAsync(connection, transaction, candidateCode, productName, productType, unit, quantity, unitPrice, supplierId, invoiceNumber, notes, now, ct);
            var receiveTransactionId = await InsertToolTransactionAsync(connection, transaction, itemId, candidateCode, productName, quantity, unit, unitPrice, supplierId, invoiceNumber, notes, now, "Receive", tool.GoodsReceiptItemId, tool.StorageOperationId, ct);
            await InsertStorageAllocationAsync(connection, transaction, tool.GoodsReceiptItemId, "UsedTool", quantity, tool.StorageOperationId, itemId, receiveTransactionId, null, null, null, ct);
            await transaction.CommitAsync(ct);
            return await GetItemByIdAsync(itemId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<string> GetNextToolCodeAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "ToolCodePrefix", "AT", ct);
        using var cmd = new SqlCommand(@"
            SELECT TOP 1 ItemCode
            FROM dbo.InventoryItems WITH (NOLOCK)
            WHERE ItemCode LIKE @prefix
            ORDER BY ItemCode DESC", connection, transaction);
        cmd.Parameters.AddWithValue("@prefix", $"{prefix}%");
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var maxNumber = 0;
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(0);
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(code[prefix.Length..], out var number))
            {
                if (number > maxNumber) maxNumber = number;
            }
        }

        return $"{prefix}{(maxNumber + 1).ToString().PadLeft(4, '0')}";
    }

    private static async Task<InventoryItemDto?> FindExistingToolAsync(SqlConnection connection, SqlTransaction transaction, string candidateCode, string productName, string productType, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice
            FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK)
            WHERE ItemCode = @code OR (ItemName = @name AND Category = @type)
            ORDER BY InventoryItemID DESC", connection, transaction);
        cmd.Parameters.AddWithValue("@code", candidateCode);
        cmd.Parameters.AddWithValue("@name", productName);
        cmd.Parameters.AddWithValue("@type", productType);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapItem(reader);
    }

    private static async Task<int> InsertToolInventoryItemAsync(SqlConnection connection, SqlTransaction transaction, string candidateCode, string productName, string productType, string unit, decimal quantity, decimal unitPrice, int? supplierId, string invoiceNumber, string? notes, DateTime now, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryItems
                (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
            OUTPUT INSERTED.InventoryItemID
            VALUES
                (@code, @name, @type, @unit, @qty, @qty, 0, 1, @now, @now, @invoice, @type, NULL, 0, N'Piece', @unitPrice, @unitPrice)", connection, transaction);
        cmd.Parameters.AddWithValue("@code", candidateCode);
        cmd.Parameters.AddWithValue("@name", productName);
        cmd.Parameters.AddWithValue("@type", productType);
        cmd.Parameters.AddWithValue("@unit", unit);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@now", now);
        AddNullable(cmd, "@invoice", invoiceNumber == "N/A" ? null : invoiceNumber);
        cmd.Parameters.AddWithValue("@unitPrice", unitPrice);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is int itemId ? itemId : throw new InvalidOperationException("تعذر إنشاء سجل أداة جديد.");
    }

    private static async Task<int> InsertToolTransactionAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string code, string productName, decimal quantity, string unit, decimal unitPrice, int? supplierId, string invoiceNumber, string? notes, DateTime now, string operationType, int? goodsReceiptItemId, Guid? storageOperationId, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
            OUTPUT INSERTED.TransactionID
            VALUES
                (@itemId, @operationType, @qty, @ref, @notes, @now, @cost, @unitCost)", connection, transaction);
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@operationType", operationType == "Receive" ? "Receive" : "Renewal");
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@ref", $"TOOL-{code}-{invoiceNumber}-{now:yyyyMMddHHmmss}");
        AddNullable(cmd, "@notes", $"SupplierId:{supplierId ?? 0} | Invoice:{invoiceNumber} | {notes ?? $"{productName} ({operationType})"}");
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@cost", quantity * unitPrice);
        cmd.Parameters.AddWithValue("@unitCost", unitPrice);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null ? throw new InvalidOperationException("تعذر إنشاء حركة الأداة.") : Convert.ToInt32(value);
    }

    public async Task<ImportedReadyMadeProductDto?> UpsertImportedProductAsync(CreateImportedProductDto product, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var now = DateTime.UtcNow;
            var productName = product.ProductName.Trim();
            var productType = string.IsNullOrWhiteSpace(product.ProductType) ? "غير محدد" : product.ProductType.Trim();
            var requestedCode = string.IsNullOrWhiteSpace(product.ProductCode) ? null : product.ProductCode.Trim();
            var productCode = requestedCode ?? await GetNextImportedProductCodeAsync(connection, transaction, ct);
            var unit = product.Unit.Trim();
            var category = string.IsNullOrWhiteSpace(product.Category) ? "Imported" : product.Category.Trim();
            var notes = string.IsNullOrWhiteSpace(product.Notes) ? null : product.Notes.Trim();

            var existing = await GetImportedProductForUpdateAsync(connection, transaction, productCode, ct);
            if (existing is not null && product.RenewExisting)
            {
                var updatedQuantity = existing.Quantity + product.Quantity;
                var updatedSellingPrice = product.SellingPrice > 0 ? product.SellingPrice : existing.SellingPrice;
                var updatedPurchasePrice = product.PurchasePrice > 0 ? product.PurchasePrice : existing.PurchasePrice;

                using var updateCmd = new SqlCommand(@"
                    UPDATE dbo.ImportedReadyMadeProducts
                    SET ProductName = @name,
                        ProductType = @type,
                        Unit = @unit,
                        Quantity = @quantity,
                        PurchasePrice = @purchasePrice,
                        SellingPrice = @sellingPrice,
                        Notes = @notes,
                        Category = @category,
                        IsActive = 1,
                        UpdatedAt = @now
                    WHERE ImportedReadyMadeProductId = @id", connection, transaction);
                updateCmd.Parameters.AddWithValue("@id", existing.ImportedReadyMadeProductId);
                updateCmd.Parameters.AddWithValue("@name", productName);
                updateCmd.Parameters.AddWithValue("@type", productType);
                updateCmd.Parameters.AddWithValue("@unit", unit);
                updateCmd.Parameters.AddWithValue("@quantity", updatedQuantity);
                updateCmd.Parameters.AddWithValue("@purchasePrice", updatedPurchasePrice);
                updateCmd.Parameters.AddWithValue("@sellingPrice", updatedSellingPrice);
                AddNullable(updateCmd, "@notes", notes);
                updateCmd.Parameters.AddWithValue("@category", category);
                updateCmd.Parameters.AddWithValue("@now", now);
                await updateCmd.ExecuteNonQueryAsync(ct);

                await InsertImportedInventoryReceiptAsync(
                    connection,
                    transaction,
                    existing.ImportedReadyMadeProductId,
                    existing.ProductCode,
                    productName,
                    unit,
                    product.Quantity,
                    updatedPurchasePrice,
                    product.SupplierId,
                    notes,
                    product.GoodsReceiptItemId,
                    product.StorageOperationId,
                    now,
                    ct);

                await transaction.CommitAsync(ct);
                return new ImportedReadyMadeProductDto(existing.ImportedReadyMadeProductId, productName, productType, existing.ProductCode, unit, updatedQuantity, updatedPurchasePrice, updatedSellingPrice, true, existing.AlertThreshold, notes, category, existing.CreatedAt, now);
            }

            if (existing is not null && !product.RenewExisting)
            {
                throw new InvalidOperationException($"المنتج '{productCode}' موجود بالفعل في المخزون المستورد.");
            }

            var result = await InsertImportedProductAsync(connection, transaction, productName, productType, productCode, unit, product.Quantity, product.PurchasePrice, product.SellingPrice, notes, category, now, ct);
            await InsertImportedInventoryReceiptAsync(
                connection,
                transaction,
                result.ImportedReadyMadeProductId,
                result.ProductCode,
                result.ProductName,
                result.Unit,
                product.Quantity,
                product.PurchasePrice,
                product.SupplierId,
                notes,
                product.GoodsReceiptItemId,
                product.StorageOperationId,
                now,
                ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<string> GetNextImportedProductCodeAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolveImportedProductCodePrefixAsync(connection, transaction, ct);
        using var cmd = new SqlCommand(@"
            SELECT TOP 1 ProductCode
            FROM dbo.ImportedReadyMadeProducts WITH (NOLOCK)
            WHERE ProductCode LIKE @prefix
            ORDER BY ProductCode DESC", connection, transaction);
        cmd.Parameters.AddWithValue("@prefix", $"{prefix}%");
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var maxNumber = 0;
        while (await reader.ReadAsync(ct))
        {
            var code = reader.GetString(0);
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(code[prefix.Length..], out var number))
            {
                if (number > maxNumber) maxNumber = number;
            }
        }

        return $"{prefix}{(maxNumber + 1).ToString().PadLeft(4, '0')}";
    }

    private static async Task<ImportedReadyMadeProductDto?> GetImportedProductForUpdateAsync(SqlConnection connection, SqlTransaction transaction, string productCode, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            SELECT ImportedReadyMadeProductId, ProductName, ProductType, ProductCode, Unit, Quantity, PurchasePrice, SellingPrice, IsActive, AlertThreshold, Notes, Category, CreatedAt
            FROM dbo.ImportedReadyMadeProducts WITH (UPDLOCK, HOLDLOCK)
            WHERE ProductCode = @code", connection, transaction);
        cmd.Parameters.AddWithValue("@code", productCode);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new ImportedReadyMadeProductDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetBoolean(8),
            reader.NullableDecimal("AlertThreshold"),
            reader.NullableString("Notes"),
            reader.GetString(11),
            reader.GetDateTime(12),
            reader.NullableDateTime("UpdatedAt")
        );
    }

    private static async Task<ImportedReadyMadeProductDto> InsertImportedProductAsync(SqlConnection connection, SqlTransaction transaction, string productName, string productType, string productCode, string unit, decimal quantity, decimal purchasePrice, decimal sellingPrice, string? notes, string category, DateTime now, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.ImportedReadyMadeProducts
                (ProductName, ProductType, ProductCode, Unit, Quantity, PurchasePrice, SellingPrice, IsActive, AlertThreshold, Notes, Category, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.ImportedReadyMadeProductId, INSERTED.ProductName, INSERTED.ProductType, INSERTED.ProductCode, INSERTED.Unit, INSERTED.Quantity, INSERTED.PurchasePrice, INSERTED.SellingPrice, INSERTED.IsActive, INSERTED.AlertThreshold, INSERTED.Notes, INSERTED.Category, INSERTED.CreatedAt, INSERTED.UpdatedAt
            VALUES
                (@name, @type, @code, @unit, @quantity, @purchasePrice, @sellingPrice, 1, NULL, @notes, @category, @now, @now)", connection, transaction);
        cmd.Parameters.AddWithValue("@name", productName);
        cmd.Parameters.AddWithValue("@type", productType);
        cmd.Parameters.AddWithValue("@code", productCode);
        cmd.Parameters.AddWithValue("@unit", unit);
        cmd.Parameters.AddWithValue("@quantity", quantity);
        cmd.Parameters.AddWithValue("@purchasePrice", purchasePrice);
        cmd.Parameters.AddWithValue("@sellingPrice", sellingPrice);
        AddNullable(cmd, "@notes", notes);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@now", now);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("تعذر حفظ المنتج المستورد.");
        }

        var productId = reader.GetInt32(0);

        return new ImportedReadyMadeProductDto(
            productId,
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetBoolean(8),
            reader.NullableDecimal("AlertThreshold"),
            reader.NullableString("Notes"),
            reader.GetString(11),
            reader.GetDateTime(12),
            reader.NullableDateTime("UpdatedAt")
        );
    }

    private static async Task InsertImportedInventoryReceiptAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int importedReadyMadeProductId,
        string productCode,
        string productName,
        string unit,
        decimal quantity,
        decimal purchasePrice,
        int? supplierId,
        string? notes,
        int? goodsReceiptItemId,
        Guid? storageOperationId,
        DateTime now,
        CancellationToken ct)
    {
        if (quantity <= 0m || purchasePrice <= 0m)
            throw new InvalidOperationException("كمية أو تكلفة الاستلام المستورد غير صالحة.");

        const string accountSql = "SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountCode = N'2100' AND IsActive = 1 ORDER BY LedgerAccountId";
        await using var accountCommand = new SqlCommand(accountSql, connection, transaction);
        var accountValue = await accountCommand.ExecuteScalarAsync(ct);
        if (accountValue is not int opposingLedgerAccountId)
            throw new InvalidOperationException("حساب الموردين الرسمي غير موجود أو غير فعال.");

        var operationalAmount = quantity * purchasePrice;
        var postingAmount = decimal.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
        var sourceOperationId = Guid.NewGuid();
        const string sourceSql = @"
            INSERT INTO dbo.ImportedReadyMadeInventoryReceipts
                (ImportedReadyMadeProductId, QuantityReceived, OfficialUnitCost, OperationalAmount, PostingAmount,
                 OpposingLedgerAccountId, SourceOperationId)
            OUTPUT INSERTED.ImportedReadyMadeInventoryReceiptId
            VALUES
                (@productId, @quantity, @unitCost, @operationalAmount, @postingAmount,
                 @opposingLedgerAccountId, @sourceOperationId);";
        await using var sourceCommand = new SqlCommand(sourceSql, connection, transaction);
        sourceCommand.Parameters.AddWithValue("@productId", importedReadyMadeProductId);
        sourceCommand.Parameters.AddWithValue("@quantity", quantity);
        sourceCommand.Parameters.AddWithValue("@unitCost", purchasePrice);
        sourceCommand.Parameters.AddWithValue("@operationalAmount", operationalAmount);
        sourceCommand.Parameters.AddWithValue("@postingAmount", postingAmount);
        sourceCommand.Parameters.AddWithValue("@opposingLedgerAccountId", opposingLedgerAccountId);
        sourceCommand.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        var sourceValue = await sourceCommand.ExecuteScalarAsync(ct);
        var receiptId = sourceValue is null ? throw new InvalidOperationException("تعذر إنشاء مصدر استلام المنتج المستورد.") : Convert.ToInt64(sourceValue);

        var accountingPosting = await AccountingEventPostingGateway.PostImportedInventoryReceiptAsync(
            connection,
            transaction,
            receiptId,
            postingAmount,
            $"IMPORTED-{importedReadyMadeProductId}:Receipt:{sourceOperationId:N}",
            $"Imported ready-made inventory receipt for {productName}",
            ct);

        var itemId = await GetOrCreateImportedInventoryItemIdAsync(connection, transaction, productCode, productName, unit, now, ct);
        const string updateItemSql = @"
            UPDATE dbo.InventoryItems
            SET CurrentQuantity = CurrentQuantity + @quantity,
                AvailableQuantity = AvailableQuantity + @quantity,
                IsActive = 1,
                UpdatedAt = @now
            WHERE InventoryItemID = @itemId;";
        await using var updateItemCommand = new SqlCommand(updateItemSql, connection, transaction);
        updateItemCommand.Parameters.AddWithValue("@quantity", quantity);
        updateItemCommand.Parameters.AddWithValue("@now", now);
        updateItemCommand.Parameters.AddWithValue("@itemId", itemId);
        if (await updateItemCommand.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("تعذر تحديث كمية المنتج المستورد في المخزون.");

        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost,
                 AccountingEventId, ImportedReadyMadeInventoryReceiptId, OperationalCostImpact)
            OUTPUT INSERTED.TransactionID
            VALUES
                (@itemId, N'Receive', @qty, @reference, @notes, @now, @cost, @unitCost,
                 @accountingEventId, @receiptId, @cost)", connection, transaction);
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@reference", $"IMPORTED-{importedReadyMadeProductId}:Receipt:{sourceOperationId:N}");
        AddNullable(cmd, "@notes", $"{(supplierId is > 0 ? $"SupplierId:{supplierId}" : "Supplier:NotSet")} | {notes ?? "Imported product receipt"}");
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@cost", operationalAmount);
        cmd.Parameters.AddWithValue("@unitCost", purchasePrice);
        cmd.Parameters.AddWithValue("@accountingEventId", accountingPosting.AccountingEventId);
        cmd.Parameters.AddWithValue("@receiptId", receiptId);
        var transactionValue = await cmd.ExecuteScalarAsync(ct);
        var inventoryTransactionId = transactionValue is null
            ? throw new InvalidOperationException("تعذر إنشاء حركة استلام المنتج المستورد.")
            : Convert.ToInt32(transactionValue);

        using var linkCommand = new SqlCommand(@"
            UPDATE dbo.ImportedReadyMadeInventoryReceipts
            SET InventoryTransactionId = @transactionId
            WHERE ImportedReadyMadeInventoryReceiptId = @receiptId;", connection, transaction);
        linkCommand.Parameters.AddWithValue("@receiptId", receiptId);
        linkCommand.Parameters.AddWithValue("@transactionId", inventoryTransactionId);
        await linkCommand.ExecuteNonQueryAsync(ct);
        await InsertStorageAllocationAsync(connection, transaction, goodsReceiptItemId, "ImportedProduct", quantity, storageOperationId, itemId, inventoryTransactionId, accountingPosting.AccountingEventId, null, receiptId, ct);
    }

    private static async Task<int> GetOrCreateImportedInventoryItemIdAsync(SqlConnection connection, SqlTransaction transaction, string productCode, string productName, string unit, DateTime now, CancellationToken ct)
    {
        using var getCmd = new SqlCommand("SELECT InventoryItemID FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK) WHERE ItemCode = @code", connection, transaction);
        getCmd.Parameters.AddWithValue("@code", productCode);
        var existingId = await getCmd.ExecuteScalarAsync(ct);
        if (existingId is not null && existingId is int itemId) return itemId;

        using var insertCmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryItems
                (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
            OUTPUT INSERTED.InventoryItemID
            VALUES
                (@code, @name, N'ImportedProduct', @unit, 0, 0, 0, 1, @now, @code, N'ImportedProduct', N'غير محدد', 0, N'Piece', 0, 0)", connection, transaction);
        insertCmd.Parameters.AddWithValue("@code", productCode);
        insertCmd.Parameters.AddWithValue("@name", productName);
        insertCmd.Parameters.AddWithValue("@unit", unit);
        insertCmd.Parameters.AddWithValue("@now", now);
        var insertedValue = await insertCmd.ExecuteScalarAsync(ct);
        return insertedValue is int insertedId ? insertedId : throw new InvalidOperationException("تعذر إنشاء سجل مخزون للمنتج المستورد.");
    }


    public async Task<InventoryFoundationPostingResultDto?> ReceiveFabricInventoryAsync(ReceiveFabricInventoryDto request, CancellationToken ct)
    {
        if (request.Rolls is { Count: > 0 })
            return await ReceiveFabricInventoryBatchAsync(request, ct);

        return await ReceiveFoundationInventoryAsync(
            request.GoodsReceiptItemId,
            1,
            request.UnitId,
            request.ItemCode,
            request.OpposingLedgerAccountCode,
            request.SourceOperationId,
            request.FabricTypeCode,
            request.RollCode,
            request.ColorValue,
            ct);
    }

    private async Task<InventoryFoundationPostingResultDto?> ReceiveFabricInventoryBatchAsync(ReceiveFabricInventoryDto request, CancellationToken ct)
    {
        if (request.SourceOperationId == Guid.Empty)
            throw new ArgumentException("SourceOperationId is required for idempotent inventory posting.");

        var rolls = request.Rolls!
            .Select((roll, index) => new
            {
                FabricCode = roll.FabricCode.Trim(),
                RollCode = string.IsNullOrWhiteSpace(roll.RollCode) ? $"{roll.FabricCode.Trim()}-{index + 1}" : roll.RollCode.Trim(),
                ColorValue = string.IsNullOrWhiteSpace(roll.ColorValue) ? request.ColorValue : roll.ColorValue.Trim(),
                Quantity = roll.Quantity,
            })
            .ToList();

        if (rolls.Count == 0)
            throw new ArgumentException("A multi-roll fabric batch requires at least one roll.");
        if (rolls.Any(roll => string.IsNullOrWhiteSpace(roll.FabricCode) || roll.Quantity <= 0m))
            throw new ArgumentException("Each roll in the batch requires a fabric code and a positive quantity.");

        var distinctRollCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roll in rolls)
        {
            if (!distinctRollCodes.Add(roll.RollCode))
                throw new InvalidOperationException($"Duplicate roll code '{roll.RollCode}' is not allowed within the same storage batch.");
        }

        var totalQuantity = rolls.Sum(roll => roll.Quantity);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var receipt = await ReadGoodsReceiptSourceAsync(connection, transaction, request.GoodsReceiptItemId, ct)
                ?? throw new InvalidOperationException("Goods receipt item was not found.");
            if (totalQuantity > receipt.Quantity)
                throw new InvalidOperationException($"The requested storage quantity ({totalQuantity}) exceeds the original receipt quantity ({receipt.Quantity}).");

            var existing = await TryReadExistingReceiptAsync(connection, transaction, request.GoodsReceiptItemId, 1, request.UnitId, rolls[0].FabricCode, request.SourceOperationId, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing with { IsExisting = true };
            }

            var totalOperationalAmount = decimal.Round(totalQuantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero);
            var totalPostingAmount = decimal.Round(totalOperationalAmount, 2, MidpointRounding.AwayFromZero);
            var opposingLedgerAccountId = await ReadOpposingLedgerAccountIdAsync(connection, transaction, request.OpposingLedgerAccountCode, ct);
            var postingId = await InsertInventoryReceiptPostingAsync(
                connection,
                transaction,
                request.GoodsReceiptItemId,
                1,
                opposingLedgerAccountId,
                request.SourceOperationId,
                totalOperationalAmount,
                totalPostingAmount,
                ct);

            var lineEntries = new List<(long LineId, int TransactionId, int InventoryItemId, string RollCode)>();
            var firstItemId = 0;
            var firstTransactionId = 0;

            for (var index = 0; index < rolls.Count; index++)
            {
                var roll = rolls[index];
                var itemCode = roll.FabricCode.Trim();
                var existingItem = await ReadFoundationItemAsync(connection, transaction, itemCode, ct);
                if (existingItem is not null && !existingItem.HasFoundation)
                    throw new InvalidOperationException("Legacy inventory items cannot be used by the foundation posting path.");
                if (existingItem is not null && (existingItem.InventoryClassId != 1 || existingItem.UnitId != request.UnitId))
                    throw new InvalidOperationException("The inventory item class or unit does not match the fabric receipt.");

                var itemId = existingItem?.InventoryItemId ?? await InsertFoundationInventoryItemAsync(
                    connection,
                    transaction,
                    itemCode,
                    receipt.ItemName,
                    1,
                    await ReadUnitCodeAsync(connection, transaction, request.UnitId, 1, ct),
                    request.UnitId,
                    roll.Quantity,
                    decimal.Round(roll.Quantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero),
                    request.FabricTypeCode,
                    roll.ColorValue,
                    DateTime.UtcNow,
                    ct);

                if (existingItem is not null)
                    await UpdateFoundationInventoryItemAsync(connection, transaction, itemId, roll.Quantity, decimal.Round(roll.Quantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero), DateTime.UtcNow, ct);

                await ApplyFabricPresentationAsync(
                    connection,
                    transaction,
                    itemId,
                    request.FabricTypeCode,
                    string.IsNullOrWhiteSpace(request.CatalogNumber) ? null : request.CatalogNumber.Trim(),
                    roll.ColorValue,
                    request.FabricWidth,
                    ct);

                var lineId = await InsertInventoryReceiptLineAsync(
                    connection,
                    transaction,
                    postingId,
                    itemId,
                    roll.Quantity,
                    receipt.UnitCost,
                    request.UnitId,
                    ct);

                var fabricRollId = await InsertFabricRollAsync(
                    connection,
                    transaction,
                    itemId,
                    roll.RollCode,
                    request.FabricTypeCode.Trim(),
                    roll.ColorValue,
                    roll.Quantity,
                    receipt.UnitCost,
                    request.UnitId,
                    lineId,
                    DateTime.UtcNow,
                    ct);
                await SetReceiptLineRollAsync(connection, transaction, lineId, fabricRollId, ct);

                var transactionSourceOperationId = DeriveOperationId(request.SourceOperationId, itemId + (index + 1) * 100000);
                var reference = $"GoodsReceipt:{receipt.ReceiptNumber}:Item:{request.GoodsReceiptItemId}:Roll:{roll.RollCode}";
                var transactionId = await InsertFoundationInventoryTransactionAsync(
                    connection,
                    transaction,
                    itemId,
                    "FabricInventoryReceived",
                    roll.Quantity,
                    reference,
                    decimal.Round(roll.Quantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero),
                    receipt.UnitCost,
                    transactionSourceOperationId,
                    DateTime.UtcNow,
                    ct);

                lineEntries.Add((lineId, transactionId, itemId, roll.RollCode));
                if (index == 0)
                {
                    firstItemId = itemId;
                    firstTransactionId = transactionId;
                }
            }

            var accountingEvent = await AccountingEventPostingGateway.PostInventoryReceiptAsync(
                connection,
                transaction,
                AccountingEventType.FabricInventoryReceived,
                postingId,
                totalPostingAmount,
                $"GoodsReceipt:{receipt.ReceiptNumber}:Item:{request.GoodsReceiptItemId}",
                $"Inventory receipt {receipt.ReceiptNumber} item {request.GoodsReceiptItemId} multi-roll storage",
                ct);

            foreach (var (lineId, transactionId, _, _) in lineEntries)
                await LinkInventoryReceiptArtifactsAsync(connection, transaction, lineId, transactionId, accountingEvent.AccountingEventId, ct);

            await InsertStorageAllocationAsync(
                connection,
                transaction,
                request.GoodsReceiptItemId,
                "Fabric",
                totalQuantity,
                request.SourceOperationId,
                firstItemId,
                firstTransactionId,
                accountingEvent.AccountingEventId,
                postingId,
                null,
                ct);

            await transaction.CommitAsync(ct);
            return new InventoryFoundationPostingResultDto(
                postingId,
                firstItemId,
                rolls[0].FabricCode,
                totalQuantity,
                totalOperationalAmount,
                totalPostingAmount,
                accountingEvent.AccountingEventId,
                firstTransactionId,
                false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<InventoryFoundationPostingResultDto?> ReceiveConsumableInventoryAsync(ReceiveConsumableInventoryDto request, CancellationToken ct) =>
        ReceiveFoundationInventoryAsync(
            request.GoodsReceiptItemId,
            2,
            request.UnitId,
            request.ItemCode,
            request.OpposingLedgerAccountCode,
            request.SourceOperationId,
            null,
            null,
            null,
            ct);

    public async Task<GoodsReceiptRuntimeResult> CreateGoodsReceiptAsync(CreateGoodsReceiptDto request, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var result = await CreateGoodsReceiptInTransactionAsync(connection, transaction, request, ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<GoodsReceiptRuntimeResult> CreateGoodsReceiptInTransactionAsync(SqlConnection connection, SqlTransaction transaction, CreateGoodsReceiptDto request, CancellationToken ct)
    {
        ValidateGoodsReceiptConfirmation(request);
        var existing = await ReadExistingGoodsReceiptAsync(connection, transaction, request.SourceOperationId, ct);
        if (existing is not null) return existing with { IsExisting = true };

        await ValidateGoodsReceiptConfirmationHeaderAsync(connection, transaction, request, ct);
        var receiptId = await InsertGoodsReceiptConfirmationAsync(connection, transaction, request, ct);
        foreach (var item in request.Items)
            await ConfirmGoodsReceiptItemAsync(connection, transaction, receiptId, request, item, ct);

        return new GoodsReceiptRuntimeResult(receiptId, request.ReceiptNumber.Trim(), false);
    }

    public async Task<GoodsReceiptReversalResult> ReverseGoodsReceiptAsync(ReverseGoodsReceiptDto request, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var result = await ReverseGoodsReceiptInTransactionAsync(connection, transaction, request, ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<GoodsReceiptReversalResult> ReverseGoodsReceiptInTransactionAsync(SqlConnection connection, SqlTransaction transaction, ReverseGoodsReceiptDto request, CancellationToken ct)
    {
        if (request.GoodsReceiptId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ReversedBy))
            throw new ArgumentException("A complete goods receipt reversal is required.");

        var existing = await ReadExistingGoodsReceiptReversalAsync(connection, transaction, request.SourceOperationId, ct);
        if (existing is not null) return existing with { IsExisting = true };

        await using (var availability = new SqlCommand("SELECT ReceiptStatus FROM dbo.GoodsReceipts WITH(UPDLOCK,HOLDLOCK) WHERE GoodsReceiptId=@receipt", connection, transaction))
        {
            availability.Parameters.AddWithValue("@receipt", request.GoodsReceiptId);
            var status = await availability.ExecuteScalarAsync(ct);
            if (status is null || !string.Equals(status.ToString(), "Confirmed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The goods receipt confirmation is unavailable or already reversed.");
        }

        var reversalId = await InsertGoodsReceiptReversalAsync(connection, transaction, request, ct);
        await using (var update = new SqlCommand("UPDATE dbo.GoodsReceipts SET ReceiptStatus=N'Reversed' WHERE GoodsReceiptId=@receipt; UPDATE dbo.GoodsReceiptItems SET LineStatus=N'Reversed' WHERE GoodsReceiptId=@receipt AND ISNULL(LineStatus,N'Confirmed')=N'Confirmed';", connection, transaction))
        {
            update.Parameters.AddWithValue("@receipt", request.GoodsReceiptId);
            await update.ExecuteNonQueryAsync(ct);
        }

        return new GoodsReceiptReversalResult(reversalId, false);
    }

    private static void ValidateGoodsReceipt(CreateGoodsReceiptDto request)
    {
        if (request.SupplierId <= 0 || request.WarehouseId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReceiptNumber) || string.IsNullOrWhiteSpace(request.CreatedBy) || request.Items.Count == 0)
            throw new ArgumentException("Supplier, warehouse, receipt number, creator, operation, and items are required.");
        if (request.Items.Any(x => x.InventoryItemId <= 0 || x.Quantity <= 0m || x.UnitCost <= 0m))
            throw new ArgumentException("Every goods receipt item requires a positive inventory item, quantity, and unit cost.");
        if (request.Items.Select(x => x.InventoryItemId).Distinct().Count() != request.Items.Count)
            throw new ArgumentException("An inventory item may appear only once in a goods receipt.");
    }

    private static void ValidateGoodsReceiptConfirmation(CreateGoodsReceiptDto request)
    {
        if (request.SupplierId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.ReceiptNumber) || string.IsNullOrWhiteSpace(request.CreatedBy) || request.Items.Count == 0)
            throw new ArgumentException("Supplier, receipt number, creator, operation, and items are required.");
        if (request.Items.Any(x => (string.IsNullOrWhiteSpace(x.ItemDescription) && !x.InventoryItemId.HasValue) || x.Quantity <= 0m || x.UnitCost <= 0m || (x.ItemType is not null && !IsSupportedStorageItemType(x.ItemType))))
            throw new ArgumentException("Every confirmation line requires a valid description, quantity, cost, and optional supported item type.");
    }

    private static bool IsSupportedStorageItemType(string? itemType) => itemType is "Fabric" or "ImportedProduct" or "UsedTool";

    private static async Task ValidateGoodsReceiptConfirmationHeaderAsync(SqlConnection c, SqlTransaction t, CreateGoodsReceiptDto request, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            IF NOT EXISTS(SELECT 1 FROM dbo.Suppliers WITH(UPDLOCK,HOLDLOCK) WHERE SupplierId=@supplier AND IsActive=1) THROW 52430,N'Supplier is unavailable.',1;
            IF @purchaseOrderId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders WITH(UPDLOCK,HOLDLOCK) WHERE PurchaseOrderId=@purchaseOrderId AND SupplierId=@supplier) THROW 52432,N'Purchase order does not belong to supplier.',1;", c, t);
        cmd.Parameters.AddWithValue("@supplier", request.SupplierId);
        cmd.Parameters.AddWithValue("@purchaseOrderId", request.PurchaseOrderId ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> InsertGoodsReceiptConfirmationAsync(SqlConnection c, SqlTransaction t, CreateGoodsReceiptDto request, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            INSERT dbo.GoodsReceipts(SupplierId,PurchaseOrderId,WarehouseId,ReceiptNumber,ReceiptDate,Notes,CreatedAt,SourceOperationId,ReceiptStatus)
            OUTPUT INSERTED.GoodsReceiptId VALUES(@supplier,@po,@warehouse,@number,@date,@notes,SYSUTCDATETIME(),@operation,N'Confirmed')", c, t);
        cmd.Parameters.AddWithValue("@supplier", request.SupplierId); cmd.Parameters.AddWithValue("@po", request.PurchaseOrderId ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@warehouse", request.WarehouseId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@number", request.ReceiptNumber.Trim()); cmd.Parameters.AddWithValue("@date", request.ReceiptDate); cmd.Parameters.AddWithValue("@notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task ConfirmGoodsReceiptItemAsync(SqlConnection c, SqlTransaction t, int receiptId, CreateGoodsReceiptDto request, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        var invoice = await ValidateInvoiceLineForConfirmationAsync(c, t, request.SupplierId, request.PurchaseOrderId, line, ct);
        var description = await ResolveConfirmationDescriptionAsync(c, t, line, ct);
        var itemType = line.ItemType ?? await ResolveInvoiceItemTypeAsync(c, t, line.SupplierInvoiceLineId, ct);
        var resolvedRollCount = line.RollCount ?? invoice?.RollCount;
        var receiptItemId = await InsertGoodsReceiptConfirmationItemAsync(c, t, receiptId, line, description, itemType, resolvedRollCount, ct);
        await MatchPurchaseOrderAsync(c, t, request.PurchaseOrderId, receiptItemId, description, line, ct);
        await RecordInvoiceDifferenceAsync(c, t, receiptItemId, invoice, line, ct);
    }

    private static async Task<string?> ResolveInvoiceItemTypeAsync(SqlConnection c, SqlTransaction t, int? supplierInvoiceLineId, CancellationToken ct)
    {
        if (!supplierInvoiceLineId.HasValue) return null;
        await using var cmd = new SqlCommand("SELECT ItemType FROM dbo.SupplierInvoiceLines WHERE SupplierInvoiceLineId=@id", c, t);
        cmd.Parameters.AddWithValue("@id", supplierInvoiceLineId.Value);
        var itemType = (await cmd.ExecuteScalarAsync(ct))?.ToString();
        return IsSupportedStorageItemType(itemType) ? itemType : null;
    }

    private static async Task<InvoiceLineMatch?> ValidateInvoiceLineForConfirmationAsync(SqlConnection c, SqlTransaction t, int supplierId, int? purchaseOrderId, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        if (!line.SupplierInvoiceLineId.HasValue) return null;
        await using var cmd = new SqlCommand(@"SELECT sil.SupplierInvoiceLineId,sil.Quantity,sil.UnitCost,sil.RollCount FROM dbo.SupplierInvoiceLines sil WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierInvoices si WITH(UPDLOCK,HOLDLOCK) ON si.SupplierInvoiceId=sil.SupplierInvoiceId WHERE sil.SupplierInvoiceLineId=@id AND si.SupplierId=@supplier AND ((si.PurchaseOrderId IS NULL AND @purchaseOrderId IS NULL) OR si.PurchaseOrderId=@purchaseOrderId) AND sil.Status=N'Posted'", c, t);
        cmd.Parameters.AddWithValue("@id", line.SupplierInvoiceLineId.Value); cmd.Parameters.AddWithValue("@supplier", supplierId); cmd.Parameters.AddWithValue("@purchaseOrderId", purchaseOrderId ?? (object)DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Supplier invoice line does not match the receipt supplier or purchase order.");
        return new InvoiceLineMatch(r.GetInt64(0), r.GetDecimal(1), r.GetDecimal(2), false, r.IsDBNull(3) ? null : r.GetInt32(3));
    }

    private static async Task<string> ResolveConfirmationDescriptionAsync(SqlConnection c, SqlTransaction t, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(line.ItemDescription)) return line.ItemDescription.Trim();
        await using var cmd = new SqlCommand("SELECT ItemName FROM dbo.InventoryItems WHERE InventoryItemID=@id", c, t);
        cmd.Parameters.AddWithValue("@id", line.InventoryItemId!.Value);
        return (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? $"InventoryItem:{line.InventoryItemId.Value}";
    }

    private static async Task<int> InsertGoodsReceiptConfirmationItemAsync(SqlConnection c, SqlTransaction t, int receiptId, CreateGoodsReceiptItemDto line, string description, string? itemType, int? rollCount, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"INSERT dbo.GoodsReceiptItems(GoodsReceiptId,ItemName,ReceivedQuantity,UnitCost,LineTotal,InventoryItemId,SourceOperationId,LineStatus,SupplierInvoiceLineId,ItemType,RollCount) OUTPUT INSERTED.GoodsReceiptItemId VALUES(@receipt,@name,@quantity,@cost,@total,NULL,NEWID(),N'Confirmed',@invoiceLine,@itemType,@rollCount)", c, t);
        cmd.Parameters.AddWithValue("@receipt", receiptId); cmd.Parameters.AddWithValue("@name", description); AddDecimal(cmd, "@quantity", line.Quantity); AddDecimal(cmd, "@cost", line.UnitCost); AddDecimal(cmd, "@total", decimal.Round(line.Quantity * line.UnitCost, 6, MidpointRounding.AwayFromZero)); cmd.Parameters.AddWithValue("@invoiceLine", line.SupplierInvoiceLineId ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@itemType", itemType ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@rollCount", rollCount ?? (object)DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<GoodsReceiptRuntimeResult?> ReadExistingGoodsReceiptAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT GoodsReceiptId,ReceiptNumber FROM dbo.GoodsReceipts WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operation", c, t);
        cmd.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = operation;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new GoodsReceiptRuntimeResult(r.GetInt32(0), r.GetString(1), true) : null;
    }

    private static async Task ValidateGoodsReceiptHeaderAsync(SqlConnection c, SqlTransaction t, CreateGoodsReceiptDto request, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            IF NOT EXISTS(SELECT 1 FROM dbo.Suppliers WITH(UPDLOCK,HOLDLOCK) WHERE SupplierId=@supplier AND IsActive=1) THROW 52430,N'Supplier is unavailable.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.Warehouses WITH(UPDLOCK,HOLDLOCK) WHERE WarehouseId=@warehouse AND IsActive=1) THROW 52431,N'Warehouse is unavailable.',1;
            IF @purchaseOrderId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PurchaseOrders WITH(UPDLOCK,HOLDLOCK) WHERE PurchaseOrderId=@purchaseOrderId AND SupplierId=@supplier) THROW 52432,N'Purchase order does not belong to supplier.',1;", c, t);
        cmd.Parameters.AddWithValue("@supplier", request.SupplierId); cmd.Parameters.AddWithValue("@warehouse", request.WarehouseId);
        cmd.Parameters.AddWithValue("@purchaseOrderId", request.PurchaseOrderId ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> InsertGoodsReceiptAsync(SqlConnection c, SqlTransaction t, CreateGoodsReceiptDto request, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            INSERT dbo.GoodsReceipts(SupplierId,PurchaseOrderId,WarehouseId,ReceiptNumber,ReceiptDate,Notes,CreatedAt,SourceOperationId,ReceiptStatus)
            OUTPUT INSERTED.GoodsReceiptId VALUES(@supplier,@po,@warehouse,@number,@date,@notes,SYSUTCDATETIME(),@operation,N'Posted')", c, t);
        cmd.Parameters.AddWithValue("@supplier", request.SupplierId); cmd.Parameters.AddWithValue("@po", request.PurchaseOrderId ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@warehouse", request.WarehouseId);
        cmd.Parameters.AddWithValue("@number", request.ReceiptNumber.Trim()); cmd.Parameters.AddWithValue("@date", request.ReceiptDate); cmd.Parameters.AddWithValue("@notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task PostGoodsReceiptItemAsync(SqlConnection c, SqlTransaction t, int receiptId, CreateGoodsReceiptDto request, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        var item = await ReadReceiptInventoryItemAsync(c, t, line.InventoryItemId!.Value, ct) ?? throw new InvalidOperationException("The inventory item is not an active foundation item.");
        var invoice = await ValidateInvoiceLineAsync(c, t, request.SupplierId, request.PurchaseOrderId, line, ct);
        var resolvedRollCount = line.RollCount ?? invoice?.RollCount;
        if (item.InventoryClassId == 1 && (!resolvedRollCount.HasValue || resolvedRollCount.Value <= 0))
            throw new ArgumentException("A roll count is required for fabric receipts.");
        var itemName = item.ItemName;
        var receiptItemId = await InsertGoodsReceiptItemAsync(c, t, receiptId, itemName, line, resolvedRollCount, ct);
        await MatchPurchaseOrderAsync(c, t, request.PurchaseOrderId, receiptItemId, itemName, line, ct);
        await RecordInvoiceDifferenceAsync(c, t, receiptItemId, invoice, line, ct);

        var operationalAmount = decimal.Round(line.Quantity * line.UnitCost, 6, MidpointRounding.AwayFromZero);
        var postingAmount = decimal.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
        var lineOperation = DeriveOperationId(request.SourceOperationId, receiptItemId);
        await UpdateFoundationInventoryItemAsync(c, t, item.InventoryItemId, line.Quantity, operationalAmount, DateTime.UtcNow, ct);
        var postingId = await InsertInventoryReceiptPostingAsync(c, t, receiptItemId, item.InventoryClassId, await ReadOpposingLedgerAccountIdAsync(c, t, "2100", ct), lineOperation, operationalAmount, postingAmount, ct);
        var postingLineId = await InsertInventoryReceiptLineAsync(c, t, postingId, item.InventoryItemId, line.Quantity, line.UnitCost, item.UnitId, ct);
        if (item.InventoryClassId == 1)
        {
            if (string.IsNullOrWhiteSpace(line.RollCode)) throw new ArgumentException("A roll code is required for fabric receipts.");
            var rollId = await InsertFabricRollAsync(c, t, item.InventoryItemId, line.RollCode.Trim(), item.FabricTypeCode ?? item.ItemCode, item.ColorValue, line.Quantity, line.UnitCost, item.UnitId, postingLineId, DateTime.UtcNow, ct);
            await SetReceiptLineRollAsync(c, t, postingLineId, rollId, ct);
        }
        var reference = $"GoodsReceipt:{request.ReceiptNumber.Trim()}:Item:{receiptItemId}";
        var transactionId = await InsertFoundationInventoryTransactionAsync(c, t, item.InventoryItemId, "GoodsReceiptReceived", line.Quantity, reference, operationalAmount, line.UnitCost, lineOperation, DateTime.UtcNow, ct);
        await SetInventoryTransactionWarehouseAsync(c, t, transactionId, request.WarehouseId!.Value, ct);
        var accounting = await AccountingEventPostingGateway.PostInventoryReceiptAsync(c, t, item.InventoryClassId == 1 ? AccountingEventType.FabricInventoryReceived : AccountingEventType.ConsumableInventoryReceived, postingId, postingAmount, reference, $"Goods receipt {request.ReceiptNumber.Trim()} item {receiptItemId}", ct);
        await SetInventoryReceiptAccountingEventPostedAsync(c, t, accounting.AccountingEventId, ct);
        await LinkInventoryReceiptArtifactsAsync(c, t, postingLineId, transactionId, accounting.AccountingEventId, ct);
    }

    private static async Task<ReceiptInventoryItem?> ReadReceiptInventoryItemAsync(SqlConnection c, SqlTransaction t, int itemId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"SELECT i.InventoryItemID,i.ItemName,i.ItemCode,f.InventoryClassId,f.UnitId,i.FabricCategory,i.FabricColor FROM dbo.InventoryItems i WITH(UPDLOCK,HOLDLOCK) JOIN dbo.InventoryItemFoundation f WITH(UPDLOCK,HOLDLOCK) ON f.InventoryItemId=i.InventoryItemID WHERE i.InventoryItemID=@id AND i.IsActive=1", c, t);
        cmd.Parameters.AddWithValue("@id", itemId); await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new ReceiptInventoryItem(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetByte(3), r.GetInt16(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)) : null;
    }

    private static async Task<InvoiceLineMatch?> ValidateInvoiceLineAsync(SqlConnection c, SqlTransaction t, int supplierId, int? purchaseOrderId, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        if (!line.SupplierInvoiceLineId.HasValue) return null;
        await using var cmd = new SqlCommand(@"SELECT sil.SupplierInvoiceLineId,sil.Quantity,sil.UnitCost,sil.InventoryItemId,sil.RollCount FROM dbo.SupplierInvoiceLines sil WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierInvoices si WITH(UPDLOCK,HOLDLOCK) ON si.SupplierInvoiceId=sil.SupplierInvoiceId WHERE sil.SupplierInvoiceLineId=@id AND (sil.InventoryItemId IS NULL OR sil.InventoryItemId=@itemId) AND si.SupplierId=@supplier AND ((si.PurchaseOrderId IS NULL AND @purchaseOrderId IS NULL) OR si.PurchaseOrderId=@purchaseOrderId) AND sil.Status=N'Posted'", c, t);
        cmd.Parameters.AddWithValue("@id", line.SupplierInvoiceLineId.Value); cmd.Parameters.AddWithValue("@itemId", line.InventoryItemId); cmd.Parameters.AddWithValue("@supplier", supplierId);
        cmd.Parameters.AddWithValue("@purchaseOrderId", purchaseOrderId ?? (object)DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Supplier invoice line does not match the receipt supplier or inventory item.");
        var match = new InvoiceLineMatch(r.GetInt64(0), r.GetDecimal(1), r.GetDecimal(2), r.IsDBNull(3), r.IsDBNull(4) ? null : r.GetInt32(4));
        await r.CloseAsync();
        if (match.NeedsInventoryLink)
        {
            await using var link = new SqlCommand("UPDATE dbo.SupplierInvoiceLines SET InventoryItemId=@itemId WHERE SupplierInvoiceLineId=@lineId AND InventoryItemId IS NULL", c, t);
            link.Parameters.AddWithValue("@itemId", line.InventoryItemId);
            link.Parameters.AddWithValue("@lineId", line.SupplierInvoiceLineId.Value);
            if (await link.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Supplier invoice line was linked to another inventory item.");
        }
        return match;
    }

    private static async Task<int> InsertGoodsReceiptItemAsync(SqlConnection c, SqlTransaction t, int receiptId, string itemName, CreateGoodsReceiptItemDto line, int? resolvedRollCount, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"INSERT dbo.GoodsReceiptItems(GoodsReceiptId,ItemName,ReceivedQuantity,UnitCost,LineTotal,InventoryItemId,SourceOperationId,LineStatus,SupplierInvoiceLineId,RollCount) OUTPUT INSERTED.GoodsReceiptItemId VALUES(@receipt,@name,@quantity,@cost,@total,@item,NULL,N'Posted',@invoiceLine,@rollCount)", c, t);
        cmd.Parameters.AddWithValue("@receipt", receiptId); cmd.Parameters.AddWithValue("@name", itemName); AddDecimal(cmd, "@quantity", line.Quantity); AddDecimal(cmd, "@cost", line.UnitCost); AddDecimal(cmd, "@total", decimal.Round(line.Quantity * line.UnitCost, 6, MidpointRounding.AwayFromZero)); cmd.Parameters.AddWithValue("@item", line.InventoryItemId); cmd.Parameters.AddWithValue("@invoiceLine", line.SupplierInvoiceLineId ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("@rollCount", resolvedRollCount ?? (object)DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task MatchPurchaseOrderAsync(SqlConnection c, SqlTransaction t, int? purchaseOrderId, int receiptItemId, string itemName, CreateGoodsReceiptItemDto line, CancellationToken ct)
    {
        if (!purchaseOrderId.HasValue) return;
        await using var cmd = new SqlCommand(@"SELECT TOP(1) poi.Quantity,poi.UnitCost,COALESCE((SELECT SUM(gri.ReceivedQuantity) FROM dbo.GoodsReceiptItems gri JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId=gri.GoodsReceiptId WHERE gr.PurchaseOrderId=poi.PurchaseOrderId AND gri.ItemName=poi.ItemName AND gri.GoodsReceiptItemId<>@receiptItem AND ISNULL(gri.LineStatus,N'Posted')=N'Posted'),0) FROM dbo.PurchaseOrderItems poi WITH(UPDLOCK,HOLDLOCK) WHERE poi.PurchaseOrderId=@po AND poi.ItemName=@name", c, t);
        cmd.Parameters.AddWithValue("@po", purchaseOrderId.Value); cmd.Parameters.AddWithValue("@name", itemName); cmd.Parameters.AddWithValue("@receiptItem", receiptItemId); await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) { await InsertDifferenceAsync(c, t, receiptItemId, "PurchaseOrderItemMissing", null, line.Quantity, null, line.UnitCost, ct); return; }
        var ordered = r.GetDecimal(0); var cost = r.GetDecimal(1); var alreadyReceived = r.GetDecimal(2); await r.CloseAsync();
        if (alreadyReceived + line.Quantity > ordered) await InsertDifferenceAsync(c, t, receiptItemId, "PurchaseOrderQuantityVariance", ordered - alreadyReceived, line.Quantity, cost, line.UnitCost, ct);
        if (cost != line.UnitCost) await InsertDifferenceAsync(c, t, receiptItemId, "PurchaseOrderCostVariance", ordered, line.Quantity, cost, line.UnitCost, ct);
    }

    private static Task RecordInvoiceDifferenceAsync(SqlConnection c, SqlTransaction t, int receiptItemId, InvoiceLineMatch? invoice, CreateGoodsReceiptItemDto line, CancellationToken ct) => invoice is null ? Task.CompletedTask : invoice.Quantity != line.Quantity || invoice.UnitCost != line.UnitCost || invoice.RollCount != line.RollCount ? InsertDifferenceAsync(c, t, receiptItemId, "SupplierInvoiceVariance", invoice.Quantity, line.Quantity, invoice.UnitCost, line.UnitCost, ct) : Task.CompletedTask;

    private static async Task InsertDifferenceAsync(SqlConnection c, SqlTransaction t, int receiptItemId, string type, decimal? expectedQuantity, decimal actualQuantity, decimal? expectedUnitCost, decimal actualUnitCost, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.GoodsReceiptDifferences(GoodsReceiptItemId,DifferenceType,ExpectedQuantity,ActualQuantity,ExpectedUnitCost,ActualUnitCost) VALUES(@item,@type,@expectedQuantity,@actualQuantity,@expectedCost,@actualCost)", c, t); cmd.Parameters.AddWithValue("@item", receiptItemId); cmd.Parameters.AddWithValue("@type", type); AddNullable(cmd, "@expectedQuantity", expectedQuantity); AddDecimal(cmd, "@actualQuantity", actualQuantity); AddNullable(cmd, "@expectedCost", expectedUnitCost); AddDecimal(cmd, "@actualCost", actualUnitCost); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task SetInventoryTransactionWarehouseAsync(SqlConnection c, SqlTransaction t, int transactionId, int warehouseId, CancellationToken ct)
    { await using var cmd = new SqlCommand("UPDATE dbo.InventoryTransactions SET WarehouseId=@warehouse WHERE TransactionID=@transaction", c, t); cmd.Parameters.AddWithValue("@warehouse", warehouseId); cmd.Parameters.AddWithValue("@transaction", transactionId); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task SetInventoryReceiptAccountingEventPostedAsync(SqlConnection c, SqlTransaction t, long accountingEventId, CancellationToken ct)
    { await using var cmd = new SqlCommand("UPDATE dbo.AccountingEvents SET Status=N'Posted' WHERE AccountingEventId=@event", c, t); cmd.Parameters.AddWithValue("@event", accountingEventId); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task<GoodsReceiptReversalResult?> ReadExistingGoodsReceiptReversalAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT GoodsReceiptReversalId FROM dbo.GoodsReceiptReversals WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operation", c, t); cmd.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = operation; var value = await cmd.ExecuteScalarAsync(ct); return value is null ? null : new GoodsReceiptReversalResult(Convert.ToInt64(value), true); }

    private static async Task<List<PostedGoodsReceiptItem>> ReadPostedGoodsReceiptItemsAsync(SqlConnection c, SqlTransaction t, int receiptId, CancellationToken ct)
    {
        const string sql = @"SELECT gri.GoodsReceiptItemId,gri.InventoryItemId,gri.ReceivedQuantity,gri.UnitCost,gr.WarehouseId,f.InventoryClassId,f.UnitId,irl.InventoryReceiptLineId,irl.InventoryTransactionId,irl.AccountingEventId,irl.FabricRollId
            FROM dbo.GoodsReceipts gr WITH(UPDLOCK,HOLDLOCK) JOIN dbo.GoodsReceiptItems gri WITH(UPDLOCK,HOLDLOCK) ON gri.GoodsReceiptId=gr.GoodsReceiptId JOIN dbo.InventoryItemFoundation f WITH(UPDLOCK,HOLDLOCK) ON f.InventoryItemId=gri.InventoryItemId JOIN dbo.InventoryReceiptPostings irp WITH(UPDLOCK,HOLDLOCK) ON irp.GoodsReceiptItemId=gri.GoodsReceiptItemId JOIN dbo.InventoryReceiptLines irl WITH(UPDLOCK,HOLDLOCK) ON irl.InventoryReceiptPostingId=irp.InventoryReceiptPostingId WHERE gr.GoodsReceiptId=@receipt AND gr.ReceiptStatus=N'Posted' AND gri.LineStatus=N'Posted'";
        await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@receipt", receiptId); await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<PostedGoodsReceiptItem>();
        while (await r.ReadAsync(ct)) rows.Add(new PostedGoodsReceiptItem(r.GetInt32(0), r.GetInt32(1), r.GetDecimal(2), r.GetDecimal(3), r.GetInt32(4), r.GetByte(5), r.GetInt16(6), r.GetInt64(7), r.GetInt32(8), r.GetInt64(9), r.IsDBNull(10) ? null : r.GetInt64(10)));
        return rows;
    }

    private static async Task<long> InsertGoodsReceiptReversalAsync(SqlConnection c, SqlTransaction t, ReverseGoodsReceiptDto request, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.GoodsReceiptReversals(OriginalGoodsReceiptId,SourceOperationId,Reason,ReversedBy) OUTPUT INSERTED.GoodsReceiptReversalId VALUES(@receipt,@operation,@reason,@by)", c, t); cmd.Parameters.AddWithValue("@receipt", request.GoodsReceiptId); cmd.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId; cmd.Parameters.AddWithValue("@reason", request.Reason.Trim()); cmd.Parameters.AddWithValue("@by", request.ReversedBy.Trim()); return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)); }

    private static async Task ReverseGoodsReceiptItemAsync(SqlConnection c, SqlTransaction t, long reversalId, ReverseGoodsReceiptDto request, PostedGoodsReceiptItem item, CancellationToken ct)
    {
        if (item.InventoryClassId == 1)
            await EnsureFabricRollCanReverseAsync(c, t, item, ct);
        else
            await EnsureFoundationQuantityCanReverseAsync(c, t, item, ct);

        var operationalAmount = decimal.Round(item.Quantity * item.UnitCost, 6, MidpointRounding.AwayFromZero);
        await ReverseFoundationInventoryAsync(c, t, item.InventoryItemId, item.Quantity, operationalAmount, ct);
        var operation = DeriveOperationId(request.SourceOperationId, item.GoodsReceiptItemId);
        var transactionId = await InsertFoundationInventoryTransactionAsync(c, t, item.InventoryItemId, "GoodsReceiptReversed", -item.Quantity, $"GoodsReceiptReversal:{request.GoodsReceiptId}:Item:{item.GoodsReceiptItemId}", -operationalAmount, item.UnitCost, operation, DateTime.UtcNow, ct);
        await SetInventoryTransactionWarehouseAsync(c, t, transactionId, item.WarehouseId, ct);
        var reversalEvent = await FoundationPostingGateway.ReverseAsync(c, t, item.AccountingEventId, operation, $"GoodsReceiptReversal:{request.GoodsReceiptId}:Item:{item.GoodsReceiptItemId}", request.Reason.Trim(), request.ReversedBy.Trim(), ct);
        await using var cmd = new SqlCommand(@"INSERT dbo.GoodsReceiptReversalLines(GoodsReceiptReversalId,GoodsReceiptItemId,InventoryTransactionId,AccountingEventId) VALUES(@reversal,@item,@transaction,@event); UPDATE dbo.GoodsReceiptItems SET LineStatus=N'Reversed' WHERE GoodsReceiptItemId=@item;", c, t);
        cmd.Parameters.AddWithValue("@reversal", reversalId); cmd.Parameters.AddWithValue("@item", item.GoodsReceiptItemId); cmd.Parameters.AddWithValue("@transaction", transactionId); cmd.Parameters.AddWithValue("@event", reversalEvent.AccountingEventId); await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureFoundationQuantityCanReverseAsync(SqlConnection c, SqlTransaction t, PostedGoodsReceiptItem item, CancellationToken ct)
    { await using var cmd = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.InventoryItemFoundation WITH(UPDLOCK,HOLDLOCK) WHERE InventoryItemId=@item AND AvailableQuantity>=@quantity) THROW 52433,N'Goods receipt inventory was consumed and cannot be reversed.',1;", c, t); cmd.Parameters.AddWithValue("@item", item.InventoryItemId); AddDecimal(cmd, "@quantity", item.Quantity); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task EnsureFabricRollCanReverseAsync(SqlConnection c, SqlTransaction t, PostedGoodsReceiptItem item, CancellationToken ct)
    { if (!item.FabricRollId.HasValue) throw new InvalidOperationException("Fabric receipt does not own a roll."); await using var cmd = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.FabricRolls WITH(UPDLOCK,HOLDLOCK) WHERE FabricRollId=@roll AND AvailableQuantity=@quantity AND ConsumedQuantity=0) THROW 52434,N'Fabric receipt was consumed and cannot be reversed.',1; DELETE FROM dbo.FabricRolls WHERE FabricRollId=@roll;", c, t); cmd.Parameters.AddWithValue("@roll", item.FabricRollId.Value); AddDecimal(cmd, "@quantity", item.Quantity); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task ReverseFoundationInventoryAsync(SqlConnection c, SqlTransaction t, int itemId, decimal quantity, decimal amount, CancellationToken ct)
    { await using var cmd = new SqlCommand(@"UPDATE dbo.InventoryItems SET CurrentQuantity=CurrentQuantity-@quantity,AvailableQuantity=AvailableQuantity-@quantity,UpdatedAt=SYSUTCDATETIME() WHERE InventoryItemID=@item; UPDATE dbo.InventoryItemFoundation SET OriginalQuantity=OriginalQuantity-@quantity,AvailableQuantity=AvailableQuantity-@quantity,OperationalValue=OperationalValue-@amount,OfficialUnitCost=CASE WHEN OriginalQuantity-@quantity=0 THEN OfficialUnitCost ELSE CONVERT(decimal(18,6),(OperationalValue-@amount)/(OriginalQuantity-@quantity)) END,UpdatedAt=SYSUTCDATETIME() WHERE InventoryItemId=@item;", c, t); cmd.Parameters.AddWithValue("@item", itemId); AddDecimal(cmd, "@quantity", quantity); AddDecimal(cmd, "@amount", amount); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task SetGoodsReceiptStatusAsync(SqlConnection c, SqlTransaction t, int receiptId, string status, CancellationToken ct)
    { await using var cmd = new SqlCommand("UPDATE dbo.GoodsReceipts SET ReceiptStatus=@status WHERE GoodsReceiptId=@receipt", c, t); cmd.Parameters.AddWithValue("@status", status); cmd.Parameters.AddWithValue("@receipt", receiptId); await cmd.ExecuteNonQueryAsync(ct); }

    private static Guid DeriveOperationId(Guid root, int itemId)
    { var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{root:N}:{itemId}")); return new Guid(bytes[..16]); }

    internal static Guid CreateFabricConsumptionOperationId(int orderItemId, bool isReadyMade) =>
        DeriveOperationId(isReadyMade
            ? new Guid("315fb2c9-a94e-4056-a667-b5b746966f3e")
            : new Guid("6081b54c-d20c-40b2-97e0-860a9c2b1986"), orderItemId);

    private async Task<InventoryFoundationPostingResultDto?> ReceiveFoundationInventoryAsync(
        int goodsReceiptItemId,
        byte inventoryClassId,
        short unitId,
        string itemCode,
        string opposingLedgerAccountCode,
        Guid sourceOperationId,
        string? fabricTypeCode,
        string? rollCode,
        string? colorValue,
        CancellationToken ct)
    {
        if (sourceOperationId == Guid.Empty)
            throw new ArgumentException("SourceOperationId is required for idempotent inventory posting.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("Inventory item code is required.");
            if (inventoryClassId == 1 && string.IsNullOrWhiteSpace(fabricTypeCode))
                throw new ArgumentException("Fabric type code is required.");

            var incomplete = await TryReadIncompleteReceiptAsync(connection, transaction, goodsReceiptItemId, inventoryClassId, unitId, itemCode, sourceOperationId, ct);
            if (incomplete is not null)
            {
                var repaired = await CompleteIncompleteReceiptStorageAsync(connection, transaction, incomplete, ct);
                await transaction.CommitAsync(ct);
                return repaired;
            }

            var existing = await TryReadExistingReceiptAsync(connection, transaction, goodsReceiptItemId, inventoryClassId, unitId, itemCode, sourceOperationId, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing with { IsExisting = true };
            }

            var unitCode = await ReadUnitCodeAsync(connection, transaction, unitId, inventoryClassId, ct);
            var opposingLedgerAccountId = await ReadOpposingLedgerAccountIdAsync(connection, transaction, opposingLedgerAccountCode, ct);
            var receipt = await ReadGoodsReceiptSourceAsync(connection, transaction, goodsReceiptItemId, ct)
                ?? throw new InvalidOperationException("Goods receipt item was not found.");
            var operationalAmount = Math.Round(receipt.Quantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero);
            var postingAmount = Math.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
            if (operationalAmount <= 0m || postingAmount <= 0m)
                throw new InvalidOperationException("The goods receipt source amount must be positive.");

            var existingItem = await ReadFoundationItemAsync(connection, transaction, itemCode.Trim(), ct);
            if (existingItem is not null && !existingItem.HasFoundation)
                throw new InvalidOperationException("Legacy inventory items cannot be used by the foundation posting path.");
            if (existingItem is not null && (existingItem.InventoryClassId != inventoryClassId || existingItem.UnitId != unitId))
                throw new InvalidOperationException("The inventory item class or unit does not match the foundation request.");

            var now = DateTime.UtcNow;
            var itemId = existingItem?.InventoryItemId ?? await InsertFoundationInventoryItemAsync(
                connection,
                transaction,
                itemCode.Trim(),
                receipt.ItemName,
                inventoryClassId,
                unitCode,
                unitId,
                receipt.Quantity,
                operationalAmount,
                fabricTypeCode,
                colorValue,
                now,
                ct);

            if (existingItem is not null)
            {
                await UpdateFoundationInventoryItemAsync(
                    connection,
                    transaction,
                    itemId,
                    receipt.Quantity,
                    operationalAmount,
                    now,
                    ct);
            }

            var postingId = await InsertInventoryReceiptPostingAsync(
                connection,
                transaction,
                goodsReceiptItemId,
                inventoryClassId,
                opposingLedgerAccountId,
                sourceOperationId,
                operationalAmount,
                postingAmount,
                ct);
            var lineId = await InsertInventoryReceiptLineAsync(
                connection,
                transaction,
                postingId,
                itemId,
                receipt.Quantity,
                receipt.UnitCost,
                unitId,
                ct);

            long? fabricRollId = null;
            if (inventoryClassId == 1)
            {
                var resolvedRollCode = string.IsNullOrWhiteSpace(rollCode)
                    ? $"{itemCode.Trim()}-R-{lineId}"
                    : rollCode.Trim();
                fabricRollId = await InsertFabricRollAsync(
                    connection,
                    transaction,
                    itemId,
                    resolvedRollCode,
                    fabricTypeCode!.Trim(),
                    colorValue,
                    receipt.Quantity,
                    receipt.UnitCost,
                    unitId,
                    lineId,
                    now,
                    ct);
                await SetReceiptLineRollAsync(connection, transaction, lineId, fabricRollId.Value, ct);
            }

            var reference = $"GoodsReceipt:{receipt.ReceiptNumber}:Item:{goodsReceiptItemId}";
            var transactionId = await InsertFoundationInventoryTransactionAsync(
                connection,
                transaction,
                itemId,
                inventoryClassId == 1 ? "FabricInventoryReceived" : "ConsumableInventoryReceived",
                receipt.Quantity,
                reference,
                operationalAmount,
                receipt.UnitCost,
                sourceOperationId,
                now,
                ct);

            var accountingEvent = await AccountingEventPostingGateway.PostInventoryReceiptAsync(
                connection,
                transaction,
                inventoryClassId == 1 ? AccountingEventType.FabricInventoryReceived : AccountingEventType.ConsumableInventoryReceived,
                postingId,
                postingAmount,
                reference,
                $"Inventory receipt {receipt.ReceiptNumber} item {goodsReceiptItemId}",
                ct);

            await LinkInventoryReceiptArtifactsAsync(connection, transaction, lineId, transactionId, accountingEvent.AccountingEventId, ct);
            await InsertStorageAllocationAsync(
                connection,
                transaction,
                goodsReceiptItemId,
                inventoryClassId == 1 ? "Fabric" : "UsedTool",
                receipt.Quantity,
                sourceOperationId,
                itemId,
                transactionId,
                accountingEvent.AccountingEventId,
                postingId,
                null,
                ct);
            await transaction.CommitAsync(ct);

            return new InventoryFoundationPostingResultDto(
                postingId,
                itemId,
                itemCode.Trim(),
                receipt.Quantity,
                operationalAmount,
                postingAmount,
                accountingEvent.AccountingEventId,
                transactionId,
                false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public static async Task<InventoryFoundationPostingResultDto> ConsumeFabricCodeInventoryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ConsumeFabricCodeInventoryDto request,
        CancellationToken ct)
    {
        if (transaction.Connection != connection)
            throw new ArgumentException("يجب صرف القماش داخل معاملة حفظ المصدر نفسه.");
        if (request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.FabricCode) || request.QuantityInches <= 0m)
            throw new ArgumentException("كود القماش والكمية ومعرف عملية الصرف مطلوبة.");

        var tailoringSource = request.OrderItemId is > 0
            && request.ReadyMadeProductionOrderItemId is null
            && request.ReadyMadeProductionOrderPieceInstanceId is null;
        var readyMadeSource = request.ReadyMadeProductionOrderItemId is > 0
            && request.OrderItemId is null && request.PieceId is null;
        if (!tailoringSource && !readyMadeSource)
            throw new ArgumentException("يجب ربط صرف القماش ببند تفصيل أو بند إنتاج جاهز واحد.");

        var fabricCode = request.FabricCode.Trim().ToUpperInvariant();
        const string existingSql = @"
            SELECT s.FabricConsumptionSourceId,s.InventoryItemId,i.ItemCode,s.ConsumedQuantity,
                   s.OperationalAmount,s.PostingAmount,s.AccountingEventId,s.InventoryTransactionId,
                   s.UnitId,s.OrderItemId,s.PieceId,s.ReadyMadeProductionOrderItemId,
                   s.ReadyMadeProductionOrderPieceInstanceId,s.FabricRollId
            FROM dbo.FabricConsumptionSources s WITH (UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=s.InventoryItemId
            WHERE s.SourceOperationId=@operationId";
        await using (var existingCommand = new SqlCommand(existingSql, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("@operationId", request.SourceOperationId);
            await using var existingReader = await existingCommand.ExecuteReaderAsync(ct);
            if (await existingReader.ReadAsync(ct))
            {
                var expectedQuantity = Math.Round(request.QuantityInches / (existingReader.GetInt16(8) == 1 ? 36m : 1m), 6, MidpointRounding.AwayFromZero);
                if (!string.Equals(existingReader.GetString(2), fabricCode, StringComparison.OrdinalIgnoreCase)
                    || existingReader.GetDecimal(3) != expectedQuantity
                    || existingReader.NullableInt32("OrderItemId") != request.OrderItemId
                    || existingReader.NullableInt32("PieceId") != request.PieceId
                    || existingReader.NullableInt32("ReadyMadeProductionOrderItemId") != request.ReadyMadeProductionOrderItemId
                    || existingReader.NullableInt32("ReadyMadeProductionOrderPieceInstanceId") != request.ReadyMadeProductionOrderPieceInstanceId
                    || !existingReader.IsDBNull(13))
                    throw new InvalidOperationException("معرف العملية مستخدم لصرف قماش ببيانات مختلفة.");
                if (existingReader.IsDBNull(6) || existingReader.IsDBNull(7))
                    throw new InvalidOperationException("روابط حركة صرف القماش السابقة غير مكتملة.");
                return new InventoryFoundationPostingResultDto(
                    existingReader.GetInt64(0), existingReader.GetInt32(1), existingReader.GetString(2),
                    existingReader.GetDecimal(3), existingReader.GetDecimal(4), existingReader.GetDecimal(5),
                    existingReader.GetInt64(6), existingReader.GetInt32(7), true);
            }
        }

        var sourceSql = tailoringSource
            ? @"SELECT 1 FROM dbo.OrderItems WITH (UPDLOCK,HOLDLOCK)
                WHERE OrderItemID=@itemId AND FabricCode=@fabricCode
                  AND (@pieceId IS NULL OR EXISTS
                      (SELECT 1 FROM dbo.Pieces WHERE PieceID=@pieceId AND OrderItemID=@itemId))"
            : @"SELECT 1 FROM dbo.ReadyMadeProductionOrderItems WITH (UPDLOCK,HOLDLOCK)
                WHERE ReadyMadeProductionOrderItemId=@itemId AND FabricCode=@fabricCode
                  AND (@pieceId IS NULL OR EXISTS
                      (SELECT 1 FROM dbo.ReadyMadeProductionOrderPieceInstances
                       WHERE ReadyMadeProductionOrderPieceInstanceId=@pieceId AND ReadyMadeProductionOrderItemId=@itemId))";
        await using (var sourceCommand = new SqlCommand(sourceSql, connection, transaction))
        {
            sourceCommand.Parameters.AddWithValue("@itemId", request.OrderItemId ?? request.ReadyMadeProductionOrderItemId!.Value);
            sourceCommand.Parameters.Add("@pieceId", System.Data.SqlDbType.Int).Value = (object?)(request.PieceId ?? request.ReadyMadeProductionOrderPieceInstanceId) ?? DBNull.Value;
            sourceCommand.Parameters.AddWithValue("@fabricCode", fabricCode);
            if (await sourceCommand.ExecuteScalarAsync(ct) is null)
                throw new InvalidOperationException("كود القماش أو القطعة لا يطابق بند الصرف الرسمي.");
        }

        const string stockSql = @"
            SELECT i.InventoryItemID,f.UnitId,f.AvailableQuantity,f.OfficialUnitCost
            FROM dbo.InventoryItems i WITH (UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.InventoryItemFoundation f WITH (UPDLOCK,HOLDLOCK)
                ON f.InventoryItemId=i.InventoryItemID AND f.InventoryClassId=1
            WHERE i.ItemCode=@fabricCode AND i.IsActive=1";
        await using var stockCommand = new SqlCommand(stockSql, connection, transaction);
        stockCommand.Parameters.AddWithValue("@fabricCode", fabricCode);
        await using var stockReader = await stockCommand.ExecuteReaderAsync(ct);
        if (!await stockReader.ReadAsync(ct))
            throw new ArgumentException($"كود القماش {fabricCode} غير مرتبط بمخزون قماش رسمي فعال.");
        var inventoryItemId = stockReader.GetInt32(0);
        var unitId = stockReader.GetInt16(1);
        var availableQuantity = stockReader.GetDecimal(2);
        var unitCost = stockReader.GetDecimal(3);
        await stockReader.DisposeAsync();
        if (unitId is not (1 or 2))
            throw new InvalidOperationException("وحدة مخزون القماش ليست ياردة أو بوصة معتمدة.");
        var unitFactor = unitId == 1 ? 36m : 1m;
        if (request.QuantityInches > availableQuantity * unitFactor)
            throw new ArgumentException($"الكمية المتاحة للقماش {fabricCode} غير كافية. المتاح: {availableQuantity * unitFactor:0.######} بوصة، المطلوب: {request.QuantityInches:0.######} بوصة.");
        var quantity = Math.Round(request.QuantityInches / unitFactor, 6, MidpointRounding.AwayFromZero);
        var operationalAmount = Math.Round(quantity * unitCost, 6, MidpointRounding.AwayFromZero);
        var postingAmount = Math.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
        if (quantity <= 0m || postingAmount <= 0m)
            throw new InvalidOperationException("كمية أو قيمة صرف القماش أقل من الدقة المحاسبية المعتمدة.");

        var now = DateTime.UtcNow;
        const string insertSql = @"
            INSERT dbo.FabricConsumptionSources
                (SourceOperationId,InventoryItemId,OrderItemId,PieceId,
                 ReadyMadeProductionOrderItemId,ReadyMadeProductionOrderPieceInstanceId,
                 ConsumedQuantity,UnitId,OfficialUnitCost,OperationalAmount,PostingAmount,ConfirmedByUserId,ConfirmedAt)
            OUTPUT INSERTED.FabricConsumptionSourceId
            VALUES (@operationId,@inventoryItemId,@orderItemId,@pieceId,@readyItemId,@readyPieceId,
                    @quantity,@unitId,@unitCost,@operationalAmount,@postingAmount,@userId,@now)";
        await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("@operationId", request.SourceOperationId);
        insertCommand.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
        AddNullable(insertCommand, "@orderItemId", request.OrderItemId);
        AddNullable(insertCommand, "@pieceId", request.PieceId);
        AddNullable(insertCommand, "@readyItemId", request.ReadyMadeProductionOrderItemId);
        AddNullable(insertCommand, "@readyPieceId", request.ReadyMadeProductionOrderPieceInstanceId);
        AddDecimal(insertCommand, "@quantity", quantity);
        insertCommand.Parameters.AddWithValue("@unitId", unitId);
        AddDecimal(insertCommand, "@unitCost", unitCost);
        AddDecimal(insertCommand, "@operationalAmount", operationalAmount);
        AddDecimal(insertCommand, "@postingAmount", postingAmount, 2);
        AddNullable(insertCommand, "@userId", request.ConfirmedByUserId);
        insertCommand.Parameters.AddWithValue("@now", now);
        var sourceId = Convert.ToInt64(await insertCommand.ExecuteScalarAsync(ct));

        await UpdateFabricInventoryAsync(connection, transaction, inventoryItemId, null, quantity, operationalAmount, now, ct);
        var reference = $"FabricConsumption:{sourceId}";
        var transactionId = await InsertFoundationInventoryTransactionAsync(
            connection, transaction, inventoryItemId, "FabricInventoryConsumed", quantity,
            reference, -operationalAmount, unitCost, request.SourceOperationId, now, ct);
        var accountingEvent = await AccountingEventPostingGateway.PostFabricConsumptionAsync(
            connection, transaction, sourceId, postingAmount, reference, $"صرف القماش {fabricCode}", ct);
        await LinkFabricConsumptionArtifactsAsync(connection, transaction, sourceId, transactionId, accountingEvent.AccountingEventId, ct);
        return new InventoryFoundationPostingResultDto(
            sourceId, inventoryItemId, fabricCode, quantity, operationalAmount, postingAmount,
            accountingEvent.AccountingEventId, transactionId, false);
    }

    public async Task<InventoryFoundationPostingResultDto?> ConsumeFabricInventoryAsync(ConsumeFabricInventoryDto request, CancellationToken ct)
    {
        if (request.SourceOperationId == Guid.Empty)
            throw new ArgumentException("SourceOperationId is required for idempotent inventory posting.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var existing = await TryReadExistingFabricConsumptionAsync(connection, transaction, request, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing with { IsExisting = true };
            }

            var roll = await ReadFabricRollForUpdateAsync(connection, transaction, request.FabricRollId, ct)
                ?? throw new InvalidOperationException("Fabric roll was not found in the foundation inventory.");
            if (request.Quantity <= 0m || request.Quantity > roll.AvailableQuantity)
                throw new InvalidOperationException("The requested fabric quantity exceeds the available roll quantity.");

            await ValidateOrderFabricSourceAsync(connection, transaction, request.OrderItemId, request.PieceId, ct);
            var operationalAmount = Math.Round(request.Quantity * roll.OfficialUnitCost, 6, MidpointRounding.AwayFromZero);
            var postingAmount = Math.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
            if (operationalAmount <= 0m || postingAmount <= 0m)
                throw new InvalidOperationException("The fabric consumption amount must be positive.");

            var now = DateTime.UtcNow;
            var sourceId = await InsertFabricConsumptionSourceAsync(
                connection,
                transaction,
                request,
                roll,
                operationalAmount,
                postingAmount,
                now,
                ct);

            await UpdateFabricInventoryAsync(
                connection,
                transaction,
                roll.InventoryItemId,
                roll.FabricRollId,
                request.Quantity,
                operationalAmount,
                now,
                ct);

            var reference = $"OrderItem:{request.OrderItemId}:FabricRoll:{request.FabricRollId}";
            var transactionId = await InsertFoundationInventoryTransactionAsync(
                connection,
                transaction,
                roll.InventoryItemId,
                "FabricInventoryConsumed",
                request.Quantity,
                reference,
                -operationalAmount,
                roll.OfficialUnitCost,
                request.SourceOperationId,
                now,
                ct);

            var accountingEvent = await AccountingEventPostingGateway.PostFabricConsumptionAsync(
                connection,
                transaction,
                sourceId,
                postingAmount,
                reference,
                $"Confirmed fabric consumption for order item {request.OrderItemId}",
                ct);

            await LinkFabricConsumptionArtifactsAsync(connection, transaction, sourceId, transactionId, accountingEvent.AccountingEventId, ct);
            await transaction.CommitAsync(ct);

            return new InventoryFoundationPostingResultDto(
                sourceId,
                roll.InventoryItemId,
                roll.ItemCode,
                request.Quantity,
                operationalAmount,
                postingAmount,
                accountingEvent.AccountingEventId,
                transactionId,
                false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<InventoryFoundationPostingResultDto?> ConsumeConsumableInventoryAsync(ConsumeConsumableInventoryDto request, CancellationToken ct)
    {
        if (request.SourceOperationId == Guid.Empty)
            throw new ArgumentException("SourceOperationId is required for idempotent inventory posting.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var existing = await TryReadExistingConsumableConsumptionAsync(connection, transaction, request, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return existing with { IsExisting = true };
            }

            var item = await ReadConsumableItemForUpdateAsync(connection, transaction, request.InventoryItemId, request.UnitId, ct)
                ?? throw new InvalidOperationException("The inventory item is not an active consumable foundation item.");
            if (request.Quantity <= 0m || request.Quantity > item.AvailableQuantity)
                throw new InvalidOperationException("The requested consumable quantity exceeds the available quantity.");

            await ValidateProductionSourceAsync(connection, transaction, request.ProductionOrderId, request.ProductionBatchId, request.ProductMaterialId, request.InventoryItemId, ct);
            var operationalAmount = Math.Round(request.Quantity * item.OfficialUnitCost, 6, MidpointRounding.AwayFromZero);
            var postingAmount = Math.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
            if (operationalAmount <= 0m || postingAmount <= 0m)
                throw new InvalidOperationException("The consumable consumption amount must be positive.");

            var now = DateTime.UtcNow;
            var sourceId = await InsertConsumableConsumptionSourceAsync(
                connection,
                transaction,
                request,
                item.OfficialUnitCost,
                operationalAmount,
                postingAmount,
                now,
                ct);

            await UpdateConsumableFoundationAsync(connection, transaction, item, request.Quantity, operationalAmount, now, ct);
            var reference = $"ProductionOrder:{request.ProductionOrderId}:InventoryItem:{request.InventoryItemId}";
            var transactionId = await InsertFoundationInventoryTransactionAsync(
                connection,
                transaction,
                request.InventoryItemId,
                "ConsumableInventoryConsumed",
                request.Quantity,
                reference,
                -operationalAmount,
                item.OfficialUnitCost,
                request.SourceOperationId,
                now,
                ct);
            await LinkConsumptionTransactionAsync(connection, transaction, sourceId, transactionId, ct);

            var accountingEvent = await AccountingEventPostingGateway.PostConsumableConsumptionAsync(
                connection,
                transaction,
                sourceId,
                postingAmount,
                reference,
                $"Confirmed consumable consumption for production order {request.ProductionOrderId}",
                ct);

            await LinkConsumableConsumptionArtifactsAsync(connection, transaction, sourceId, transactionId, accountingEvent.AccountingEventId, ct);
            await transaction.CommitAsync(ct);

            return new InventoryFoundationPostingResultDto(
                sourceId,
                request.InventoryItemId,
                item.ItemCode,
                request.Quantity,
                operationalAmount,
                postingAmount,
                accountingEvent.AccountingEventId,
                transactionId,
                false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<InventoryFoundationPostingResultDto?> TryReadExistingReceiptAsync(SqlConnection connection, SqlTransaction transaction, int expectedGoodsReceiptItemId, byte expectedClassId, short expectedUnitId, string expectedItemCode, Guid sourceOperationId, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP (1) rp.InventoryReceiptPostingId, rp.InventoryClassId, rp.GoodsReceiptItemId, l.UnitId,
                   l.InventoryItemId, i.ItemCode, l.ReceivedQuantity, rp.OperationalAmount, rp.PostingAmount,
                   l.InventoryTransactionId, rp.AccountingEventId
            FROM dbo.InventoryReceiptPostings rp WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.InventoryReceiptLines l ON l.InventoryReceiptPostingId = rp.InventoryReceiptPostingId
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = l.InventoryItemId
            WHERE rp.SourceOperationId = @sourceOperationId
               OR (rp.GoodsReceiptItemId = @goodsReceiptItemId
                   AND rp.InventoryClassId = @classId
                   AND l.UnitId = @unitId
                   AND i.ItemCode = @itemCode)
            ORDER BY CASE WHEN rp.SourceOperationId = @sourceOperationId THEN 0 ELSE 1 END, rp.InventoryReceiptPostingId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        command.Parameters.AddWithValue("@goodsReceiptItemId", expectedGoodsReceiptItemId);
        command.Parameters.AddWithValue("@classId", expectedClassId);
        command.Parameters.AddWithValue("@unitId", expectedUnitId);
        command.Parameters.AddWithValue("@itemCode", expectedItemCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetByte(1) != expectedClassId)
            throw new InvalidOperationException("The source operation was already used by another inventory class.");
        if (reader.GetInt32(2) != expectedGoodsReceiptItemId || reader.GetInt16(3) != expectedUnitId || !string.Equals(reader.GetString(5), expectedItemCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The source operation was already used by another inventory receipt source.");
        if (reader.IsDBNull(9) || reader.IsDBNull(10))
            throw new InvalidOperationException("The existing inventory receipt posting is incomplete.");
        return new InventoryFoundationPostingResultDto(reader.GetInt64(0), reader.GetInt32(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetInt64(10), reader.GetInt32(9), true);
    }

    private static async Task<IncompleteReceiptState?> TryReadIncompleteReceiptAsync(SqlConnection connection, SqlTransaction transaction, int expectedGoodsReceiptItemId, byte expectedClassId, short expectedUnitId, string expectedItemCode, Guid sourceOperationId, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP (1) rp.InventoryReceiptPostingId, rp.SourceOperationId, rp.GoodsReceiptItemId, rp.InventoryClassId, l.UnitId,
                   l.InventoryItemId, i.ItemCode, l.ReceivedQuantity, rp.OperationalAmount, rp.PostingAmount,
                   l.InventoryTransactionId, rp.AccountingEventId
            FROM dbo.InventoryReceiptPostings rp WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.InventoryReceiptLines l ON l.InventoryReceiptPostingId = rp.InventoryReceiptPostingId
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = l.InventoryItemId
            LEFT JOIN dbo.GoodsReceiptItemStorageAllocations sga ON sga.InventoryReceiptPostingId = rp.InventoryReceiptPostingId
            WHERE rp.GoodsReceiptItemId = @goodsReceiptItemId
              AND rp.InventoryClassId = @classId
              AND sga.StorageAllocationId IS NULL";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@goodsReceiptItemId", expectedGoodsReceiptItemId);
        command.Parameters.AddWithValue("@classId", expectedClassId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var actualSourceOperationId = reader.GetGuid(1);
        if (actualSourceOperationId != sourceOperationId)
            throw new InvalidOperationException("A partial inventory receipt already exists for this goods receipt item and uses a different source operation id. The partial state must be reconciled before retrying.");
        if (reader.GetByte(3) != expectedClassId)
            throw new InvalidOperationException("A partial inventory receipt exists for the same receipt item but with a different inventory class.");
        if (reader.GetInt32(2) != expectedGoodsReceiptItemId || reader.GetInt16(4) != expectedUnitId || !string.Equals(reader.GetString(6), expectedItemCode.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A partial inventory receipt exists for the same receipt item but with conflicting item or unit details.");
        if (reader.IsDBNull(10) || reader.IsDBNull(11))
            throw new InvalidOperationException("The existing partial inventory receipt posting is incomplete and cannot be safely completed.");

        return new IncompleteReceiptState(
            reader.GetInt64(0),
            reader.GetInt32(2),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetInt64(11),
            reader.GetInt32(10),
            reader.GetByte(3),
            reader.GetInt16(4),
            actualSourceOperationId);
    }

    private static async Task<InventoryFoundationPostingResultDto> CompleteIncompleteReceiptStorageAsync(SqlConnection connection, SqlTransaction transaction, IncompleteReceiptState state, CancellationToken ct)
    {
        await using var allocationExists = new SqlCommand(@"SELECT COUNT_BIG(1) FROM dbo.GoodsReceiptItemStorageAllocations WITH (UPDLOCK, HOLDLOCK) WHERE InventoryReceiptPostingId = @postingId", connection, transaction);
        allocationExists.Parameters.AddWithValue("@postingId", state.SourceRecordId);
        if (Convert.ToInt64(await allocationExists.ExecuteScalarAsync(ct)) > 0)
        {
            return new InventoryFoundationPostingResultDto(state.SourceRecordId, state.InventoryItemId, state.ItemCode, state.Quantity, state.OperationalAmount, state.PostingAmount, state.AccountingEventId, state.InventoryTransactionId, true);
        }

        await InsertStorageAllocationAsync(
            connection,
            transaction,
            state.GoodsReceiptItemId,
            state.InventoryClassId == 1 ? "Fabric" : "UsedTool",
            state.Quantity,
            state.SourceOperationId,
            state.InventoryItemId,
            state.InventoryTransactionId,
            state.AccountingEventId,
            state.SourceRecordId,
            null,
            ct);

        return new InventoryFoundationPostingResultDto(state.SourceRecordId, state.InventoryItemId, state.ItemCode, state.Quantity, state.OperationalAmount, state.PostingAmount, state.AccountingEventId, state.InventoryTransactionId, true);
    }

    private static async Task<GoodsReceiptSource?> ReadGoodsReceiptSourceAsync(SqlConnection connection, SqlTransaction transaction, int goodsReceiptItemId, CancellationToken ct)
    {
        const string sql = @"
            SELECT gri.ReceivedQuantity, gri.UnitCost, gr.ReceiptNumber, gri.ItemName
            FROM dbo.GoodsReceiptItems gri WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.GoodsReceipts gr WITH (UPDLOCK, HOLDLOCK) ON gr.GoodsReceiptId = gri.GoodsReceiptId
            WHERE gri.GoodsReceiptItemId = @goodsReceiptItemId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@goodsReceiptItemId", goodsReceiptItemId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new GoodsReceiptSource(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    private static async Task<string> ReadUnitCodeAsync(SqlConnection connection, SqlTransaction transaction, short unitId, byte inventoryClassId, CancellationToken ct)
    {
        const string sql = "SELECT Code FROM dbo.InventoryUnitCatalog WITH (HOLDLOCK) WHERE UnitId=@unitId AND ((@classId=1 AND UnitId IN (1,2)) OR (@classId=2 AND UnitId=3))";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@unitId", unitId);
        command.Parameters.AddWithValue("@classId", inventoryClassId);
        var result = await command.ExecuteScalarAsync(ct) as string;
        return result ?? throw new ArgumentException("The requested unit is not valid for the inventory class.");
    }

    private static async Task<int> ReadOpposingLedgerAccountIdAsync(SqlConnection connection, SqlTransaction transaction, string accountCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountCode) || accountCode.Trim() is "1101" or "1102" or "5300")
            throw new ArgumentException("A verified non-inventory opposing ledger account is required.");
        const string sql = "SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountCode=@accountCode AND IsActive=1";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@accountCode", accountCode.Trim());
        var result = await command.ExecuteScalarAsync(ct);
        return result is int accountId ? accountId : throw new InvalidOperationException("The opposing ledger account is not active or does not exist.");
    }

    private static async Task<FoundationItemState?> ReadFoundationItemAsync(SqlConnection connection, SqlTransaction transaction, string itemCode, CancellationToken ct)
    {
        const string sql = @"
            SELECT i.InventoryItemID, f.InventoryClassId, f.UnitId, f.OriginalQuantity, f.AvailableQuantity,
                   f.ConsumedQuantity, f.OperationalValue
            FROM dbo.InventoryItems i WITH (UPDLOCK, HOLDLOCK)
            LEFT JOIN dbo.InventoryItemFoundation f WITH (UPDLOCK, HOLDLOCK) ON f.InventoryItemId = i.InventoryItemID
            WHERE i.ItemCode=@itemCode";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemCode", itemCode);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new FoundationItemState(
            reader.GetInt32(0),
            !reader.IsDBNull(1),
            reader.IsDBNull(1) ? null : reader.GetByte(1),
            reader.IsDBNull(2) ? null : reader.GetInt16(2),
            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
            reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
            reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
            reader.IsDBNull(6) ? 0m : reader.GetDecimal(6));
    }

    private static async Task<int> InsertFoundationInventoryItemAsync(SqlConnection connection, SqlTransaction transaction, string itemCode, string itemName, byte inventoryClassId, string unitCode, short unitId, decimal quantity, decimal operationalAmount, string? fabricTypeCode, string? colorValue, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            DECLARE @itemId int;
            INSERT INTO dbo.InventoryItems
                (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
            VALUES
                (@itemCode, @itemName, N'Foundation', @unitCode, @quantity, @quantity, 0, 1, @now, @now, NULL, @fabricTypeCode, @colorValue, NULL, NULL,
                 CASE WHEN @classId=1 AND @unitId=2 THEN @unitCost ELSE NULL END,
                 CASE WHEN @classId=1 AND @unitId=1 THEN @unitCost WHEN @classId=1 AND @unitId=2 THEN @unitCost*36 ELSE NULL END);
            SET @itemId = CONVERT(int, SCOPE_IDENTITY());
            INSERT INTO dbo.InventoryItemFoundation
                (InventoryItemId,InventoryClassId,UnitId,CurrencyCode,OriginalQuantity,AvailableQuantity,ConsumedQuantity,OfficialUnitCost,OperationalValue,CreatedAt,UpdatedAt)
            VALUES
                (@itemId,@classId,@unitId,'YER',@quantity,@quantity,0,@unitCost,@operationalAmount,@now,@now);
            SELECT @itemId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemCode", itemCode);
        command.Parameters.AddWithValue("@itemName", itemName);
        command.Parameters.AddWithValue("@unitCode", unitCode);
        AddDecimal(command, "@quantity", quantity);
        command.Parameters.AddWithValue("@now", now);
        AddNullable(command, "@fabricTypeCode", fabricTypeCode);
        AddNullable(command, "@colorValue", colorValue);
        command.Parameters.AddWithValue("@classId", inventoryClassId);
        command.Parameters.AddWithValue("@unitId", unitId);
        AddDecimal(command, "@unitCost", quantity == 0m ? 0m : operationalAmount / quantity);
        AddDecimal(command, "@operationalAmount", operationalAmount);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task UpdateFoundationInventoryItemAsync(SqlConnection connection, SqlTransaction transaction, int itemId, decimal quantity, decimal operationalAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            UPDATE i
            SET CurrentQuantity=i.CurrentQuantity+@quantity,
                AvailableQuantity=i.AvailableQuantity+@quantity,
                UpdatedAt=@now
            FROM dbo.InventoryItems i
            WHERE i.InventoryItemID=@itemId;
            UPDATE f
            SET OriginalQuantity=f.OriginalQuantity+@quantity,
                AvailableQuantity=f.AvailableQuantity+@quantity,
                OperationalValue=f.OperationalValue+@operationalAmount,
                OfficialUnitCost=CONVERT(decimal(18,6), (f.OperationalValue+@operationalAmount)/(f.OriginalQuantity+@quantity)),
                UpdatedAt=@now
            FROM dbo.InventoryItemFoundation f
            WHERE f.InventoryItemId=@itemId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        AddDecimal(command, "@quantity", quantity);
        AddDecimal(command, "@operationalAmount", operationalAmount);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@itemId", itemId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> InsertInventoryReceiptPostingAsync(SqlConnection connection, SqlTransaction transaction, int goodsReceiptItemId, byte inventoryClassId, int opposingLedgerAccountId, Guid sourceOperationId, decimal operationalAmount, decimal postingAmount, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.InventoryReceiptPostings
                (GoodsReceiptItemId, InventoryClassId, AccountingBasisCode, OpposingLedgerAccountId, SourceOperationId, OperationalAmount, PostingAmount)
            OUTPUT INSERTED.InventoryReceiptPostingId
            VALUES (@goodsReceiptItemId,@classId,1,@opposingAccountId,@sourceOperationId,@operationalAmount,@postingAmount)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@goodsReceiptItemId", goodsReceiptItemId);
        command.Parameters.AddWithValue("@classId", inventoryClassId);
        command.Parameters.AddWithValue("@opposingAccountId", opposingLedgerAccountId);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        AddDecimal(command, "@operationalAmount", operationalAmount);
        AddDecimal(command, "@postingAmount", postingAmount, 2);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<long> InsertInventoryReceiptLineAsync(SqlConnection connection, SqlTransaction transaction, long postingId, int itemId, decimal quantity, decimal unitCost, short unitId, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.InventoryReceiptLines (InventoryReceiptPostingId,InventoryItemId,ReceivedQuantity,OfficialUnitCost,UnitId)
            OUTPUT INSERTED.InventoryReceiptLineId
            VALUES (@postingId,@itemId,@quantity,@unitCost,@unitId)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@postingId", postingId);
        command.Parameters.AddWithValue("@itemId", itemId);
        AddDecimal(command, "@quantity", quantity);
        AddDecimal(command, "@unitCost", unitCost);
        command.Parameters.AddWithValue("@unitId", unitId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<long> InsertFabricRollAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string rollCode, string fabricTypeCode, string? colorValue, decimal quantity, decimal unitCost, short unitId, long lineId, DateTime now, CancellationToken ct)
    {
        const string selectSql = @"SELECT FabricRollId, InventoryItemId FROM dbo.FabricRolls WITH (UPDLOCK, HOLDLOCK) WHERE RollCode = @rollCode;";
        await using (var lookup = new SqlCommand(selectSql, connection, transaction))
        {
            lookup.Parameters.AddWithValue("@rollCode", rollCode);
            await using var existingReader = await lookup.ExecuteReaderAsync(ct);
            if (await existingReader.ReadAsync(ct))
            {
                var existingRollId = existingReader.GetInt64(0);
                var existingItemId = existingReader.GetInt32(1);
                if (existingItemId != itemId)
                    throw new InvalidOperationException($"Fabric roll code '{rollCode}' already belongs to a different inventory item.");
                return existingRollId;
            }
        }

        const string sql = @"
            INSERT INTO dbo.FabricRolls
                (InventoryItemId,RollCode,FabricTypeCode,ColorValue,OriginalQuantity,AvailableQuantity,ConsumedQuantity,UnitId,OfficialUnitCost,CurrencyCode,InventoryReceiptLineId,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.FabricRollId
            VALUES (@itemId,@rollCode,@fabricTypeCode,@colorValue,@quantity,@quantity,0,@unitId,@unitCost,'YER',@lineId,@now,@now)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@rollCode", rollCode);
        command.Parameters.AddWithValue("@fabricTypeCode", fabricTypeCode);
        AddNullable(command, "@colorValue", colorValue);
        AddDecimal(command, "@quantity", quantity);
        command.Parameters.AddWithValue("@unitId", unitId);
        AddDecimal(command, "@unitCost", unitCost);
        command.Parameters.AddWithValue("@lineId", lineId);
        command.Parameters.AddWithValue("@now", now);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task SetReceiptLineRollAsync(SqlConnection connection, SqlTransaction transaction, long lineId, long rollId, CancellationToken ct)
    {
        await using var command = new SqlCommand("UPDATE dbo.InventoryReceiptLines SET FabricRollId=@rollId WHERE InventoryReceiptLineId=@lineId", connection, transaction);
        command.Parameters.AddWithValue("@rollId", rollId);
        command.Parameters.AddWithValue("@lineId", lineId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> InsertFoundationInventoryTransactionAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string transactionType, decimal quantity, string reference, decimal operationalCostImpact, decimal unitCost, Guid sourceOperationId, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID,TransactionType,Quantity,ReferenceNumber,Notes,CreatedAt,TotalCostImpact,UnitCost,SourceOperationId,OperationalCostImpact)
            OUTPUT INSERTED.TransactionID
            VALUES (@itemId,@transactionType,@quantity,@reference,@notes,@now,@cost,@unitCost,@sourceOperationId,@cost)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        AddDecimal(command, "@quantity", quantity);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@notes", "Official fabric and consumables foundation movement");
        command.Parameters.AddWithValue("@now", now);
        AddDecimal(command, "@cost", operationalCostImpact);
        AddDecimal(command, "@unitCost", unitCost);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task LinkInventoryReceiptArtifactsAsync(SqlConnection connection, SqlTransaction transaction, long lineId, int transactionId, long accountingEventId, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.InventoryReceiptLines SET InventoryTransactionId=@transactionId,AccountingEventId=@accountingEventId WHERE InventoryReceiptLineId=@lineId; UPDATE dbo.InventoryTransactions SET AccountingEventId=@accountingEventId WHERE TransactionID=@transactionId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@lineId", lineId);
        command.Parameters.AddWithValue("@transactionId", transactionId);
        command.Parameters.AddWithValue("@accountingEventId", accountingEventId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<FabricRollState?> ReadFabricRollForUpdateAsync(SqlConnection connection, SqlTransaction transaction, long rollId, CancellationToken ct)
    {
        const string sql = @"
            SELECT fr.FabricRollId, fr.InventoryItemId, i.ItemCode, fr.AvailableQuantity, fr.UnitId, fr.OfficialUnitCost
            FROM dbo.FabricRolls fr WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.InventoryItems i WITH (UPDLOCK, HOLDLOCK) ON i.InventoryItemID=fr.InventoryItemId AND i.IsActive=1
            INNER JOIN dbo.InventoryItemFoundation f WITH (UPDLOCK, HOLDLOCK) ON f.InventoryItemId=fr.InventoryItemId AND f.InventoryClassId=1
            WHERE fr.FabricRollId=@rollId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@rollId", rollId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new FabricRollState(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetInt16(4), reader.GetDecimal(5))
            : null;
    }

    private static async Task ValidateOrderFabricSourceAsync(SqlConnection connection, SqlTransaction transaction, int orderItemId, int? pieceId, CancellationToken ct)
    {
        const string itemSql = "SELECT OrderID FROM dbo.OrderItems WITH (UPDLOCK,HOLDLOCK) WHERE OrderItemID=@orderItemId";
        await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
        itemCommand.Parameters.AddWithValue("@orderItemId", orderItemId);
        if (await itemCommand.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("The order item source does not exist.");
        if (pieceId is null) return;
        const string pieceSql = "SELECT 1 FROM dbo.Pieces WITH (UPDLOCK,HOLDLOCK) WHERE PieceID=@pieceId AND OrderItemID=@orderItemId";
        await using var pieceCommand = new SqlCommand(pieceSql, connection, transaction);
        pieceCommand.Parameters.AddWithValue("@pieceId", pieceId.Value);
        pieceCommand.Parameters.AddWithValue("@orderItemId", orderItemId);
        if (await pieceCommand.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("The piece source does not belong to the order item.");
    }

    private static async Task<long> InsertFabricConsumptionSourceAsync(SqlConnection connection, SqlTransaction transaction, ConsumeFabricInventoryDto request, FabricRollState roll, decimal operationalAmount, decimal postingAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.FabricConsumptionSources
                (SourceOperationId,InventoryItemId,FabricRollId,OrderItemId,PieceId,ConsumedQuantity,UnitId,OfficialUnitCost,OperationalAmount,PostingAmount,ConfirmedByUserId,ConfirmedAt)
            OUTPUT INSERTED.FabricConsumptionSourceId
            VALUES (@sourceOperationId,@itemId,@rollId,@orderItemId,@pieceId,@quantity,@unitId,@unitCost,@operationalAmount,@postingAmount,@userId,@now)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        command.Parameters.AddWithValue("@itemId", roll.InventoryItemId);
        command.Parameters.AddWithValue("@rollId", roll.FabricRollId);
        command.Parameters.AddWithValue("@orderItemId", request.OrderItemId);
        AddNullable(command, "@pieceId", request.PieceId);
        AddDecimal(command, "@quantity", request.Quantity);
        command.Parameters.AddWithValue("@unitId", roll.UnitId);
        AddDecimal(command, "@unitCost", roll.OfficialUnitCost);
        AddDecimal(command, "@operationalAmount", operationalAmount);
        AddDecimal(command, "@postingAmount", postingAmount, 2);
        AddNullable(command, "@userId", request.ConfirmedByUserId);
        command.Parameters.AddWithValue("@now", now);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private static async Task UpdateFabricInventoryAsync(SqlConnection connection, SqlTransaction transaction, int inventoryItemId, long? fabricRollId, decimal quantity, decimal operationalAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            IF @rollId IS NOT NULL
            BEGIN
                UPDATE dbo.FabricRolls
                SET AvailableQuantity=AvailableQuantity-@quantity, ConsumedQuantity=ConsumedQuantity+@quantity, UpdatedAt=@now
                WHERE FabricRollId=@rollId AND AvailableQuantity>=@quantity;
                IF @@ROWCOUNT <> 1 THROW 51440, N'Fabric roll availability changed before consumption.', 1;
            END;
            UPDATE dbo.InventoryItems
            SET CurrentQuantity=CurrentQuantity-@quantity, AvailableQuantity=AvailableQuantity-@quantity, UpdatedAt=@now
            WHERE InventoryItemID=@itemId AND AvailableQuantity>=@quantity;
            IF @@ROWCOUNT <> 1 THROW 51441, N'Inventory item availability changed before consumption.', 1;
            UPDATE dbo.InventoryItemFoundation
            SET AvailableQuantity=AvailableQuantity-@quantity, ConsumedQuantity=ConsumedQuantity+@quantity, OperationalValue=OperationalValue-@operationalAmount, UpdatedAt=@now
            WHERE InventoryItemId=@itemId AND AvailableQuantity>=@quantity AND OperationalValue>=@operationalAmount;
            IF @@ROWCOUNT <> 1 THROW 51442, N'Foundation inventory availability changed before consumption.', 1;";
        await using var command = new SqlCommand(sql, connection, transaction);
        AddDecimal(command, "@quantity", quantity);
        AddDecimal(command, "@operationalAmount", operationalAmount);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.Add("@rollId", System.Data.SqlDbType.BigInt).Value = (object?)fabricRollId ?? DBNull.Value;
        command.Parameters.AddWithValue("@itemId", inventoryItemId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<InventoryFoundationPostingResultDto?> TryReadExistingFabricConsumptionAsync(SqlConnection connection, SqlTransaction transaction, ConsumeFabricInventoryDto request, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP (1) s.FabricConsumptionSourceId,s.InventoryItemId,i.ItemCode,s.ConsumedQuantity,s.OperationalAmount,s.PostingAmount,s.AccountingEventId,s.InventoryTransactionId,
                   s.FabricRollId,s.OrderItemId,s.PieceId
            FROM dbo.FabricConsumptionSources s WITH (UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=s.InventoryItemId
            WHERE s.SourceOperationId=@sourceOperationId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetDecimal(3) != request.Quantity || reader.GetInt64(8) != request.FabricRollId || reader.GetInt32(9) != request.OrderItemId || (request.PieceId is null ? !reader.IsDBNull(10) : reader.IsDBNull(10) || reader.GetInt32(10) != request.PieceId.Value))
            throw new InvalidOperationException("The source operation was already used with different fabric consumption details.");
        if (reader.IsDBNull(6) || reader.IsDBNull(7))
            throw new InvalidOperationException("The existing fabric consumption posting is incomplete.");
        return new InventoryFoundationPostingResultDto(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetInt64(6), reader.GetInt32(7), true);
    }

    private static async Task<ConsumableItemState?> ReadConsumableItemForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int itemId, short unitId, CancellationToken ct)
    {
        const string sql = @"
            SELECT i.ItemCode,f.AvailableQuantity,f.OfficialUnitCost,f.UnitId
            FROM dbo.InventoryItems i WITH (UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.InventoryItemFoundation f WITH (UPDLOCK,HOLDLOCK) ON f.InventoryItemId=i.InventoryItemID AND f.InventoryClassId=2 AND f.UnitId=@unitId
            WHERE i.InventoryItemID=@itemId AND i.IsActive=1";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@unitId", unitId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new ConsumableItemState(itemId, reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetInt16(3))
            : null;
    }

    private static async Task ValidateProductionSourceAsync(SqlConnection connection, SqlTransaction transaction, int productionOrderId, int? productionBatchId, int? productMaterialId, int inventoryItemId, CancellationToken ct)
    {
        const string orderSql = "SELECT 1 FROM dbo.ProductionOrders WITH (UPDLOCK,HOLDLOCK) WHERE ProductionOrderId=@productionOrderId";
        await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
        orderCommand.Parameters.AddWithValue("@productionOrderId", productionOrderId);
        if (await orderCommand.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("The production order source does not exist.");
        if (productionBatchId is not null)
        {
            const string batchSql = "SELECT 1 FROM dbo.ProductionBatches WITH (UPDLOCK,HOLDLOCK) WHERE ProductionBatchId=@batchId AND ProductionOrderId=@orderId";
            await using var batchCommand = new SqlCommand(batchSql, connection, transaction);
            batchCommand.Parameters.AddWithValue("@batchId", productionBatchId.Value);
            batchCommand.Parameters.AddWithValue("@orderId", productionOrderId);
            if (await batchCommand.ExecuteScalarAsync(ct) is null)
                throw new InvalidOperationException("The production batch source does not belong to the production order.");
        }
        if (productMaterialId is null) return;
        const string materialSql = "SELECT 1 FROM dbo.ProductMaterials WITH (UPDLOCK,HOLDLOCK) WHERE ProductMaterialId=@materialId AND MaterialInventoryItemId=@itemId";
        await using var materialCommand = new SqlCommand(materialSql, connection, transaction);
        materialCommand.Parameters.AddWithValue("@materialId", productMaterialId.Value);
        materialCommand.Parameters.AddWithValue("@itemId", inventoryItemId);
        if (await materialCommand.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("The product material source does not belong to the consumable item.");
    }

    private static async Task<int> InsertConsumableConsumptionSourceAsync(SqlConnection connection, SqlTransaction transaction, ConsumeConsumableInventoryDto request, decimal unitCost, decimal operationalAmount, decimal postingAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.ProductionMaterialConsumptions
                (ProductionOrderId,ProductionBatchId,InventoryItemId,ProductMaterialId,ConsumedQuantity,UnitCost,TotalCost,ConsumptionDate,ReferenceNumber,Notes,CreatedAt,UnitId,SourceOperationId,OperationalAmount,PostingAmount)
            OUTPUT INSERTED.ProductionMaterialConsumptionId
            VALUES (@orderId,@batchId,@itemId,@materialId,@quantity,@unitCost,@totalCost,@now,@reference,@notes,@now,@unitId,@sourceOperationId,@operationalAmount,@postingAmount)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", request.ProductionOrderId);
        AddNullable(command, "@batchId", request.ProductionBatchId);
        command.Parameters.AddWithValue("@itemId", request.InventoryItemId);
        AddNullable(command, "@materialId", request.ProductMaterialId);
        AddDecimal(command, "@quantity", request.Quantity);
        AddDecimal(command, "@unitCost", unitCost);
        AddDecimal(command, "@totalCost", operationalAmount);
        var reference = $"ProductionOrder:{request.ProductionOrderId}:InventoryItem:{request.InventoryItemId}";
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@notes", "Official consumable inventory consumption");
        command.Parameters.AddWithValue("@unitId", request.UnitId);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        AddDecimal(command, "@operationalAmount", operationalAmount);
        AddDecimal(command, "@postingAmount", postingAmount, 2);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task UpdateConsumableFoundationAsync(SqlConnection connection, SqlTransaction transaction, ConsumableItemState item, decimal quantity, decimal operationalAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            UPDATE dbo.InventoryItems
            SET CurrentQuantity=CurrentQuantity-@quantity, AvailableQuantity=AvailableQuantity-@quantity, UpdatedAt=@now
            WHERE InventoryItemID=@itemId AND AvailableQuantity>=@quantity;
            IF @@ROWCOUNT <> 1 THROW 51443, N'Consumable inventory availability changed before consumption.', 1;
            UPDATE dbo.InventoryItemFoundation
            SET AvailableQuantity=AvailableQuantity-@quantity, ConsumedQuantity=ConsumedQuantity+@quantity, OperationalValue=OperationalValue-@operationalAmount, UpdatedAt=@now
            WHERE InventoryItemId=@itemId AND AvailableQuantity>=@quantity AND OperationalValue>=@operationalAmount;
            IF @@ROWCOUNT <> 1 THROW 51444, N'Consumable foundation availability changed before consumption.', 1;";
        await using var command = new SqlCommand(sql, connection, transaction);
        AddDecimal(command, "@quantity", quantity);
        AddDecimal(command, "@operationalAmount", operationalAmount);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@itemId", item.ItemId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task LinkConsumptionTransactionAsync(SqlConnection connection, SqlTransaction transaction, long sourceId, int transactionId, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.ProductionMaterialConsumptions SET InventoryTransactionId=@transactionId WHERE ProductionMaterialConsumptionId=@sourceId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@sourceId", sourceId);
        command.Parameters.AddWithValue("@transactionId", transactionId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task LinkFabricConsumptionArtifactsAsync(SqlConnection connection, SqlTransaction transaction, long sourceId, int transactionId, long accountingEventId, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.FabricConsumptionSources SET InventoryTransactionId=@transactionId,AccountingEventId=@accountingEventId WHERE FabricConsumptionSourceId=@sourceId; UPDATE dbo.InventoryTransactions SET AccountingEventId=@accountingEventId WHERE TransactionID=@transactionId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@sourceId", sourceId);
        command.Parameters.AddWithValue("@transactionId", transactionId);
        command.Parameters.AddWithValue("@accountingEventId", accountingEventId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task LinkConsumableConsumptionArtifactsAsync(SqlConnection connection, SqlTransaction transaction, long sourceId, int transactionId, long accountingEventId, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.ProductionMaterialConsumptions SET InventoryTransactionId=@transactionId,AccountingEventId=@accountingEventId WHERE ProductionMaterialConsumptionId=@sourceId; UPDATE dbo.InventoryTransactions SET AccountingEventId=@accountingEventId WHERE TransactionID=@transactionId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@sourceId", sourceId);
        command.Parameters.AddWithValue("@transactionId", transactionId);
        command.Parameters.AddWithValue("@accountingEventId", accountingEventId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<InventoryFoundationPostingResultDto?> TryReadExistingConsumableConsumptionAsync(SqlConnection connection, SqlTransaction transaction, ConsumeConsumableInventoryDto request, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP (1) c.ProductionMaterialConsumptionId,c.InventoryItemId,i.ItemCode,c.ConsumedQuantity,c.OperationalAmount,c.PostingAmount,c.AccountingEventId,c.InventoryTransactionId,
                   c.ProductionOrderId,c.ProductionBatchId,c.ProductMaterialId
            FROM dbo.ProductionMaterialConsumptions c WITH (UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=c.InventoryItemId
            WHERE c.SourceOperationId=@sourceOperationId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (reader.GetInt32(1) != request.InventoryItemId || reader.GetInt32(8) != request.ProductionOrderId || reader.GetDecimal(3) != request.Quantity || (request.ProductionBatchId is null ? !reader.IsDBNull(9) : reader.IsDBNull(9) || reader.GetInt32(9) != request.ProductionBatchId.Value) || (request.ProductMaterialId is null ? !reader.IsDBNull(10) : reader.IsDBNull(10) || reader.GetInt32(10) != request.ProductMaterialId.Value))
            throw new InvalidOperationException("The source operation was already used with different consumable consumption details.");
        if (reader.IsDBNull(6) || reader.IsDBNull(7))
            throw new InvalidOperationException("The existing consumable consumption posting is incomplete.");
        return new InventoryFoundationPostingResultDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetInt64(6), reader.GetInt32(7), true);
    }

    private sealed record IncompleteReceiptState(
        long SourceRecordId,
        int GoodsReceiptItemId,
        int InventoryItemId,
        string ItemCode,
        decimal Quantity,
        decimal OperationalAmount,
        decimal PostingAmount,
        long AccountingEventId,
        int InventoryTransactionId,
        byte InventoryClassId,
        short UnitId,
        Guid SourceOperationId);

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte scale = 6)
    {
        var parameter = command.Parameters.Add(name, System.Data.SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    internal static bool IsStorageItemTypeCompatible(string? sourceType, string itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType))
            return true;

        if (string.IsNullOrWhiteSpace(sourceType))
            return true;

        return string.Equals(sourceType.Trim(), itemType.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReceiptStorageEligible(string? receiptStatus, string? lineStatus)
    {
        if (string.IsNullOrWhiteSpace(receiptStatus) || string.IsNullOrWhiteSpace(lineStatus))
            return false;

        var normalizedReceipt = receiptStatus.Trim();
        var normalizedLine = lineStatus.Trim();

        if (string.Equals(normalizedReceipt, "Reversed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLine, "Reversed", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(normalizedReceipt, "Confirmed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedReceipt, "Posted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLine, "Confirmed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedLine, "Posted", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InsertStorageAllocationAsync(SqlConnection connection, SqlTransaction transaction, int? goodsReceiptItemId, string itemType, decimal quantity, Guid? storageOperationId, int inventoryItemId, int inventoryTransactionId, long? accountingEventId, long? inventoryReceiptPostingId, long? importedReceiptId, CancellationToken ct)
    {
        if (!goodsReceiptItemId.HasValue) return;
        if (storageOperationId is null || storageOperationId == Guid.Empty)
            throw new ArgumentException("Storage operation id is required when storing a confirmed receipt.");

        var itemTypeColumnExists = await ColumnExistsAsync(connection, transaction, "dbo.GoodsReceiptItems", "ItemType", ct);
        var invoiceItemTypeColumnExists = await ColumnExistsAsync(connection, transaction, "dbo.SupplierInvoiceLines", "ItemType", ct);
        var sourceSql = itemTypeColumnExists
            ? @"
            SELECT i.ReceivedQuantity, COALESCE(i.ItemType,sil.ItemType), r.ReceiptStatus, ISNULL(i.LineStatus,N'Confirmed')
            FROM dbo.GoodsReceiptItems i WITH(UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.GoodsReceipts r WITH(UPDLOCK,HOLDLOCK) ON r.GoodsReceiptId=i.GoodsReceiptId
            LEFT JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId=i.SupplierInvoiceLineId
            WHERE i.GoodsReceiptItemId=@item"
            : invoiceItemTypeColumnExists
                ? @"
            SELECT i.ReceivedQuantity, sil.ItemType, r.ReceiptStatus, ISNULL(i.LineStatus,N'Confirmed')
            FROM dbo.GoodsReceiptItems i WITH(UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.GoodsReceipts r WITH(UPDLOCK,HOLDLOCK) ON r.GoodsReceiptId=i.GoodsReceiptId
            LEFT JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId=i.SupplierInvoiceLineId
            WHERE i.GoodsReceiptItemId=@item"
                : @"
            SELECT i.ReceivedQuantity, NULL AS ItemType, r.ReceiptStatus, ISNULL(i.LineStatus,N'Confirmed')
            FROM dbo.GoodsReceiptItems i WITH(UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.GoodsReceipts r WITH(UPDLOCK,HOLDLOCK) ON r.GoodsReceiptId=i.GoodsReceiptId
            WHERE i.GoodsReceiptItemId=@item";

        await using var source = new SqlCommand(sourceSql, connection, transaction);
        source.Parameters.AddWithValue("@item", goodsReceiptItemId.Value);
        await using var reader = await source.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("سطر الاستلام غير موجود.");
        var received = reader.GetDecimal(0);
        var sourceType = reader.IsDBNull(1) ? null : reader.GetString(1);
        var receiptStatus = reader.GetString(2);
        var lineStatus = reader.GetString(3);
        await reader.CloseAsync();
        if (!IsReceiptStorageEligible(receiptStatus, lineStatus)) throw new InvalidOperationException("لا يمكن تخزين استلام غير مفعّل أو معكوس.");
        if (!IsStorageItemTypeCompatible(sourceType, itemType)) throw new InvalidOperationException("نوع التخزين لا يطابق نوع سطر الاستلام.");

        await using var duplicate = new SqlCommand("SELECT COUNT_BIG(1) FROM dbo.GoodsReceiptItemStorageAllocations WITH(UPDLOCK,HOLDLOCK) WHERE StorageOperationId=@operation", connection, transaction);
        duplicate.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = storageOperationId.Value;
        if (Convert.ToInt64(await duplicate.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("عملية التخزين مستخدمة مسبقاً.");

        await using var total = new SqlCommand("SELECT COALESCE(SUM(StoredQuantity),0) FROM dbo.GoodsReceiptItemStorageAllocations WITH(UPDLOCK,HOLDLOCK) WHERE GoodsReceiptItemId=@item", connection, transaction);
        total.Parameters.AddWithValue("@item", goodsReceiptItemId.Value);
        var stored = Convert.ToDecimal(await total.ExecuteScalarAsync(ct));
        if (quantity <= 0m || stored + quantity > received)
            throw new InvalidOperationException($"الكمية المتبقية للتخزين هي {received - stored} فقط.");

        await using var insert = new SqlCommand(@"
            INSERT dbo.GoodsReceiptItemStorageAllocations(GoodsReceiptItemId,ItemType,StorageOperationId,StoredQuantity,InventoryItemId,InventoryTransactionId,AccountingEventId,InventoryReceiptPostingId,ImportedReadyMadeInventoryReceiptId)
            VALUES(@item,@type,@operation,@quantity,@inventoryItem,@transaction,@accounting,@posting,@imported)", connection, transaction);
        insert.Parameters.AddWithValue("@item", goodsReceiptItemId.Value);
        insert.Parameters.AddWithValue("@type", itemType);
        insert.Parameters.Add("@operation", System.Data.SqlDbType.UniqueIdentifier).Value = storageOperationId.Value;
        AddDecimal(insert, "@quantity", quantity);
        insert.Parameters.AddWithValue("@inventoryItem", inventoryItemId);
        insert.Parameters.AddWithValue("@transaction", inventoryTransactionId);
        insert.Parameters.AddWithValue("@accounting", accountingEventId ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("@posting", inventoryReceiptPostingId ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("@imported", importedReceiptId ?? (object)DBNull.Value);
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ColumnExistsAsync(SqlConnection connection, SqlTransaction transaction, string tableName, string columnName, CancellationToken ct)
    {
        await using var command = new SqlCommand(@"
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM sys.columns c
                WHERE c.object_id = OBJECT_ID(@tableName)
                  AND c.name = @columnName
            ) THEN 1 ELSE 0 END AS bit)", connection, transaction);
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
    }

    private sealed record GoodsReceiptSource(decimal Quantity, decimal UnitCost, string ReceiptNumber, string ItemName);
    private sealed record ReceiptInventoryItem(int InventoryItemId, string ItemName, string ItemCode, byte InventoryClassId, short UnitId, string? FabricTypeCode, string? ColorValue);
    private sealed record InvoiceLineMatch(long SupplierInvoiceLineId, decimal Quantity, decimal UnitCost, bool NeedsInventoryLink, int? RollCount);
    private sealed record PostedGoodsReceiptItem(int GoodsReceiptItemId, int InventoryItemId, decimal Quantity, decimal UnitCost, int WarehouseId, byte InventoryClassId, short UnitId, long InventoryReceiptLineId, int InventoryTransactionId, long AccountingEventId, long? FabricRollId);
    private sealed record FoundationItemState(int InventoryItemId, bool HasFoundation, byte? InventoryClassId, short? UnitId, decimal OriginalQuantity, decimal AvailableQuantity, decimal ConsumedQuantity, decimal OperationalValue);
    private sealed record FabricRollState(long FabricRollId, int InventoryItemId, string ItemCode, decimal AvailableQuantity, short UnitId, decimal OfficialUnitCost);
    private sealed record ConsumableItemState(int ItemId, string ItemCode, decimal AvailableQuantity, decimal OfficialUnitCost, short UnitId);

    public async Task<FabricBatchResultDto?> ReceiveFabricBatchAsync(CreateFabricBatchDto batch, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var step = "ValidateFabricBatch";
        var invoiceNumber = batch.InvoiceNumber?.Trim() ?? string.Empty;
        string? fabricCode = null;

        try
        {
            step = "ValidateFabricBatch";
            ValidateFabricBatch(batch);
            invoiceNumber = batch.InvoiceNumber?.Trim() ?? string.Empty;
            fabricCode = batch.Rolls[0].FabricCode?.Trim() ?? string.Empty;

            var storageLinkedRolls = batch.Rolls
                .Where(roll => roll.GoodsReceiptItemId.HasValue || roll.StorageOperationId.HasValue)
                .ToList();

            if (storageLinkedRolls.Count > 0)
            {
                if (storageLinkedRolls.Count != batch.Rolls.Count)
                    throw new ArgumentException("Storage-linked fabric batches cannot be mixed with new official purchase batches.");

                var now = DateTime.UtcNow;
                decimal totalYards = 0m;
                decimal totalCost = 0m;

                foreach (var roll in storageLinkedRolls)
                {
                    if (!roll.GoodsReceiptItemId.HasValue || !roll.StorageOperationId.HasValue)
                        throw new ArgumentException("Each storage-linked fabric roll must contain both GoodsReceiptItemId and StorageOperationId.");

                    if (roll.GoodsReceiptItemId.Value <= 0 || roll.StorageOperationId.Value == Guid.Empty)
                        throw new ArgumentException("Storage-linked fabric rolls require a valid GoodsReceiptItemId and StorageOperationId.");

                    var receipt = await ReadGoodsReceiptSourceAsync(connection, transaction, roll.GoodsReceiptItemId.Value, ct)
                        ?? throw new InvalidOperationException($"Goods receipt item {roll.GoodsReceiptItemId.Value} was not found.");
                    if (roll.QuantityYards > receipt.Quantity)
                        throw new InvalidOperationException($"The storage request for GoodsReceiptItem {roll.GoodsReceiptItemId.Value} exceeds the original received quantity.");

                    await PostOfficialFabricBatchRollAsync(
                        connection,
                        transaction,
                        roll.GoodsReceiptItemId.Value,
                        roll.FabricCode.Trim(),
                        roll.FabricType.Trim(),
                        string.IsNullOrWhiteSpace(roll.CatalogNumber) ? null : roll.CatalogNumber.Trim(),
                        string.IsNullOrWhiteSpace(roll.FabricColor) ? null : roll.FabricColor.Trim(),
                        roll.FabricWidth,
                        roll.GoodsReceiptItemId.Value,
                        roll.StorageOperationId.Value,
                        now,
                        ct);

                    totalYards += roll.QuantityYards;
                    totalCost = decimal.Round(totalCost + (roll.QuantityYards * roll.YardPrice), 2, MidpointRounding.AwayFromZero);
                }

                await transaction.CommitAsync(ct);
                logger?.LogInformation("Storage-linked fabric batch completed without creating a new purchase order. SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; TotalRolls={TotalRolls}; TotalYards={TotalYards}", batch.SupplierId, invoiceNumber, storageLinkedRolls.Count, totalYards);
                return new FabricBatchResultDto(storageLinkedRolls.Count, totalYards, totalCost, $"STORAGE-{invoiceNumber}", now);
            }

            step = "ReadExistingOfficialBatch";
            var existing = await TryReadOfficialFabricBatchAsync(connection, transaction, batch.SupplierId, invoiceNumber, ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                logger?.LogInformation("Official fabric batch already exists. SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; FabricCode={FabricCode}", batch.SupplierId, invoiceNumber, fabricCode);
                return existing;
            }

            step = "ValidateSupplier";
            if (!await SupplierExistsAsync(connection, transaction, batch.SupplierId, ct)) return null;

            var officialNow = DateTime.UtcNow;
            var officialTotalCost = decimal.Round(batch.Rolls.Sum(roll => roll.QuantityYards * roll.YardPrice), 2, MidpointRounding.AwayFromZero);
            step = "InsertPurchaseOrder";
            var purchaseOrderId = await InsertFabricPurchaseOrderAsync(connection, transaction, batch.SupplierId, invoiceNumber, officialTotalCost, officialNow, ct);
            logger?.LogInformation("Official fabric batch step succeeded. Step={Step}; SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; PurchaseOrderId={PurchaseOrderId}", step, batch.SupplierId, invoiceNumber, purchaseOrderId);
            step = "InsertGoodsReceipt";
            var goodsReceiptId = await InsertFabricGoodsReceiptAsync(connection, transaction, batch.SupplierId, purchaseOrderId, invoiceNumber, batch.Notes, officialNow, ct);
            logger?.LogInformation("Official fabric batch step succeeded. Step={Step}; SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; GoodsReceiptId={GoodsReceiptId}", step, batch.SupplierId, invoiceNumber, goodsReceiptId);
            decimal officialTotalYards = 0m;

            foreach (var roll in batch.Rolls)
            {
                var code = roll.FabricCode.Trim();
                fabricCode = code;
                var fabricType = roll.FabricType.Trim();
                var lineTotal = decimal.Round(roll.QuantityYards * roll.YardPrice, 6, MidpointRounding.AwayFromZero);
                step = "InsertPurchaseOrderItem";
                await InsertFabricPurchaseOrderItemAsync(connection, transaction, purchaseOrderId, fabricType, roll.QuantityYards, roll.YardPrice, lineTotal, ct);
                step = "InsertGoodsReceiptItem";
                var goodsReceiptItemId = await InsertFabricGoodsReceiptItemAsync(connection, transaction, goodsReceiptId, fabricType, roll.QuantityYards, roll.YardPrice, lineTotal, ct);
                logger?.LogInformation("Official fabric batch step succeeded. Step={Step}; SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; FabricCode={FabricCode}; GoodsReceiptItemId={GoodsReceiptItemId}", step, batch.SupplierId, invoiceNumber, code, goodsReceiptItemId);
                step = "PostOfficialFabricReceipt";
                await PostOfficialFabricBatchRollAsync(
                    connection,
                    transaction,
                    goodsReceiptItemId,
                    code,
                    fabricType,
                    string.IsNullOrWhiteSpace(roll.CatalogNumber) ? null : roll.CatalogNumber.Trim(),
                    string.IsNullOrWhiteSpace(roll.FabricColor) ? null : roll.FabricColor.Trim(),
                    roll.FabricWidth,
                    roll.GoodsReceiptItemId,
                    roll.StorageOperationId,
                    officialNow,
                    ct);
                logger?.LogInformation("Official fabric batch step succeeded. Step={Step}; SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; FabricCode={FabricCode}", step, batch.SupplierId, invoiceNumber, code);
                officialTotalYards += roll.QuantityYards;
            }

            step = "CommitTransaction";
            await transaction.CommitAsync(ct);
            logger?.LogInformation("Official fabric batch transaction committed. SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; FabricCode={FabricCode}", batch.SupplierId, invoiceNumber, fabricCode);
            return new FabricBatchResultDto(batch.Rolls.Count, officialTotalYards, officialTotalCost, $"FAB-{invoiceNumber}", officialNow);
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "Official fabric batch transaction failed. Step={Step}; SupplierId={SupplierId}; InvoiceNumber={InvoiceNumber}; FabricCode={FabricCode}; SqlErrorNumber={SqlErrorNumber}; InnerException={InnerException}",
                step,
                batch.SupplierId,
                invoiceNumber,
                fabricCode,
                exception is SqlException sqlException ? sqlException.Number : null,
                exception.InnerException?.ToString() ?? "<none>");
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void ValidateFabricBatch(CreateFabricBatchDto batch)
    {
        if (batch.SupplierId <= 0 || string.IsNullOrWhiteSpace(batch.InvoiceNumber) || batch.Rolls.Count == 0)
            throw new ArgumentException("Supplier, invoice number, and at least one fabric roll are required.");

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roll in batch.Rolls)
        {
            var hasStorageLink = roll.GoodsReceiptItemId.HasValue || roll.StorageOperationId.HasValue;
            if (hasStorageLink && (!roll.GoodsReceiptItemId.HasValue || !roll.StorageOperationId.HasValue))
                throw new ArgumentException("Storage-linked fabric rolls must include both GoodsReceiptItemId and StorageOperationId.");
            if (string.IsNullOrWhiteSpace(roll.FabricCode) || string.IsNullOrWhiteSpace(roll.FabricType))
                throw new ArgumentException("Fabric code and type are required for every roll.");
            if (roll.QuantityYards <= 0m || roll.YardPrice <= 0m || roll.FabricWidth <= 0m)
                throw new ArgumentException("Fabric quantity, price, and width must be positive.");
            if (!codes.Add(roll.FabricCode.Trim()))
                throw new ArgumentException($"Fabric code '{roll.FabricCode.Trim()}' is duplicated within the batch.");
        }
    }

    private static async Task<FabricBatchResultDto?> TryReadOfficialFabricBatchAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, string invoiceNumber, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP (1) gr.GoodsReceiptId, gr.CreatedAt
            FROM dbo.GoodsReceipts gr WITH (UPDLOCK, HOLDLOCK)
            WHERE gr.SupplierId=@supplierId AND gr.ReceiptNumber=@receiptNumber
            ORDER BY gr.GoodsReceiptId DESC;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        command.Parameters.AddWithValue("@receiptNumber", $"FAB-{invoiceNumber}");
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var receiptId = reader.GetInt32(0);
        var createdAt = reader.GetDateTime(1);
        await reader.CloseAsync();

        const string totalsSql = @"
            SELECT COUNT(*), COALESCE(SUM(ReceivedQuantity),0), COALESCE(SUM(LineTotal),0)
            FROM dbo.GoodsReceiptItems
            WHERE GoodsReceiptId=@receiptId;";
        await using var totalsCommand = new SqlCommand(totalsSql, connection, transaction);
        totalsCommand.Parameters.AddWithValue("@receiptId", receiptId);
        await using var totals = await totalsCommand.ExecuteReaderAsync(ct);
        if (!await totals.ReadAsync(ct)) return null;
        return new FabricBatchResultDto(totals.GetInt32(0), totals.GetDecimal(1), totals.GetDecimal(2), $"FAB-{invoiceNumber}", createdAt);
    }

    private static async Task<bool> SupplierExistsAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, CancellationToken ct)
    {
        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId=@supplierId", connection, transaction);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<int> InsertFabricPurchaseOrderAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, string invoiceNumber, decimal totalCost, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.PurchaseOrders (PurchaseOrderNumber,SupplierId,OrderDate,ExpectedDeliveryDate,Status,TotalAmount,CreatedAt)
            OUTPUT INSERTED.PurchaseOrderId
            VALUES (@number,@supplierId,@now,@now,N'Open',@totalCost,@now);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@number", $"FAB-PO-{invoiceNumber}");
        command.Parameters.AddWithValue("@supplierId", supplierId);
        AddDecimal(command, "@totalCost", totalCost, 2);
        command.Parameters.AddWithValue("@now", now);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<int> InsertFabricGoodsReceiptAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, int purchaseOrderId, string invoiceNumber, string? notes, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.GoodsReceipts (SupplierId,PurchaseOrderId,ReceiptNumber,ReceiptDate,Notes,CreatedAt)
            OUTPUT INSERTED.GoodsReceiptId
            VALUES (@supplierId,@purchaseOrderId,@receiptNumber,@now,@notes,@now);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        command.Parameters.AddWithValue("@purchaseOrderId", purchaseOrderId);
        command.Parameters.AddWithValue("@receiptNumber", $"FAB-{invoiceNumber}");
        AddNullable(command, "@notes", notes);
        command.Parameters.AddWithValue("@now", now);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task InsertFabricPurchaseOrderItemAsync(SqlConnection connection, SqlTransaction transaction, int purchaseOrderId, string fabricType, decimal quantity, decimal unitCost, decimal lineTotal, CancellationToken ct)
    {
        const string sql = "INSERT INTO dbo.PurchaseOrderItems (PurchaseOrderId,ItemName,Quantity,UnitCost,LineTotal) VALUES (@orderId,@name,@quantity,@unitCost,@lineTotal);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", purchaseOrderId);
        command.Parameters.AddWithValue("@name", fabricType);
        AddDecimal(command, "@quantity", quantity, 2);
        AddDecimal(command, "@unitCost", unitCost, 2);
        AddDecimal(command, "@lineTotal", lineTotal, 2);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> InsertFabricGoodsReceiptItemAsync(SqlConnection connection, SqlTransaction transaction, int goodsReceiptId, string fabricType, decimal quantity, decimal unitCost, decimal lineTotal, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId,ItemName,ReceivedQuantity,UnitCost,LineTotal,ItemType)
            OUTPUT INSERTED.GoodsReceiptItemId
            VALUES (@receiptId,@name,@quantity,@unitCost,@lineTotal,@itemType);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@receiptId", goodsReceiptId);
        command.Parameters.AddWithValue("@name", fabricType);
        AddDecimal(command, "@quantity", quantity);
        AddDecimal(command, "@unitCost", unitCost);
        AddDecimal(command, "@lineTotal", lineTotal);
        command.Parameters.AddWithValue("@itemType", "Fabric");
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task PostOfficialFabricBatchRollAsync(SqlConnection connection, SqlTransaction transaction, int goodsReceiptItemId, string itemCode, string fabricType, string? catalogNumber, string? colorValue, decimal fabricWidth, int? linkedGoodsReceiptItemId, Guid? storageOperationId, DateTime now, CancellationToken ct)
    {
        var receipt = await ReadGoodsReceiptSourceAsync(connection, transaction, goodsReceiptItemId, ct)
            ?? throw new InvalidOperationException("Goods receipt item was not found.");
        var operationalAmount = decimal.Round(receipt.Quantity * receipt.UnitCost, 6, MidpointRounding.AwayFromZero);
        var postingAmount = decimal.Round(operationalAmount, 2, MidpointRounding.AwayFromZero);
        var existingItem = await ReadFoundationItemAsync(connection, transaction, itemCode, ct);
        if (existingItem is not null && !existingItem.HasFoundation)
            throw new InvalidOperationException("Legacy inventory items cannot be used by the foundation posting path.");
        if (existingItem is not null && (existingItem.InventoryClassId != 1 || existingItem.UnitId != 1))
            throw new InvalidOperationException("The inventory item class or unit does not match the fabric receipt.");

        var itemId = existingItem?.InventoryItemId ?? await InsertFoundationInventoryItemAsync(connection, transaction, itemCode, receipt.ItemName, 1, "Yard", 1, receipt.Quantity, operationalAmount, fabricType, colorValue, now, ct);
        if (existingItem is not null)
            await UpdateFoundationInventoryItemAsync(connection, transaction, itemId, receipt.Quantity, operationalAmount, now, ct);

        await ApplyFabricPresentationAsync(connection, transaction, itemId, fabricType, catalogNumber, colorValue, fabricWidth, ct);
        var sourceOperationId = Guid.NewGuid();
        var opposingLedgerAccountId = await ReadOpposingLedgerAccountIdAsync(connection, transaction, "2100", ct);
        var postingId = await InsertInventoryReceiptPostingAsync(connection, transaction, goodsReceiptItemId, 1, opposingLedgerAccountId, sourceOperationId, operationalAmount, postingAmount, ct);
        var lineId = await InsertInventoryReceiptLineAsync(connection, transaction, postingId, itemId, receipt.Quantity, receipt.UnitCost, 1, ct);
        var rollId = await InsertFabricRollAsync(connection, transaction, itemId, itemCode, fabricType, colorValue, receipt.Quantity, receipt.UnitCost, 1, lineId, now, ct);
        await SetReceiptLineRollAsync(connection, transaction, lineId, rollId, ct);
        var reference = $"GoodsReceipt:{receipt.ReceiptNumber}:Item:{goodsReceiptItemId}";
        var transactionId = await InsertFoundationInventoryTransactionAsync(connection, transaction, itemId, "FabricInventoryReceived", receipt.Quantity, reference, operationalAmount, receipt.UnitCost, sourceOperationId, now, ct);
        var accountingEvent = await AccountingEventPostingGateway.PostInventoryReceiptAsync(connection, transaction, AccountingEventType.FabricInventoryReceived, postingId, postingAmount, reference, $"Inventory receipt {receipt.ReceiptNumber} item {goodsReceiptItemId}", ct);
        await LinkInventoryReceiptArtifactsAsync(connection, transaction, lineId, transactionId, accountingEvent.AccountingEventId, ct);
        await InsertStorageAllocationAsync(connection, transaction, linkedGoodsReceiptItemId, "Fabric", receipt.Quantity, storageOperationId, itemId, transactionId, accountingEvent.AccountingEventId, postingId, null, ct);
    }

    private static async Task ApplyFabricPresentationAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string fabricType, string? catalogNumber, string? colorValue, decimal fabricWidth, CancellationToken ct)
    {
        var resolvedCatalog = await ResolveFabricCatalogNumberAsync(connection, transaction, itemId, catalogNumber, ct);

        const string sql = @"
            UPDATE dbo.InventoryItems
            SET FabricCategory=@fabricType,
                Barcode=@catalogNumber,
                FabricColor=COALESCE(@colorValue,FabricColor),
                FabricWidth=@fabricWidth,
                FabricWidthUnit=N'Inch'
            WHERE InventoryItemID=@itemId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@fabricType", fabricType);
        command.Parameters.AddWithValue("@catalogNumber", resolvedCatalog);
        AddNullable(command, "@colorValue", colorValue);
        AddDecimal(command, "@fabricWidth", fabricWidth, 4);
        command.Parameters.AddWithValue("@itemId", itemId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string> ResolveFabricCatalogNumberAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string? requestedCatalogNumber, CancellationToken ct)
    {
        using var readCurrent = new SqlCommand("SELECT Barcode FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK) WHERE InventoryItemID=@itemId", connection, transaction);
        readCurrent.Parameters.AddWithValue("@itemId", itemId);
        var currentValue = await readCurrent.ExecuteScalarAsync(ct);
        var currentCatalog = currentValue is DBNull or null ? null : currentValue.ToString();
        if (!string.IsNullOrWhiteSpace(currentCatalog))
        {
            return currentCatalog.Trim();
        }

        var preferredCatalog = string.IsNullOrWhiteSpace(requestedCatalogNumber) ? null : requestedCatalogNumber.Trim();
        if (!string.IsNullOrWhiteSpace(preferredCatalog))
        {
            using var check = new SqlCommand("SELECT TOP (1) InventoryItemID FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK) WHERE Barcode=@requested AND InventoryItemID<>@itemId", connection, transaction);
            check.Parameters.AddWithValue("@requested", preferredCatalog);
            check.Parameters.AddWithValue("@itemId", itemId);
            var existingItemId = await check.ExecuteScalarAsync(ct);
            if (existingItemId is null)
            {
                return preferredCatalog;
            }
        }

        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "CatalogNumberPrefix", "CAT", ct);
        var nextNumber = await SystemCodeGenerator.GetNextNumberAsync(connection, transaction, "dbo.InventoryItems", "Barcode", "CatalogNumberPrefix", "CAT", ct);
        return $"{prefix}{nextNumber.ToString("D4", CultureInfo.InvariantCulture)}";
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value is string text ? (string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim()) : value ?? DBNull.Value);

    private static InventoryItemDto MapItem(SqlDataReader r) => new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetBoolean(8), r.GetDateTime(9), r.NullableDateTime("UpdatedAt"), r.NullableString("Barcode"), r.NullableString("FabricCategory"), r.NullableString("FabricColor"), r.NullableDecimal("FabricWidth"), r.NullableString("FabricWidthUnit"), r.NullableDecimal("InchPrice"), r.NullableDecimal("YardPrice"));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? id, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id is not null) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}