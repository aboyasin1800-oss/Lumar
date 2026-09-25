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

                await InsertInventoryTransactionAsync(connection, transaction, existing.ProductCode, updatedQuantity, product.PurchasePrice, product.SellingPrice, product.SupplierId, notes, now, ct);

                await transaction.CommitAsync(ct);
                return new ImportedReadyMadeProductDto(existing.ImportedReadyMadeProductId, productName, productType, existing.ProductCode, unit, updatedQuantity, updatedPurchasePrice, updatedSellingPrice, true, existing.AlertThreshold, notes, category, existing.CreatedAt, now);
            }

            if (existing is not null && !product.RenewExisting)
            {
                throw new InvalidOperationException($"المنتج '{productCode}' موجود بالفعل في المخزون المستورد.");
            }

            var result = await InsertImportedProductAsync(connection, transaction, productName, productType, productCode, unit, product.Quantity, product.PurchasePrice, product.SellingPrice, notes, category, product.SupplierId, now, ct);
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

    private static async Task<ImportedReadyMadeProductDto> InsertImportedProductAsync(SqlConnection connection, SqlTransaction transaction, string productName, string productType, string productCode, string unit, decimal quantity, decimal purchasePrice, decimal sellingPrice, string? notes, string category, int? supplierId, DateTime now, CancellationToken ct)
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
        await InsertInventoryTransactionAsync(connection, transaction, productCode, quantity, purchasePrice, sellingPrice, supplierId, notes, now, ct);

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

    private static async Task InsertInventoryTransactionAsync(SqlConnection connection, SqlTransaction transaction, string productCode, decimal quantity, decimal purchasePrice, decimal sellingPrice, int? supplierId, string? notes, DateTime now, CancellationToken ct)
    {
        var reference = $"IMPORTED-{productCode}-{now:yyyyMMddHHmmss}";
        var itemId = await GetOrCreateImportedInventoryItemIdAsync(connection, transaction, productCode, now, ct);

        using var cmd = new SqlCommand(@"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
            VALUES
                (@itemId, N'Receive', @qty, @reference, @notes, @now, @cost, @unitCost)", connection, transaction);
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@reference", reference);
        AddNullable(cmd, "@notes", $"{(supplierId is > 0 ? $"SupplierId:{supplierId}" : "Supplier:NotSet")} | {notes ?? "Imported product receipt"}");
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@cost", quantity * purchasePrice);
        cmd.Parameters.AddWithValue("@unitCost", purchasePrice);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> GetOrCreateImportedInventoryItemIdAsync(SqlConnection connection, SqlTransaction transaction, string productCode, DateTime now, CancellationToken ct)
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
        insertCmd.Parameters.AddWithValue("@name", productCode);
        insertCmd.Parameters.AddWithValue("@unit", "حبة");
        insertCmd.Parameters.AddWithValue("@now", now);
        var insertedValue = await insertCmd.ExecuteScalarAsync(ct);
        return insertedValue is int insertedId ? insertedId : throw new InvalidOperationException("تعذر إنشاء سجل مخزون للمنتج المستورد.");
    }

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