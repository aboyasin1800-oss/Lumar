using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FabricConsumablesFoundationIntegrationTests
{
    [Fact]
    public async Task FoundationReceiptAndConsumption_AreBalancedPreciseAndIdempotent()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var fabricReceipt = await repository.ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "COTTON-TEST",
                    RollCode = seed.FabricRollCode,
                    ColorValue = "WHITE",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                },
                CancellationToken.None);
            Assert.NotNull(fabricReceipt);
            Assert.False(fabricReceipt.IsExisting);
            Assert.Equal(6m, fabricReceipt.Quantity);
            Assert.Equal(153m, fabricReceipt.OperationalAmount);
            Assert.Equal(153m, fabricReceipt.PostingAmount);
            Assert.Equal(7, await ReadEventTypeAsync(connectionString, fabricReceipt.AccountingEventId));
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, fabricReceipt.AccountingEventId));
            var fabricRollId = await ReadLongAsync(connectionString, "SELECT FabricRollId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId=@id", fabricReceipt.SourceRecordId);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=fr.InventoryItemId INNER JOIN dbo.InventoryItemFoundation f ON f.InventoryItemId=fr.InventoryItemId AND f.InventoryClassId=1 WHERE fr.FabricRollId=@id AND i.IsActive=1", fabricRollId));

            var fabricReceiptRetry = await repository.ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "COTTON-TEST",
                    RollCode = seed.FabricRollCode,
                    UnitId = 1,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                },
                CancellationToken.None);
            Assert.True(fabricReceiptRetry!.IsExisting);
            Assert.Equal(fabricReceipt.AccountingEventId, fabricReceiptRetry.AccountingEventId);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventId=@id", fabricReceipt.AccountingEventId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Fabrics_Inventory WHERE FabricName=@name", seed.FabricItemCode));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReceiveConsumableInventoryAsync(
                new ReceiveConsumableInventoryDto
                {
                    GoodsReceiptItemId = seed.ConsumableReceiptItemId,
                    ItemCode = seed.ConsumableItemCode,
                    UnitId = 3,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                },
                CancellationToken.None));

            var consumableReceipt = await repository.ReceiveConsumableInventoryAsync(
                new ReceiveConsumableInventoryDto
                {
                    GoodsReceiptItemId = seed.ConsumableReceiptItemId,
                    ItemCode = seed.ConsumableItemCode,
                    UnitId = 3,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.ConsumableReceiptOperationId
                },
                CancellationToken.None);
            Assert.NotNull(consumableReceipt);
            Assert.Equal(9, await ReadEventTypeAsync(connectionString, consumableReceipt.AccountingEventId));
            Assert.Equal(36m, consumableReceipt.PostingAmount);
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, consumableReceipt.AccountingEventId));

            var fabricConsumption = await repository.ConsumeFabricInventoryAsync(
                new ConsumeFabricInventoryDto
                {
                    FabricRollId = fabricRollId,
                    OrderItemId = seed.OrderItemId,
                    PieceId = seed.PieceId,
                    Quantity = 1.234567m,
                    SourceOperationId = seed.FabricConsumptionOperationId
                },
                CancellationToken.None);
            Assert.NotNull(fabricConsumption);
            Assert.Equal(1.234567m, fabricConsumption.Quantity);
            Assert.Equal(31.481459m, fabricConsumption.OperationalAmount);
            Assert.Equal(31.48m, fabricConsumption.PostingAmount);
            Assert.Equal(8, await ReadEventTypeAsync(connectionString, fabricConsumption.AccountingEventId));
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, fabricConsumption.AccountingEventId));

            var fabricConsumptionRetry = await repository.ConsumeFabricInventoryAsync(
                new ConsumeFabricInventoryDto
                {
                    FabricRollId = fabricRollId,
                    OrderItemId = seed.OrderItemId,
                    PieceId = seed.PieceId,
                    Quantity = 1.234567m,
                    SourceOperationId = seed.FabricConsumptionOperationId
                },
                CancellationToken.None);
            Assert.True(fabricConsumptionRetry!.IsExisting);
            Assert.Equal(fabricConsumption.AccountingEventId, fabricConsumptionRetry.AccountingEventId);

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ConsumeFabricInventoryAsync(
                new ConsumeFabricInventoryDto
                {
                    FabricRollId = fabricRollId,
                    OrderItemId = seed.OrderItemId,
                    PieceId = seed.PieceId,
                    Quantity = 1.234568m,
                    SourceOperationId = seed.FabricConsumptionOperationId
                },
                CancellationToken.None));

            var consumableConsumption = await repository.ConsumeConsumableInventoryAsync(
                new ConsumeConsumableInventoryDto
                {
                    ProductionOrderId = seed.ProductionOrderId,
                    InventoryItemId = consumableReceipt.InventoryItemId,
                    Quantity = 1.125m,
                    UnitId = 3,
                    SourceOperationId = seed.ConsumableConsumptionOperationId
                },
                CancellationToken.None);
            Assert.NotNull(consumableConsumption);
            Assert.Equal(13.5m, consumableConsumption.OperationalAmount);
            Assert.Equal(13.5m, consumableConsumption.PostingAmount);
            Assert.Equal(10, await ReadEventTypeAsync(connectionString, consumableConsumption.AccountingEventId));
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, consumableConsumption.AccountingEventId));

            var consumableConsumptionRetry = await repository.ConsumeConsumableInventoryAsync(
                new ConsumeConsumableInventoryDto
                {
                    ProductionOrderId = seed.ProductionOrderId,
                    InventoryItemId = consumableReceipt.InventoryItemId,
                    Quantity = 1.125m,
                    UnitId = 3,
                    SourceOperationId = seed.ConsumableConsumptionOperationId
                },
                CancellationToken.None);
            Assert.True(consumableConsumptionRetry!.IsExisting);
            Assert.Equal(consumableConsumption.AccountingEventId, consumableConsumptionRetry.AccountingEventId);

            Assert.Equal(4.765433m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.FabricRolls WHERE FabricRollId=@id", fabricRollId));
            Assert.Equal(1.875m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", consumableReceipt.InventoryItemId));
            Assert.Equal(4, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType IN (7,8,9,10) AND (InventoryReceiptPostingId IS NOT NULL OR FabricConsumptionSourceId IS NOT NULL OR ProductionMaterialConsumptionId IS NOT NULL)", null));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationConsumption_RejectsInsufficientQuantityWithoutArtifacts()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var receipt = await repository.ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "COTTON-ROLLBACK",
                    RollCode = seed.FabricRollCode,
                    UnitId = 1,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                },
                CancellationToken.None);
            var fabricRollId = await ReadLongAsync(connectionString, "SELECT FabricRollId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId=@id", receipt!.SourceRecordId);

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ConsumeFabricInventoryAsync(
                new ConsumeFabricInventoryDto
                {
                    FabricRollId = fabricRollId,
                    OrderItemId = seed.OrderItemId,
                    Quantity = 6.000001m,
                    SourceOperationId = seed.FabricConsumptionOperationId
                },
                CancellationToken.None));

            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.FabricRolls WHERE FabricRollId=@id", fabricRollId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@operation", seed.FabricConsumptionOperationId));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    private static InventoryRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new InventoryRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_FOUNDATION_GAP_TEST", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fabric foundation tests are restricted to the approved test databases.");
        return builder.ConnectionString;
    }

    private static async Task<TestSeed> SeedAsync(string connectionString)
    {
        await CleanupLeakedSeedsAsync(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var seed = new TestSeed(
            $"FAB-FOUND-{suffix[..12]}",
            $"CON-FOUND-{suffix[..12]}",
            $"ROLL-FOUND-{suffix[..12]}",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var purchaseOrderId = await ReadFirstIdAsync(connection, transaction, "SELECT TOP(1) PurchaseOrderId FROM dbo.PurchaseOrders ORDER BY PurchaseOrderId");
            var supplierId = await ReadFirstIdAsync(connection, transaction, "SELECT TOP(1) SupplierId FROM dbo.Suppliers ORDER BY SupplierId");
            var orderId = await ReadFirstIdAsync(connection, transaction, "SELECT TOP(1) OrderID FROM dbo.Orders ORDER BY OrderID");
            var goodsReceiptId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.GoodsReceipts (SupplierId,PurchaseOrderId,ReceiptNumber,ReceiptDate,Notes,CreatedAt)
                OUTPUT INSERTED.GoodsReceiptId
                VALUES (@supplierId,@purchaseOrderId,@receiptNumber,@now,N'Foundation integration seed',@now)",
                ("@supplierId", supplierId), ("@purchaseOrderId", purchaseOrderId), ("@receiptNumber", $"GR-FOUND-{suffix}"), ("@now", now));
            var fabricReceiptItemId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId,ItemName,ReceivedQuantity,UnitCost,LineTotal)
                OUTPUT INSERTED.GoodsReceiptItemId
                VALUES (@receiptId,N'Foundation Fabric',6.000000,25.500000,153.000000)", ("@receiptId", goodsReceiptId));
            var consumableReceiptItemId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId,ItemName,ReceivedQuantity,UnitCost,LineTotal)
                OUTPUT INSERTED.GoodsReceiptItemId
                VALUES (@receiptId,N'Foundation Consumable',3.000000,12.000000,36.000000)", ("@receiptId", goodsReceiptId));
            var orderItemId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,FabricCode,FabricType,FabricColor,PieceStatus,CreatedDate)
                OUTPUT INSERTED.OrderItemID
                VALUES (@orderId,N'Foundation Test',1,@fabricCode,N'Foundation',N'White',N'New',@now)", ("@orderId", orderId), ("@fabricCode", seed.FabricItemCode), ("@now", now));
            var pieceId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                OUTPUT INSERTED.PieceID
                VALUES (@orderItemId,@tracking,N'New',1,@now)", ("@orderItemId", orderItemId), ("@tracking", $"TRK-FOUND-{suffix}"), ("@now", now));
            var productId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Products (ProductCode,ProductName,Category,Unit,IsActive,CreatedAt)
                OUTPUT INSERTED.ProductId
                VALUES (@code,N'Foundation Test Product',N'Test',N'Piece',1,@now)", ("@code", $"PROD-FOUND-{suffix}"), ("@now", now));
            var bomId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.BillOfMaterials (ProductId,Version,IsDefault,IsActive,Notes,CreatedAt)
                OUTPUT INSERTED.BillOfMaterialsId
                VALUES (@productId,N'1',1,1,N'Foundation integration seed',@now)", ("@productId", productId), ("@now", now));
            var productionOrderId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.ProductionOrders (ProductionOrderNumber,ProductId,BillOfMaterialsId,PlannedQuantity,ProducedQuantity,Status,PlannedStartDate,CreatedAt)
                OUTPUT INSERTED.ProductionOrderId
                VALUES (@number,@productId,@bomId,1,0,N'Planned',@now,@now)", ("@number", $"PO-FOUND-{suffix}"), ("@productId", productId), ("@bomId", bomId), ("@now", now));
            await transaction.CommitAsync();
            return seed with
            {
                FabricReceiptItemId = fabricReceiptItemId,
                ConsumableReceiptItemId = consumableReceiptItemId,
                OrderItemId = orderItemId,
                PieceId = pieceId,
                ProductionOrderId = productionOrderId,
                GoodsReceiptId = goodsReceiptId,
                ProductId = productId,
                BillOfMaterialsId = bomId
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, TestSeed seed)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @eventIds TABLE (AccountingEventId bigint);
            INSERT INTO @eventIds
            SELECT AccountingEventId FROM dbo.AccountingEvents
            WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation))
               OR FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@fabricConsumptionOperation)
               OR ProductionMaterialConsumptionId IN (SELECT ProductionMaterialConsumptionId FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId=@consumableConsumptionOperation);
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation);
            UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation);
            UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL,InventoryTransactionId=NULL,FabricRollId=NULL WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation));
            UPDATE dbo.FabricConsumptionSources SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE SourceOperationId=@fabricConsumptionOperation;
            UPDATE dbo.ProductionMaterialConsumptions SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE SourceOperationId=@consumableConsumptionOperation;
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.InventoryTransactions WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation);
            DELETE FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@fabricConsumptionOperation;
            DELETE FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId=@consumableConsumptionOperation;
            DELETE FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation)));
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation));
            DELETE FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation);
            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (SELECT InventoryItemID FROM dbo.InventoryItems WHERE ItemCode IN (@fabricCode,@consumableCode));
            DELETE FROM dbo.InventoryItems WHERE ItemCode IN (@fabricCode,@consumableCode);
            DELETE FROM dbo.Pieces WHERE PieceID=@pieceId;
            DELETE FROM dbo.OrderItems WHERE OrderItemID=@orderItemId;
            DELETE FROM dbo.ProductionOrders WHERE ProductionOrderId=@productionOrderId;
            DELETE FROM dbo.BillOfMaterials WHERE BillOfMaterialsId=@bomId;
            DELETE FROM dbo.Products WHERE ProductId=@productId;
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);
            DELETE FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@goodsReceiptId;", connection);
        command.Parameters.AddWithValue("@fabricReceiptOperation", seed.FabricReceiptOperationId);
        command.Parameters.AddWithValue("@consumableReceiptOperation", seed.ConsumableReceiptOperationId);
        command.Parameters.AddWithValue("@fabricConsumptionOperation", seed.FabricConsumptionOperationId);
        command.Parameters.AddWithValue("@consumableConsumptionOperation", seed.ConsumableConsumptionOperationId);
        command.Parameters.AddWithValue("@fabricCode", seed.FabricItemCode);
        command.Parameters.AddWithValue("@consumableCode", seed.ConsumableItemCode);
        command.Parameters.AddWithValue("@pieceId", seed.PieceId);
        command.Parameters.AddWithValue("@orderItemId", seed.OrderItemId);
        command.Parameters.AddWithValue("@productionOrderId", seed.ProductionOrderId);
        command.Parameters.AddWithValue("@bomId", seed.BillOfMaterialsId);
        command.Parameters.AddWithValue("@productId", seed.ProductId);
        command.Parameters.AddWithValue("@fabricReceiptItemId", seed.FabricReceiptItemId);
        command.Parameters.AddWithValue("@consumableReceiptItemId", seed.ConsumableReceiptItemId);
        command.Parameters.AddWithValue("@goodsReceiptId", seed.GoodsReceiptId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupLeakedSeedsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @postingIds TABLE (InventoryReceiptPostingId bigint);
            DECLARE @lineIds TABLE (InventoryReceiptLineId bigint);
            DECLARE @rollIds TABLE (FabricRollId bigint);
            DECLARE @itemIds TABLE (InventoryItemId int);
            DECLARE @eventIds TABLE (AccountingEventId bigint);
            INSERT INTO @postingIds
            SELECT irp.InventoryReceiptPostingId
            FROM dbo.InventoryReceiptPostings irp
            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId=irp.GoodsReceiptItemId
            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId=gri.GoodsReceiptId
            WHERE gr.ReceiptNumber LIKE N'GR-FOUND-%';
            INSERT INTO @lineIds SELECT InventoryReceiptLineId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
                INSERT INTO @rollIds SELECT FabricRollId FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
                INSERT INTO @itemIds SELECT InventoryItemID FROM dbo.InventoryItems WHERE ItemCode LIKE N'FAB-FOUND-%' OR ItemCode LIKE N'CON-FOUND-%';
                INSERT INTO @eventIds
                SELECT AccountingEventId FROM dbo.AccountingEvents
                WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds)
                    OR FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM dbo.FabricConsumptionSources WHERE FabricRollId IN (SELECT FabricRollId FROM @rollIds))
                    OR ProductionMaterialConsumptionId IN (SELECT ProductionMaterialConsumptionId FROM dbo.ProductionMaterialConsumptions WHERE InventoryItemId IN (SELECT InventoryItemId FROM @itemIds));
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE SourceOperationId IN (SELECT SourceOperationId FROM dbo.InventoryReceiptPostings WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds));
            UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL,InventoryTransactionId=NULL,FabricRollId=NULL WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.InventoryTransactions WHERE SourceOperationId IN (SELECT SourceOperationId FROM dbo.InventoryReceiptPostings WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds));
            UPDATE dbo.FabricConsumptionSources SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE FabricRollId IN (SELECT FabricRollId FROM @rollIds);
            UPDATE dbo.ProductionMaterialConsumptions SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE InventoryItemId IN (SELECT InventoryItemId FROM @itemIds);
            DELETE FROM dbo.FabricConsumptionSources WHERE FabricRollId IN (SELECT FabricRollId FROM @rollIds);
            DELETE FROM dbo.ProductionMaterialConsumptions WHERE InventoryItemId IN (SELECT InventoryItemId FROM @itemIds);
            DELETE FROM dbo.FabricRolls WHERE FabricRollId IN (SELECT FabricRollId FROM @rollIds) OR InventoryItemId IN (SELECT InventoryItemId FROM @itemIds);
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.InventoryReceiptPostings WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (SELECT InventoryItemId FROM @itemIds);
            DELETE FROM dbo.InventoryItems WHERE InventoryItemId IN (SELECT InventoryItemId FROM @itemIds);
            DELETE FROM dbo.Pieces WHERE TrackingCode LIKE N'TRK-FOUND-%';
            DELETE FROM dbo.OrderItems WHERE FabricCode LIKE N'FAB-FOUND-%';
            DELETE FROM dbo.ProductionOrders WHERE ProductionOrderNumber LIKE N'PO-FOUND-%';
            DELETE FROM dbo.BillOfMaterials WHERE Notes=N'Foundation integration seed' AND ProductId IN (SELECT ProductId FROM dbo.Products WHERE ProductCode LIKE N'PROD-FOUND-%');
            DELETE FROM dbo.Products WHERE ProductCode LIKE N'PROD-FOUND-%';
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE ReceiptNumber LIKE N'GR-FOUND-%');
            DELETE FROM dbo.GoodsReceipts WHERE ReceiptNumber LIKE N'GR-FOUND-%';", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadFirstIdAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> InsertScalarAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ReadEventTypeAsync(string connectionString, long eventId) =>
        await ReadIntAsync(connectionString, "SELECT AccountingEventType FROM dbo.AccountingEvents WHERE AccountingEventId=@id", eventId);

    private static async Task<decimal> ReadJournalDifferenceAsync(string connectionString, long eventId) =>
        await ReadDecimalAsync(connectionString, "SELECT COALESCE(SUM(jel.DebitAmount),0)-COALESCE(SUM(jel.CreditAmount),0) FROM dbo.JournalEntryLines jel INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId WHERE je.AccountingEventId=@id", eventId);

    private static async Task<int> CountAsync(string connectionString, string sql, object? value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        if (sql.Contains("@id", StringComparison.Ordinal)) command.Parameters.AddWithValue("@id", value ?? DBNull.Value);
        if (sql.Contains("@name", StringComparison.Ordinal)) command.Parameters.AddWithValue("@name", value ?? DBNull.Value);
        if (sql.Contains("@operation", StringComparison.Ordinal)) command.Parameters.AddWithValue("@operation", value ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ReadIntAsync(string connectionString, string sql, long value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, long value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<long> ReadLongAsync(string connectionString, string sql, long value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed record TestSeed(
        string FabricItemCode,
        string ConsumableItemCode,
        string FabricRollCode,
        Guid FabricReceiptOperationId,
        Guid ConsumableReceiptOperationId,
        Guid FabricConsumptionOperationId,
        Guid ConsumableConsumptionOperationId)
    {
        public int FabricReceiptItemId { get; init; }
        public int ConsumableReceiptItemId { get; init; }
        public int OrderItemId { get; init; }
        public int PieceId { get; init; }
        public int ProductionOrderId { get; init; }
        public int GoodsReceiptId { get; init; }
        public int ProductId { get; init; }
        public int BillOfMaterialsId { get; init; }
    }
}
