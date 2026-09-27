using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class InventoryRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IInventoryRepository
{
    public Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken ct) => QueryAsync("SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice FROM dbo.InventoryItems ORDER BY ItemName, InventoryItemID", MapItem, null, ct);
    public async Task<InventoryItemDto?> GetItemByIdAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT InventoryItemID, ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice FROM dbo.InventoryItems WHERE InventoryItemID = @id", MapItem, id, ct)).SingleOrDefault();
    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken ct) => QueryAsync("SELECT TransactionID, InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost FROM dbo.InventoryTransactions ORDER BY CreatedAt DESC, TransactionID DESC", reader => new InventoryTransactionDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDecimal(3), reader.NullableString("ReferenceNumber"), reader.NullableString("Notes"), reader.GetDateTime(6), reader.NullableDecimal("TotalCostImpact"), reader.NullableDecimal("UnitCost")), null, ct);
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
            FROM dbo.InventoryItems
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

                await InsertToolTransactionAsync(connection, transaction, existing.InventoryItemId, candidateCode, productName, quantity, unit, unitPrice, supplierId, invoiceNumber, notes, now, "Renewal", ct);
                await transaction.CommitAsync(ct);
                return await GetItemByIdAsync(existing.InventoryItemId, ct);
            }

            if (existing is not null && !tool.RenewExisting)
            {
                throw new InvalidOperationException($"الأداة '{productName}' موجودة بالفعل في المخزون.");
            }

            var itemId = await InsertToolInventoryItemAsync(connection, transaction, candidateCode, productName, productType, unit, quantity, unitPrice, supplierId, invoiceNumber, notes, now, ct);
            await InsertToolTransactionAsync(connection, transaction, itemId, candidateCode, productName, quantity, unit, unitPrice, supplierId, invoiceNumber, notes, now, "Receive", ct);
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

    private static async Task InsertToolTransactionAsync(SqlConnection connection, SqlTransaction transaction, int itemId, string code, string productName, decimal quantity, string unit, decimal unitPrice, int? supplierId, string invoiceNumber, string? notes, DateTime now, string operationType, CancellationToken ct)
    {
        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
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
        await cmd.ExecuteNonQueryAsync(ct);
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
            var productCode = product.ProductCode.Trim();
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


    public Task<InventoryFoundationPostingResultDto?> ReceiveFabricInventoryAsync(ReceiveFabricInventoryDto request, CancellationToken ct) =>
        ReceiveFoundationInventoryAsync(
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

            await UpdateFabricRollAndFoundationAsync(
                connection,
                transaction,
                roll,
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
            WHERE rp.SourceOperationId = @sourceOperationId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = sourceOperationId;
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

    private static async Task UpdateFabricRollAndFoundationAsync(SqlConnection connection, SqlTransaction transaction, FabricRollState roll, decimal quantity, decimal operationalAmount, DateTime now, CancellationToken ct)
    {
        const string sql = @"
            UPDATE dbo.FabricRolls
            SET AvailableQuantity=AvailableQuantity-@quantity, ConsumedQuantity=ConsumedQuantity+@quantity, UpdatedAt=@now
            WHERE FabricRollId=@rollId AND AvailableQuantity>=@quantity;
            IF @@ROWCOUNT <> 1 THROW 51440, N'Fabric roll availability changed before consumption.', 1;
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
        command.Parameters.AddWithValue("@rollId", roll.FabricRollId);
        command.Parameters.AddWithValue("@itemId", roll.InventoryItemId);
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

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte scale = 6)
    {
        var parameter = command.Parameters.Add(name, System.Data.SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    private sealed record GoodsReceiptSource(decimal Quantity, decimal UnitCost, string ReceiptNumber, string ItemName);
    private sealed record FoundationItemState(int InventoryItemId, bool HasFoundation, byte? InventoryClassId, short? UnitId, decimal OriginalQuantity, decimal AvailableQuantity, decimal ConsumedQuantity, decimal OperationalValue);
    private sealed record FabricRollState(long FabricRollId, int InventoryItemId, string ItemCode, decimal AvailableQuantity, short UnitId, decimal OfficialUnitCost);
    private sealed record ConsumableItemState(int ItemId, string ItemCode, decimal AvailableQuantity, decimal OfficialUnitCost, short UnitId);

    public async Task<FabricBatchResultDto?> ReceiveFabricBatchAsync(CreateFabricBatchDto batch, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            string? supplierName = null;
            using (var supCmd = new SqlCommand("SELECT SupplierName FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = @supId", connection, transaction))
            {
                supCmd.Parameters.AddWithValue("@supId", batch.SupplierId);
                supplierName = (await supCmd.ExecuteScalarAsync(ct)) as string;
                if (supplierName is null) return null;
            }

            var now = DateTime.UtcNow;
            var reference = $"FBATCH-{batch.InvoiceNumber.Trim()}";
            decimal totalYards = 0;
            decimal totalCost = 0;

            var reservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allCodes = new List<string>();
            var fabricPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "FabricCodePrefix", "FA", ct);

            using (var codeCmd = new SqlCommand(@"
                SELECT ItemCode
                FROM dbo.InventoryItems WITH (NOLOCK)
                WHERE ItemCode LIKE @fabricPrefix

                UNION ALL

                SELECT CAST(FabricCode AS nvarchar(50))
                FROM dbo.Fabrics_Inventory WITH (NOLOCK)
                WHERE FabricCode IS NOT NULL

                UNION ALL

                SELECT FabricCode
                FROM dbo.Fabrics WITH (NOLOCK)
                WHERE FabricCode IS NOT NULL

                UNION ALL

                SELECT FabricCode
                FROM dbo.OrderItemFabrics WITH (NOLOCK)
                WHERE FabricCode IS NOT NULL

                UNION ALL

                SELECT FabricCode
                FROM dbo.ReadyMadeInventoryProducts WITH (NOLOCK)
                WHERE FabricCode IS NOT NULL

                UNION ALL

                SELECT ProductCode
                FROM dbo.ImportedReadyMadeProducts WITH (NOLOCK)
                WHERE ProductCode LIKE @fabricPrefix", connection, transaction))
            {
                codeCmd.Parameters.AddWithValue("@fabricPrefix", $"{fabricPrefix}%");
                using var reader = await codeCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var codeValue = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(codeValue))
                    {
                        allCodes.Add(codeValue.Trim());
                    }
                }
            }

            for (int i = 0; i < batch.Rolls.Count; i++)
            {
                var roll = batch.Rolls[i];
                var code = roll.FabricCode.Trim();
                var name = roll.FabricType.Trim();
                if (reservedCodes.Contains(code))
                {
                    throw new InvalidOperationException($"كود القماش '{code}' مكرر داخل نفس الدفعة.");
                }

                if (allCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"كود القماش '{code}' موجود بالفعل في قاعدة البيانات ولا يمكن إعادة استخدامه.");
                }

                reservedCodes.Add(code);
                allCodes.Add(code);
                var catalog = string.IsNullOrWhiteSpace(roll.CatalogNumber) ? null : roll.CatalogNumber.Trim();
                var color = string.IsNullOrWhiteSpace(roll.FabricColor) ? null : roll.FabricColor.Trim();
                var width = roll.FabricWidth;
                var yards = roll.QuantityYards;
                var yardPrice = roll.YardPrice;
                var inchPrice = Math.Round(yardPrice / 36m, 4);
                var rollCost = Math.Round(yards * yardPrice, 2);

                totalYards += yards;
                totalCost += rollCost;

                int itemId;
                using (var checkCmd = new SqlCommand(@"
                    SELECT InventoryItemID, Category
                    FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK)
                    WHERE ItemCode = @code", connection, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@code", code);
                    using var reader = await checkCmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        itemId = reader.GetInt32(0);
                        var category = reader.GetString(1);
                        if (!string.Equals(category, "Fabric", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"كود القماش '{code}' موجود بالفعل في فئة أخرى: {category}");
                        }

                        await reader.DisposeAsync();
                        using var updateCmd = new SqlCommand(@"
                            UPDATE dbo.InventoryItems
                            SET ItemName = @name,
                                CurrentQuantity = CurrentQuantity + @qty,
                                AvailableQuantity = AvailableQuantity + @qty,
                                UpdatedAt = @now,
                                Barcode = ISNULL(@barcode, Barcode),
                                FabricCategory = @name,
                                FabricColor = ISNULL(@color, FabricColor),
                                FabricWidth = @width,
                                FabricWidthUnit = N'Inch',
                                InchPrice = @inchPrice,
                                YardPrice = @yardPrice
                            WHERE InventoryItemID = @id", connection, transaction);
                        updateCmd.Parameters.AddWithValue("@id", itemId);
                        updateCmd.Parameters.AddWithValue("@name", name);
                        updateCmd.Parameters.AddWithValue("@qty", yards);
                        updateCmd.Parameters.AddWithValue("@now", now);
                        AddNullable(updateCmd, "@barcode", catalog);
                        AddNullable(updateCmd, "@color", color);
                        updateCmd.Parameters.AddWithValue("@width", width);
                        updateCmd.Parameters.AddWithValue("@inchPrice", inchPrice);
                        updateCmd.Parameters.AddWithValue("@yardPrice", yardPrice);
                        await updateCmd.ExecuteNonQueryAsync(ct);
                    }
                    else
                    {
                        await reader.DisposeAsync();
                        using var insertCmd = new SqlCommand(@"
                            INSERT INTO dbo.InventoryItems
                                (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
                            OUTPUT INSERTED.InventoryItemID
                            VALUES
                                (@code, @name, N'Fabric', N'Yard', @qty, @qty, 0, 1, @now, @barcode, @name, @color, @width, N'Inch', @inchPrice, @yardPrice)", connection, transaction);
                        insertCmd.Parameters.AddWithValue("@code", code);
                        insertCmd.Parameters.AddWithValue("@name", name);
                        insertCmd.Parameters.AddWithValue("@qty", yards);
                        insertCmd.Parameters.AddWithValue("@now", now);
                        AddNullable(insertCmd, "@barcode", catalog);
                        AddNullable(insertCmd, "@color", color);
                        insertCmd.Parameters.AddWithValue("@width", width);
                        insertCmd.Parameters.AddWithValue("@inchPrice", inchPrice);
                        insertCmd.Parameters.AddWithValue("@yardPrice", yardPrice);
                        itemId = (int)(await insertCmd.ExecuteScalarAsync(ct))!;
                    }
                }

                var rollNotes = $"مورد: {supplierName} | فاتورة: {batch.InvoiceNumber.Trim()} | رول {i + 1} ({name} - {color ?? "-"}){(string.IsNullOrWhiteSpace(batch.Notes) ? "" : " | " + batch.Notes.Trim())}";
                using (var txCmd = new SqlCommand(@"
                    INSERT INTO dbo.InventoryTransactions
                        (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
                    VALUES
                        (@itemId, N'Receive', @qty, @ref, @notes, @now, @cost, @unitCost)", connection, transaction))
                {
                    txCmd.Parameters.AddWithValue("@itemId", itemId);
                    txCmd.Parameters.AddWithValue("@qty", yards);
                    txCmd.Parameters.AddWithValue("@ref", reference);
                    txCmd.Parameters.AddWithValue("@notes", rollNotes);
                    txCmd.Parameters.AddWithValue("@now", now);
                    txCmd.Parameters.AddWithValue("@cost", rollCost);
                    txCmd.Parameters.AddWithValue("@unitCost", yardPrice);
                    await txCmd.ExecuteNonQueryAsync(ct);
                }

                try
                {
                    using var fiCmd = new SqlCommand(@"
                        IF OBJECT_ID('dbo.Fabrics_Inventory', 'U') IS NOT NULL
                        BEGIN
                            IF EXISTS (SELECT 1 FROM dbo.Fabrics_Inventory WITH (UPDLOCK, HOLDLOCK) WHERE FabricName = @name OR FabricCode = TRY_CAST(@code AS int))
                            BEGIN
                                UPDATE dbo.Fabrics_Inventory
                                SET QuantityYard = ISNULL(QuantityYard, 0) + @qty,
                                    QuantityInch = ISNULL(QuantityInch, 0) + (@qty * 36),
                                    TotalRollCost = ISNULL(TotalRollCost, 0) + @cost,
                                    PricePerYard = @yardPrice,
                                    PricePerInch = @inchPrice,
                                    AvailableQuantity = ISNULL(AvailableQuantity, 0) + @qty,
                                    Color = ISNULL(@color, Color)
                                WHERE FabricName = @name OR FabricCode = TRY_CAST(@code AS int)
                            END
                            ELSE
                            BEGIN
                                INSERT INTO dbo.Fabrics_Inventory
                                    (FabricName, Unit, Color, QuantityYard, QuantityInch, TotalRollCost, PricePerYard, PricePerInch, UsedQuantity, AvailableQuantity)
                                VALUES
                                    (@name, N'ياردة', @color, @qty, @qty * 36, @cost, @yardPrice, @inchPrice, 0, @qty)
                            END
                        END", connection, transaction);
                    fiCmd.Parameters.AddWithValue("@name", name);
                    fiCmd.Parameters.AddWithValue("@code", code);
                    fiCmd.Parameters.AddWithValue("@qty", yards);
                    fiCmd.Parameters.AddWithValue("@cost", rollCost);
                    fiCmd.Parameters.AddWithValue("@yardPrice", yardPrice);
                    fiCmd.Parameters.AddWithValue("@inchPrice", inchPrice);
                    AddNullable(fiCmd, "@color", color);
                    await fiCmd.ExecuteNonQueryAsync(ct);
                }
                catch
                {
                    // Non-fatal if legacy table structure differs
                }
            }

            await transaction.CommitAsync(ct);

            return new FabricBatchResultDto(
                batch.Rolls.Count,
                totalYards,
                totalCost,
                reference,
                now
            );
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value is string text ? (string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim()) : value ?? DBNull.Value);

    private static InventoryItemDto MapItem(SqlDataReader r) => new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetBoolean(8), r.GetDateTime(9), r.NullableDateTime("UpdatedAt"), r.NullableString("Barcode"), r.NullableString("FabricCategory"), r.NullableString("FabricColor"), r.NullableDecimal("FabricWidth"), r.NullableString("FabricWidthUnit"), r.NullableDecimal("InchPrice"), r.NullableDecimal("YardPrice"));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? id, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id is not null) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}