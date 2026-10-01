using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FabricConsumablesFoundationIntegrationTests(ITestOutputHelper output)
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
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item AND StorageOperationId=@operation AND InventoryReceiptPostingId=@posting AND InventoryTransactionId=@transaction",
                ("@item", seed.FabricReceiptItemId),
                ("@operation", seed.FabricReceiptOperationId),
                ("@posting", fabricReceipt.SourceRecordId),
                ("@transaction", fabricReceipt.InventoryTransactionId)));
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
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item AND StorageOperationId=@operation AND InventoryReceiptPostingId=@posting AND InventoryTransactionId=@transaction",
                ("@item", seed.ConsumableReceiptItemId),
                ("@operation", seed.ConsumableReceiptOperationId),
                ("@posting", consumableReceipt.SourceRecordId),
                ("@transaction", consumableReceipt.InventoryTransactionId)));
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
    public async Task FoundationReceipt_ReplayedWithSameRollCode_DoesNotCreateDuplicateFabricRoll()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var first = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = "REPLAY-ROLL",
                RollCode = "REPLAY-ROLL-001",
                UnitId = 1,
                OpposingLedgerAccountCode = "1000",
                SourceOperationId = seed.FabricReceiptOperationId
            }, CancellationToken.None);
            Assert.NotNull(first);
            Assert.False(first!.IsExisting);

            var replay = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = "REPLAY-ROLL",
                RollCode = "REPLAY-ROLL-001",
                UnitId = 1,
                OpposingLedgerAccountCode = "1000",
                SourceOperationId = seed.FabricReceiptOperationId
            }, CancellationToken.None);

            Assert.NotNull(replay);
            Assert.True(replay!.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode=@roll", ("@roll", "REPLAY-ROLL-001")));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationReceipt_PartialPostingWithoutStorage_IsCompletedSafely()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var partialOperationId = Guid.NewGuid();

        try
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                var inventoryItemIdValue = await InsertScalarAsync(connection, transaction, @"
                    SELECT InventoryItemID FROM dbo.InventoryItems WHERE ItemCode=@code", ("@code", seed.FabricItemCode));
                var inventoryItemId = inventoryItemIdValue;
                if (inventoryItemId == 0)
                {
                    inventoryItemId = await InsertScalarAsync(connection, transaction, @"
                        INSERT INTO dbo.InventoryItems (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt)
                        OUTPUT INSERTED.InventoryItemID
                        VALUES (@code, @name, N'Foundation', N'Yard', 6, 6, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());",
                        ("@code", seed.FabricItemCode), ("@name", "Partial fabric repair seed"));
                    await InsertScalarAsync(connection, transaction, @"
                        INSERT INTO dbo.InventoryItemFoundation (InventoryItemId, InventoryClassId, UnitId, CurrencyCode, OriginalQuantity, AvailableQuantity, ConsumedQuantity, OperationalValue, OfficialUnitCost, CreatedAt, UpdatedAt)
                        VALUES (@inventoryItemId, 1, 1, N'YER', 6, 6, 0, 153, 25.5, SYSUTCDATETIME(), SYSUTCDATETIME());",
                        ("@inventoryItemId", inventoryItemId));
                }

                var postingId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.InventoryReceiptPostings (GoodsReceiptItemId, InventoryClassId, AccountingBasisCode, OpposingLedgerAccountId, SourceOperationId, OperationalAmount, PostingAmount)
                    OUTPUT INSERTED.InventoryReceiptPostingId
                    VALUES (@goodsReceiptItemId, 1, 1, (SELECT TOP(1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode=N'1000' AND IsActive=1), @sourceOperationId, 153.000000, 153.00);",
                    ("@goodsReceiptItemId", seed.FabricReceiptItemId), ("@sourceOperationId", partialOperationId));

                var transactionId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.InventoryTransactions (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost, SourceOperationId, OperationalCostImpact)
                    OUTPUT INSERTED.TransactionID
                    VALUES (@inventoryItemId, N'FabricInventoryReceived', 6.000000, @reference, N'Partial repair seed', SYSUTCDATETIME(), 153.000000, 25.500000, @sourceOperationId, 153.000000);",
                    ("@inventoryItemId", inventoryItemId), ("@reference", $"PartialRepairSeed:{seed.FabricReceiptItemId}"), ("@sourceOperationId", partialOperationId));

                await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.InventoryReceiptLines (InventoryReceiptPostingId, InventoryItemId, ReceivedQuantity, OfficialUnitCost, UnitId, InventoryTransactionId)
                    VALUES (@postingId, @inventoryItemId, 6.000000, 25.500000, 1, @transactionId);",
                    ("@postingId", postingId), ("@inventoryItemId", inventoryItemId), ("@transactionId", transactionId));

                var eventId = await InsertScalarAsync(connection, transaction, @"
                    INSERT INTO dbo.AccountingEvents (AccountingEventType, PostingAmount, Status, InventoryReceiptPostingId, SourceType, SourceId, SourceOperationId, CreatedAt)
                    OUTPUT INSERTED.AccountingEventId
                    VALUES (7, 153.00, N'Posted', @postingId, N'Legacy', @sourceId, @sourceOperationId, SYSUTCDATETIME());",
                    ("@postingId", postingId), ("@sourceId", seed.FabricReceiptItemId), ("@sourceOperationId", partialOperationId));

                await InsertScalarAsync(connection, transaction, @"
                    UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=@eventId WHERE InventoryReceiptPostingId=@postingId;
                    UPDATE dbo.InventoryReceiptLines SET AccountingEventId=@eventId WHERE InventoryReceiptPostingId=@postingId;
                    UPDATE dbo.InventoryTransactions SET AccountingEventId=@eventId WHERE SourceOperationId=@sourceOperationId;",
                    ("@eventId", eventId), ("@postingId", postingId), ("@sourceOperationId", partialOperationId));

                await transaction.CommitAsync();
            }

            var result = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = "REPAIR-PARTIAL",
                RollCode = $"ROLL-PARTIAL-{Guid.NewGuid():N}",
                UnitId = 1,
                OpposingLedgerAccountCode = "1000",
                SourceOperationId = partialOperationId
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item AND StorageOperationId=@operation AND InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@item)",
                ("@item", seed.FabricReceiptItemId), ("@operation", partialOperationId)));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT SUM(StoredQuantity) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@id", seed.FabricReceiptItemId));
        }
        finally
        {
            await using var cleanupConnection = new SqlConnection(connectionString);
            await cleanupConnection.OpenAsync();
            await using var cleanupCommand = new SqlCommand(@"
                DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE StorageOperationId=@operation;
                UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL, InventoryTransactionId=NULL WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId=@operation);
                UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE SourceOperationId=@operation;
                UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE SourceOperationId=@operation;
                DELETE FROM dbo.AccountingEvents WHERE SourceOperationId=@operation;
                DELETE FROM dbo.InventoryTransactions WHERE SourceOperationId=@operation;
                DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId=@operation);
                DELETE FROM dbo.InventoryReceiptPostings WHERE SourceOperationId=@operation;",
                cleanupConnection);
            cleanupCommand.Parameters.AddWithValue("@operation", partialOperationId);
            await cleanupCommand.ExecuteNonQueryAsync();
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FabricCodeConsumption_UsesOfficialStockAndPostsOnce(short unitId)
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            var receipt = await CreateRepository(connectionString).ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "CODE-TEST",
                    RollCode = seed.FabricRollCode,
                    UnitId = unitId,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                }, CancellationToken.None);
            Assert.NotNull(receipt);
            var request = new ConsumeFabricCodeInventoryDto
            {
                FabricCode = seed.FabricItemCode,
                OrderItemId = seed.OrderItemId,
                PieceId = seed.PieceId,
                QuantityInches = 1.25m * (unitId == 1 ? 36m : 1m),
                SourceOperationId = seed.FabricConsumptionOperationId
            };

            InventoryFoundationPostingResultDto consumption;
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                consumption = await InventoryRepository.ConsumeFabricCodeInventoryAsync(connection, transaction, request, CancellationToken.None);
                var replayed = await InventoryRepository.ConsumeFabricCodeInventoryAsync(connection, transaction, request, CancellationToken.None);
                Assert.False(consumption.IsExisting);
                Assert.True(replayed.IsExisting);
                Assert.Equal(consumption.SourceRecordId, replayed.SourceRecordId);
                Assert.Equal(consumption.AccountingEventId, replayed.AccountingEventId);
                await transaction.CommitAsync();
            }

            Assert.Equal(1.25m, consumption.Quantity);
            Assert.Equal(31.875m, consumption.OperationalAmount);
            Assert.Equal(31.88m, consumption.PostingAmount);
            Assert.Equal(4.75m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(1.25m, await ReadDecimalAsync(connectionString, "SELECT ConsumedQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(121.125m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(4.75m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", receipt.InventoryItemId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@operation AND FabricRollId IS NULL", seed.FabricConsumptionOperationId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceOperationId=@operation AND OperationalCostImpact=-31.875", seed.FabricConsumptionOperationId));
            Assert.Equal(8, await ReadEventTypeAsync(connectionString, consumption.AccountingEventId));
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, consumption.AccountingEventId));
            Assert.Equal(2, await CountAsync(connectionString, @"
                SELECT COUNT(*) FROM dbo.JournalEntryLines line
                JOIN dbo.JournalEntries entry ON entry.JournalEntryId=line.JournalEntryId
                JOIN dbo.LedgerAccounts account ON account.LedgerAccountId=line.LedgerAccountId
                WHERE entry.AccountingEventId=@id
                  AND ((account.AccountCode=N'1130' AND line.DebitAmount=31.88 AND line.CreditAmount=0)
                    OR (account.AccountCode=N'1101' AND line.CreditAmount=31.88 AND line.DebitAmount=0))", consumption.AccountingEventId));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FabricCodeConsumption_CallerRollbackRestoresStockAndRemovesPosting()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            var receipt = await CreateRepository(connectionString).ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "CODE-ROLLBACK",
                    RollCode = seed.FabricRollCode,
                    UnitId = 1,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = seed.FabricReceiptOperationId
                }, CancellationToken.None);
            Assert.NotNull(receipt);
            InventoryFoundationPostingResultDto consumption;
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                consumption = await InventoryRepository.ConsumeFabricCodeInventoryAsync(connection, transaction,
                    new ConsumeFabricCodeInventoryDto
                    {
                        FabricCode = seed.FabricItemCode,
                        OrderItemId = seed.OrderItemId,
                        PieceId = seed.PieceId,
                        QuantityInches = 36m,
                        SourceOperationId = seed.FabricConsumptionOperationId
                    }, CancellationToken.None);
                await transaction.RollbackAsync();
            }

            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(153m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", receipt.InventoryItemId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@operation", seed.FabricConsumptionOperationId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceOperationId=@operation", seed.FabricConsumptionOperationId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventId=@id", consumption.AccountingEventId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntries WHERE AccountingEventId=@id", consumption.AccountingEventId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE AccountingEventId=@id", consumption.AccountingEventId));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Theory]
    [InlineData(36, 2)]
    [InlineData(108, 2)]
    [InlineData(24, 9)]
    public async Task TailoringFabric_OrderSaveConsumesOfficialQuantityAndRetryDoesNotRepeat(decimal inchesPerPiece, int quantity)
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var reference = $"FAB-ORDER-{Guid.NewGuid():N}";
        try
        {
            var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
            var request = await CreateFabricOrderRequestAsync(connectionString, seed, reference, inchesPerPiece, quantity);
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            var created = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(created);
            var replayed = await repository.CreateAsync(request, CancellationToken.None);
            Assert.NotNull(replayed);
            Assert.Equal(created.OrderId, replayed.OrderId);
            var storedItem = Assert.Single(await repository.GetItemsAsync(created.OrderId, CancellationToken.None));
            using var snapshot = System.Text.Json.JsonDocument.Parse(storedItem.MeasurementSnapshot!);
            Assert.Equal(inchesPerPiece, decimal.Parse(snapshot.RootElement.GetProperty("_consumption").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("Inch", snapshot.RootElement.GetProperty("_consumptionUnit").GetString());
            var expectedQuantity = inchesPerPiece * quantity / 36m;
            var expectedCost = expectedQuantity * 25.5m;
            Assert.Equal(6m - expectedQuantity, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(expectedQuantity, await ReadDecimalAsync(connectionString, "SELECT ConsumedQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(153m - expectedCost, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(6m - expectedQuantity, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", receipt.InventoryItemId));
            Assert.Equal(quantity, await CountAsync(connectionString, @"
                SELECT COUNT(*) FROM dbo.FabricConsumptionSources source
                JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId
                JOIN dbo.Pieces piece ON piece.OrderItemID=item.OrderItemID
                JOIN dbo.AccountingEvents event ON event.AccountingEventId=source.AccountingEventId
                JOIN dbo.InventoryTransactions movement ON movement.TransactionID=source.InventoryTransactionId
                WHERE item.OrderID=@id AND source.FabricRollId IS NULL
                  AND event.AccountingEventType=8 AND event.Status=N'Posted'
                  AND movement.AccountingEventId=event.AccountingEventId", created.OrderId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources source JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId WHERE item.OrderID=@id AND source.PieceId IS NULL", created.OrderId));
            Assert.Equal(inchesPerPiece * quantity, await ReadDecimalAsync(connectionString, "SELECT SUM(fabric.ConsumedQuantity) FROM dbo.OrderItemFabrics fabric JOIN dbo.OrderItems item ON item.OrderItemID=fabric.OrderItemID WHERE item.OrderID=@id", created.OrderId));
            Assert.Equal(expectedCost, await ReadDecimalAsync(connectionString, "SELECT SUM(fabric.TotalCost) FROM dbo.OrderItemFabrics fabric JOIN dbo.OrderItems item ON item.OrderItemID=fabric.OrderItemID WHERE item.OrderID=@id", created.OrderId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.TailoringCostPostings WHERE OrderId=@id", created.OrderId));
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                FabricCode = seed.FabricItemCode,
                receipt.InventoryItemId,
                created.OrderId,
                RequiredInches = inchesPerPiece * quantity,
                BeforeQuantity = 6m,
                AfterQuantity = 6m - expectedQuantity,
                BeforeValue = 153m,
                AfterValue = 153m - expectedCost,
                RetryOrderId = replayed.OrderId
            }));
            await WriteOrderEvidenceAsync(connectionString, created.OrderId);
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task TailoringFabric_InsufficientStockRejectsBeforeCreatingOrder()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var reference = $"FAB-ORDER-SHORT-{Guid.NewGuid():N}";
        try
        {
            var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
            var request = await CreateFabricOrderRequestAsync(connectionString, seed, reference, 109m, 2);
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(request, CancellationToken.None));
            Assert.Contains(seed.FabricItemCode, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("216", exception.Message);
            Assert.Contains("218", exception.Message);
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE InventoryItemID=@id AND TransactionType=N'FabricInventoryConsumed'", receipt.InventoryItemId));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(153m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, seed);
        }
    }

    [Theory]
    [InlineData(108, true)]
    [InlineData(109, false)]
    public async Task TailoringFabric_RepeatedCodeIsCheckedAcrossAllItems(decimal inchesPerPiece, bool shouldSucceed)
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var reference = $"FAB-ORDER-GROUP-{Guid.NewGuid():N}";
        try
        {
            var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
            var template = await CreateFabricOrderRequestAsync(connectionString, seed, reference, inchesPerPiece, 1);
            var request = new CreateOrderDto { CustomerId = seed.CustomerId, RequestReference = reference, Items = [template.Items[0], template.Items[0]] };
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            if (shouldSucceed)
            {
                var order = await repository.CreateAsync(request, CancellationToken.None);
                Assert.NotNull(order);
                Assert.Equal(2, (await repository.GetItemsAsync(order.OrderId, CancellationToken.None)).Count);
                Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            }
            else
            {
                await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(request, CancellationToken.None));
                Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
                Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            }
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, seed);
        }
    }

    [Theory]
    [InlineData(36, true)]
    [InlineData(217, false)]
    public async Task TailoringFabric_MultipleCodesRemainIndependentAndSaveAtomically(decimal secondInches, bool shouldSucceed)
    {
        var connectionString = GetConnectionString();
        var firstSeed = await SeedAsync(connectionString);
        var secondSeed = await SeedAsync(connectionString);
        var reference = $"FAB-ORDER-CODES-{Guid.NewGuid():N}";
        try
        {
            var firstReceipt = await ReceiveOrderFabricAsync(connectionString, firstSeed);
            var secondReceipt = await ReceiveOrderFabricAsync(connectionString, secondSeed);
            var firstTemplate = await CreateFabricOrderRequestAsync(connectionString, firstSeed, reference, 36m, 1);
            var secondTemplate = await CreateFabricOrderRequestAsync(connectionString, secondSeed, reference, secondInches, 1);
            var request = new CreateOrderDto { CustomerId = firstSeed.CustomerId, RequestReference = reference, Items = [firstTemplate.Items[0], secondTemplate.Items[0]] };
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            if (shouldSucceed)
            {
                var order = await repository.CreateAsync(request, CancellationToken.None);
                Assert.NotNull(order);
                Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(DISTINCT source.InventoryItemId) FROM dbo.FabricConsumptionSources source JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId WHERE item.OrderID=@id", order.OrderId));
            }
            else
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(request, CancellationToken.None));
                Assert.Contains(secondSeed.FabricItemCode, exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
            }

            foreach (var receipt in new[] { firstReceipt, secondReceipt })
            {
                Assert.Equal(shouldSucceed ? 5m : 6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
                Assert.Equal(shouldSucceed ? 127.5m : 153m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            }
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, firstSeed);
            await CleanupAsync(connectionString, secondSeed);
        }
    }

    [Fact]
    public async Task TailoringFabric_ConcurrentOrdersCannotExceedSameCodeAvailability()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var firstReference = $"FAB-ORDER-RACE-A-{Guid.NewGuid():N}";
        var secondReference = $"FAB-ORDER-RACE-B-{Guid.NewGuid():N}";
        try
        {
            var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
            var firstRequest = await CreateFabricOrderRequestAsync(connectionString, seed, firstReference, 144m, 1);
            var secondRequest = await CreateFabricOrderRequestAsync(connectionString, seed, secondReference, 144m, 1);
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<Exception?> SaveAsync(CreateOrderDto request)
            {
                await start.Task;
                return await Record.ExceptionAsync(() => repository.CreateAsync(request, CancellationToken.None));
            }

            var saves = new[] { SaveAsync(firstRequest), SaveAsync(secondRequest) };
            start.SetResult(true);
            var outcomes = await Task.WhenAll(saves);
            Assert.Single(outcomes, outcome => outcome is null);
            Assert.IsType<ArgumentException>(Assert.Single(outcomes, outcome => outcome is not null));
            Assert.Equal(2m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(51m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", firstReference)
                + await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", secondReference));
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, firstReference);
            await CleanupFabricOrderAsync(connectionString, secondReference);
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task TailoringFabric_FailureAfterFirstPostingRollsBackEntireOrder()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var reference = $"FAB-ORDER-ROLLBACK-{Guid.NewGuid():N}";
        try
        {
            var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
            var first = await CreateFabricOrderRequestAsync(connectionString, seed, reference, 36m, 1);
            var second = await CreateFabricOrderRequestAsync(connectionString, seed, reference, 0.001m, 1);
            var request = new CreateOrderDto { CustomerId = seed.CustomerId, RequestReference = reference, Items = [first.Items[0], second.Items[0]] };
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(request, CancellationToken.None));
            Assert.Contains("الدقة المحاسبية", exception.Message);
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE InventoryItemID=@id AND TransactionType=N'FabricInventoryConsumed'", receipt.InventoryItemId));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", receipt.InventoryItemId));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
            Assert.Equal(153m, await ReadDecimalAsync(connectionString, "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, seed);
        }
    }

    private async Task WriteOrderEvidenceAsync(string connectionString, int orderId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT item.FabricCode,source.InventoryItemId,source.OrderItemId,source.PieceId,
                   source.FabricConsumptionSourceId,source.SourceOperationId,source.ConsumedQuantity,
                   source.UnitId,source.OfficialUnitCost,source.OperationalAmount,source.PostingAmount,
                   source.InventoryTransactionId,source.AccountingEventId,financial.FinancialTransactionId,
                   entry.JournalEntryId,account.AccountCode,line.DebitAmount,line.CreditAmount
            FROM dbo.FabricConsumptionSources source
            JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId
            JOIN dbo.FinancialTransactions financial ON financial.AccountingEventId=source.AccountingEventId
            JOIN dbo.JournalEntries entry ON entry.AccountingEventId=source.AccountingEventId
            JOIN dbo.JournalEntryLines line ON line.JournalEntryId=entry.JournalEntryId
            JOIN dbo.LedgerAccounts account ON account.LedgerAccountId=line.LedgerAccountId
            WHERE item.OrderID=@orderId ORDER BY source.FabricConsumptionSourceId,account.AccountCode", connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var evidence = new Dictionary<string, object?>();
            for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                evidence[reader.GetName(columnIndex)] = reader.IsDBNull(columnIndex) ? null : reader.GetValue(columnIndex);
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(evidence));
        }
    }

    [Fact]
    public async Task TailoringFabric_ShortageReturnsJsonMessageForFlutter()
    {
        var reference = $"FAB-ORDER-API-{Guid.NewGuid():N}";
        await WithFabricOrderAsync(reference, async (repository, template) =>
        {
            var item = Assert.Single(template.Items);
            var controller = new OrdersController(new OrderService(repository, null!, null!));
            var response = await controller.CreateOrder(new CreateOrderDto
            {
                CustomerId = template.CustomerId,
                RequestReference = reference,
                Items = [new CreateOrderItemDto
                {
                    PieceType = item.PieceType, ProductTypeId = item.ProductTypeId,
                    FabricCode = item.FabricCode, Quantity = 7,
                    MeasurementSnapshot = item.MeasurementSnapshot
                }]
            }, CancellationToken.None);
            var rejection = Assert.IsType<BadRequestObjectResult>(response.Result);
            Assert.Equal(400, rejection.StatusCode);
            var payload = System.Text.Json.JsonSerializer.SerializeToElement(rejection.Value);
            var message = payload.GetProperty("message").GetString();
            Assert.NotNull(message);
            Assert.Contains(item.FabricCode!, message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("المتاح: 216", message);
            Assert.Contains("المطلوب: 252", message);
            Assert.Equal(0, await CountAsync(GetConnectionString(), "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
        });
    }

    [Fact]
    public async Task TailoringFabric_OmittingNestedFabricDoesNotBypassConsumption()
    {
        var reference = $"FAB-ORDER-NESTED-{Guid.NewGuid():N}";
        await WithFabricOrderAsync(reference, async (repository, template) =>
        {
            var item = Assert.Single(template.Items);
            var created = await repository.CreateAsync(new CreateOrderDto
            {
                CustomerId = template.CustomerId,
                RequestReference = reference,
                Items = [new CreateOrderItemDto
                {
                    PieceType = item.PieceType, ProductTypeId = item.ProductTypeId,
                    FabricCode = item.FabricCode, Quantity = item.Quantity,
                    MeasurementSnapshot = item.MeasurementSnapshot
                }]
            }, CancellationToken.None);
            Assert.NotNull(created);
            Assert.Equal(1, await CountAsync(GetConnectionString(), "SELECT COUNT(*) FROM dbo.FabricConsumptionSources source JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId WHERE item.OrderID=@id", created.OrderId));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TailoringFabric_MissingOrConflictingCodeRejectsEntireOrder(bool missingCode)
    {
        var reference = $"FAB-ORDER-IDENTITY-{Guid.NewGuid():N}";
        await WithFabricOrderAsync(reference, async (repository, template) =>
        {
            var item = Assert.Single(template.Items);
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(new CreateOrderDto
            {
                CustomerId = template.CustomerId,
                RequestReference = reference,
                Items = [new CreateOrderItemDto
                {
                    PieceType = item.PieceType, ProductTypeId = item.ProductTypeId,
                    FabricCode = missingCode ? null : item.FabricCode, Quantity = item.Quantity,
                    MeasurementSnapshot = item.MeasurementSnapshot,
                    Fabric = new CreateOrderFabricDto { FabricCode = missingCode ? item.FabricCode : "DIFFERENT-CODE" }
                }]
            }, CancellationToken.None));
            Assert.Equal(0, await CountAsync(GetConnectionString(), "SELECT COUNT(*) FROM dbo.Orders WHERE SaleReference=@name", reference));
        });
    }

    internal static async Task WithFabricOrderAsync(string reference, Func<OrderRepository, CreateOrderDto, Task> action)
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            await ReceiveOrderFabricAsync(connectionString, seed);
            var request = await CreateFabricOrderRequestAsync(connectionString, seed, reference, 36m, 1);
            var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
            var repository = new OrderRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options), new MeasurementConsumptionRules());
            await action(repository, request);
        }
        finally
        {
            await CleanupFabricOrderAsync(connectionString, reference);
            await CleanupAsync(connectionString, seed);
        }
    }

    private static async Task<InventoryFoundationPostingResultDto> ReceiveOrderFabricAsync(string connectionString, TestSeed seed) =>
        await CreateRepository(connectionString).ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
        {
            GoodsReceiptItemId = seed.FabricReceiptItemId,
            ItemCode = seed.FabricItemCode,
            FabricTypeCode = "ORDER-CODE-TEST",
            RollCode = seed.FabricRollCode,
            UnitId = 1,
            OpposingLedgerAccountCode = "1000",
            SourceOperationId = seed.FabricReceiptOperationId
        }, CancellationToken.None) ?? throw new InvalidOperationException("The test fabric receipt was not created.");

    private static async Task<CreateOrderDto> CreateFabricOrderRequestAsync(string connectionString, TestSeed seed, string reference, decimal inchesPerPiece, int quantity)
    {
        var productTypeId = await ReadIntAsync(connectionString, "SELECT TOP(1) ProductTypeId FROM dbo.PricingProductTypes WHERE IsActive=1 ORDER BY ProductTypeId", 0);
        return new CreateOrderDto
        {
            CustomerId = seed.CustomerId,
            RequestReference = reference,
            TotalAmount = 1000m,
            Items = [new CreateOrderItemDto
            {
                PieceType = "Fabric order test",
                ProductTypeId = productTypeId,
                FabricCode = seed.FabricItemCode,
                Quantity = quantity,
                Consumption = 9999m,
                ConsumptionUnit = "Inch",
                MeasurementSnapshot = System.Text.Json.JsonSerializer.Serialize(new { Length = inchesPerPiece }),
                Fabric = new CreateOrderFabricDto { FabricCode = seed.FabricItemCode, Quantity = 9999m, Unit = "Inch", UnitCost = 9999m }
            }]
        };
    }

    private static async Task CleanupFabricOrderAsync(string connectionString, string reference)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var command = new SqlCommand(@"
            DECLARE @orders TABLE(OrderId int PRIMARY KEY);
            INSERT @orders SELECT OrderID FROM dbo.Orders WHERE SaleReference=@reference;
            DECLARE @sources TABLE(SourceId bigint PRIMARY KEY,EventId bigint,TransactionId int);
            INSERT @sources SELECT source.FabricConsumptionSourceId,source.AccountingEventId,source.InventoryTransactionId
                FROM dbo.FabricConsumptionSources source JOIN dbo.OrderItems item ON item.OrderItemID=source.OrderItemId
                WHERE item.OrderID IN(SELECT OrderId FROM @orders);
            DECLARE @events TABLE(EventId bigint PRIMARY KEY);
            INSERT @events SELECT EventId FROM @sources WHERE EventId IS NOT NULL
                UNION SELECT AccountingEventId FROM dbo.AccountingEvents
                WHERE OrderId IN(SELECT OrderId FROM @orders)
                   OR PaymentId IN(SELECT PaymentID FROM dbo.Payments WHERE OrderID IN(SELECT OrderId FROM @orders));
            UPDATE dbo.FabricConsumptionSources SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE FabricConsumptionSourceId IN(SELECT SourceId FROM @sources);
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE TransactionID IN(SELECT TransactionId FROM @sources);
            DELETE FROM dbo.CashMovements WHERE AccountingEventId IN(SELECT EventId FROM @events);
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN(SELECT EventId FROM @events));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN(SELECT EventId FROM @events);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN(SELECT EventId FROM @events);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN(SELECT EventId FROM @events);
            DELETE ledger FROM dbo.CustomerLedgerEntries ledger JOIN dbo.Orders orders ON ledger.ReferenceNumber=orders.OrderNumber+N':Advance' WHERE orders.OrderID IN(SELECT OrderId FROM @orders);
            DELETE FROM dbo.Payments WHERE OrderID IN(SELECT OrderId FROM @orders);
            DELETE FROM dbo.InventoryTransactions WHERE TransactionID IN(SELECT TransactionId FROM @sources);
            DELETE FROM dbo.FabricConsumptionSources WHERE FabricConsumptionSourceId IN(SELECT SourceId FROM @sources);
            DELETE FROM dbo.OrderItemFabrics WHERE OrderItemID IN(SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders));
            DELETE FROM dbo.Pieces WHERE OrderItemID IN(SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders));
            DELETE FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders);
            DELETE FROM dbo.Orders WHERE OrderID IN(SELECT OrderId FROM @orders);", connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private sealed class MeasurementConsumptionRules : IConsumptionRulesRepository
    {
        public Task<EvaluateConsumptionResponseDto?> EvaluateAsync(EvaluateConsumptionRequestDto request, CancellationToken cancellationToken) =>
            Task.FromResult<EvaluateConsumptionResponseDto?>(new(request.Measurements["Length"], "Inch", 1, null, ["Length"]));
        public Task<ConsumptionRulesDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConsumptionRulesIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConsumptionRuleDto>> SaveProductRulesBatchAsync(SaveProductRulesBatchDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConsumptionRuleDto?> CreateAsync(CreateConsumptionRuleDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConsumptionRuleDto?> UpdateAsync(int consumptionRuleId, UpdateConsumptionRuleDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MeasurementTypeWriteResultDto?> CreateMeasurementTypeAsync(CreateMeasurementTypeDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MeasurementTypeWriteResultDto?> UpdateMeasurementTypeAsync(int productTypeId, UpdateMeasurementTypeDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteMeasurementTypeAsync(int productTypeId, CancellationToken cancellationToken) => throw new NotSupportedException();
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
            && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase)
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
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
                OR TransactionID IN (SELECT InventoryTransactionId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId)));
            UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation)
                OR GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);
            UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL,InventoryTransactionId=NULL,FabricRollId=NULL WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation))
                OR InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId));
            UPDATE dbo.FabricConsumptionSources SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE SourceOperationId=@fabricConsumptionOperation;
            UPDATE dbo.ProductionMaterialConsumptions SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE SourceOperationId=@consumableConsumptionOperation;
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.InventoryTransactions WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
                OR TransactionID IN (SELECT InventoryTransactionId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId)));
            DELETE FROM dbo.FabricConsumptionSources WHERE SourceOperationId=@fabricConsumptionOperation;
            DELETE FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId=@consumableConsumptionOperation;
            DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);
            DELETE FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation)) OR InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId)));
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation)) OR InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId));
            DELETE FROM dbo.InventoryReceiptPostings WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation) OR GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);
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
            DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId IN (SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@receiptId);
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

    private static async Task<int> CountAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
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
