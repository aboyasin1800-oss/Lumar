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
    public async Task OfficialFabricBatch_CreatesPurchaseReceiptFoundationAndBalancedEntry()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var invoiceNumber = $"FOUND-BATCH-{suffix}";
        var fabricCode = $"FAB-BATCH-{suffix}";

        try
        {
            var result = await CreateRepository(connectionString).ReceiveFabricBatchAsync(
                new CreateFabricBatchDto
                {
                    SupplierId = support.SupplierId,
                    InvoiceNumber = invoiceNumber,
                    PurchaseDate = DateTime.UtcNow,
                    Rolls = [new CreateFabricRollDto
                    {
                        FabricCode = fabricCode,
                        CatalogNumber = "CAT-FOUND",
                        FabricType = "Foundation batch fabric",
                        FabricColor = "Blue",
                        FabricWidth = 58m,
                        QuantityYards = 50m,
                        YardPrice = 1200m
                    }]
                },
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(50m, result!.TotalYards);
            Assert.Equal(60000m, result.TotalCost);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode=@name", fabricCode));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItemFoundation f INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=f.InventoryItemId WHERE i.ItemCode=@name AND f.InventoryClassId=1", fabricCode));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode=@name AND AvailableQuantity=50", fabricCode));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId=ae.InventoryReceiptPostingId INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId=rp.GoodsReceiptItemId INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId=gri.GoodsReceiptId WHERE gr.ReceiptNumber=@name AND ae.AccountingEventType=7 AND ae.PostingAmount=60000", $"FAB-{invoiceNumber}"));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=7 AND debitAccount.AccountCode=N'1101' AND creditAccount.AccountCode=N'2100' AND debitLine.DebitAmount=60000 AND creditLine.CreditAmount=60000 AND je.ReferenceNumber LIKE @name", $"GoodsReceipt:FAB-{invoiceNumber}:%"));

            var retry = await CreateRepository(connectionString).ReceiveFabricBatchAsync(
                new CreateFabricBatchDto
                {
                    SupplierId = support.SupplierId,
                    InvoiceNumber = invoiceNumber,
                    Rolls = [new CreateFabricRollDto
                    {
                        FabricCode = fabricCode,
                        FabricType = "Foundation batch fabric",
                        FabricWidth = 58m,
                        QuantityYards = 50m,
                        YardPrice = 1200m
                    }]
                },
                CancellationToken.None);

            Assert.NotNull(retry);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE ReceiptNumber=@name", $"FAB-{invoiceNumber}"));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode=@name", fabricCode));
        }
        finally
        {
            await CleanupOfficialFabricBatchAsync(connectionString, invoiceNumber, fabricCode);
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

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
            Assert.Equal(4, await CountAsync(connectionString, @"
                SELECT COUNT(*)
                FROM dbo.AccountingEvents
                WHERE AccountingEventType IN (7,8,9,10)
                  AND (
                      InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation))
                      OR FabricConsumptionSourceId IN (SELECT FabricConsumptionSourceId FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@fabricConsumptionOperation)
                      OR ProductionMaterialConsumptionId IN (SELECT ProductionMaterialConsumptionId FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId=@consumableConsumptionOperation)
                  )", seed));
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
            && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_FOUNDATION_GAP_TEST", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_CUSTOMERS_ONLY_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fabric foundation tests are restricted to the approved test databases.");
        return builder.ConnectionString;
    }

    private static async Task<TestSeed> SeedAsync(string connectionString)
    {
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
            await SeedLedgerAccountsAsync(connection, transaction);
            var supplierId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Suppliers (SupplierCode, SupplierName, IsActive, CreatedAt)
                OUTPUT INSERTED.SupplierId
                VALUES (@code, N'مورد اختبار Fabric', 1, @now);", ("@code", $"SF-{suffix[..12]}"), ("@now", now));
            var customerId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, IsActive)
                OUTPUT INSERTED.CustomerID
                VALUES (@code, N'عميل اختبار Fabric', @phone, 0, 0, 0, 1);",
                ("@code", $"CF-{suffix[..12]}"), ("@phone", $"012{suffix[..9]}"));
            var orderId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Orders
                    (OrderNumber, CustomerID, OrderDate, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, CreatedDate, SaleCategory, RevenueRecognized, RevenueReversalCreated)
                OUTPUT INSERTED.OrderID
                VALUES (@number, @customerId, @now, 0, 0, 0, 0, N'Normal', N'New', @now, N'Custom', 0, 0);",
                ("@number", $"ORD-FOUND-{suffix[..12]}"), ("@customerId", customerId), ("@now", now));
            var purchaseOrderId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.PurchaseOrders (PurchaseOrderNumber, SupplierId, OrderDate, Status, TotalAmount, CreatedAt)
                OUTPUT INSERTED.PurchaseOrderId
                VALUES (@number, @supplierId, @now, N'New', 189, @now);",
                ("@number", $"PO-FOUND-{suffix[..12]}"), ("@supplierId", supplierId), ("@now", now));
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
                BillOfMaterialsId = bomId,
                SupplierId = supplierId,
                CustomerId = customerId,
                PurchaseOrderId = purchaseOrderId,
                OrderId = orderId
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
            DELETE FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@goodsReceiptId;
            DELETE FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@purchaseOrderId;
            DELETE FROM dbo.Orders WHERE OrderID=@orderId;
            DELETE FROM dbo.Suppliers WHERE SupplierId=@supplierId;
            DELETE FROM dbo.Customers WHERE CustomerID=@customerId;", connection);
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
        command.Parameters.AddWithValue("@purchaseOrderId", seed.PurchaseOrderId);
        command.Parameters.AddWithValue("@orderId", seed.OrderId);
        command.Parameters.AddWithValue("@supplierId", seed.SupplierId);
        command.Parameters.AddWithValue("@customerId", seed.CustomerId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<OfficialBatchSupportSeed> SeedOfficialBatchSupportAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await SeedLedgerAccountsAsync(connection, transaction);
            var supplierId = await InsertScalarAsync(connection, transaction, @"
                INSERT INTO dbo.Suppliers (SupplierCode, SupplierName, IsActive, CreatedAt)
                OUTPUT INSERTED.SupplierId
                VALUES (@code, N'مورد اختبار دفعة Fabric', 1, SYSUTCDATETIME());", ("@code", $"SB-{suffix}"));
            await transaction.CommitAsync();
            return new OfficialBatchSupportSeed(supplierId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupOfficialBatchSupportAsync(string connectionString, OfficialBatchSupportSeed support)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DELETE FROM dbo.Suppliers WHERE SupplierId = @supplierId;", connection);
        command.Parameters.AddWithValue("@supplierId", support.SupplierId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupOfficialFabricBatchAsync(string connectionString, string invoiceNumber, string fabricCode)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @receiptId int = (SELECT TOP (1) GoodsReceiptId FROM dbo.GoodsReceipts WHERE ReceiptNumber=@receiptNumber);
            DECLARE @purchaseOrderId int = (SELECT TOP (1) PurchaseOrderId FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@receiptId);
            DECLARE @postingIds TABLE (InventoryReceiptPostingId bigint PRIMARY KEY);
            DECLARE @lineIds TABLE (InventoryReceiptLineId bigint PRIMARY KEY);
            DECLARE @eventIds TABLE (AccountingEventId bigint PRIMARY KEY);
            DECLARE @transactionIds TABLE (TransactionId int PRIMARY KEY);
            INSERT INTO @postingIds SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@receiptId);
            INSERT INTO @lineIds SELECT InventoryReceiptLineId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            INSERT INTO @eventIds SELECT AccountingEventId FROM dbo.AccountingEvents WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            INSERT INTO @transactionIds SELECT InventoryTransactionId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds) AND InventoryTransactionId IS NOT NULL;
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE TransactionID IN (SELECT TransactionId FROM @transactionIds);
            UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL,InventoryTransactionId=NULL,FabricRollId=NULL WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.InventoryTransactions WHERE TransactionID IN (SELECT TransactionId FROM @transactionIds);
            DELETE FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.InventoryReceiptPostings WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (SELECT InventoryItemID FROM dbo.InventoryItems WHERE ItemCode=@fabricCode);
            DELETE FROM dbo.InventoryItems WHERE ItemCode=@fabricCode;
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@receiptId;
            DELETE FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@receiptId;
            DELETE FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId=@purchaseOrderId;
            DELETE FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@purchaseOrderId;", connection);
        command.Parameters.AddWithValue("@receiptNumber", $"FAB-{invoiceNumber}");
        command.Parameters.AddWithValue("@fabricCode", fabricCode);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadFirstSupplierIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP (1) SupplierId FROM dbo.Suppliers ORDER BY SupplierId", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SeedLedgerAccountsAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand(@"
            IF (SELECT COUNT(*) FROM dbo.LedgerAccounts WHERE IsActive = 1 AND AccountCode IN (N'1000',N'1101',N'1102',N'1130',N'2100',N'5300')) <> 6
                THROW 51002, N'Fabric integration fixture requires the six reference ledger accounts.', 1;", connection, transaction);
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
        if (value is TestSeed seed)
        {
            command.Parameters.AddWithValue("@fabricReceiptOperation", seed.FabricReceiptOperationId);
            command.Parameters.AddWithValue("@consumableReceiptOperation", seed.ConsumableReceiptOperationId);
            command.Parameters.AddWithValue("@fabricConsumptionOperation", seed.FabricConsumptionOperationId);
            command.Parameters.AddWithValue("@consumableConsumptionOperation", seed.ConsumableConsumptionOperationId);
        }
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
        public int SupplierId { get; init; }
        public int CustomerId { get; init; }
        public int PurchaseOrderId { get; init; }
        public int OrderId { get; init; }
    }

    private sealed record OfficialBatchSupportSeed(int SupplierId);
}
