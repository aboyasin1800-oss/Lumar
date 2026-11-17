using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace LUMAR_ERP_API_V2.Tests;

public sealed partial class FabricConsumablesFoundationIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OfficialFabricBatch_CreatesPurchaseReceiptFoundationAndBalancedEntry()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var invoiceNumber = $"FOUND-BATCH-{suffix}";
        var fabricCode = $"FAB-BATCH-{suffix}";
        string? generatedCode = null;

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

            generatedCode = await ReadStringAsync(connectionString,
                @"SELECT TOP (1) i.ItemCode
                  FROM dbo.InventoryItems i
                  INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemID
                  INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                  INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                  INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                  WHERE gr.ReceiptNumber = @receiptNumber
                  ORDER BY i.InventoryItemId DESC;",
                ("@receiptNumber", $"FAB-{invoiceNumber}"));

            Assert.NotNull(result);
            Assert.Equal(50m, result.TotalYards);
            Assert.Equal(60000m, result.TotalCost);
            Assert.NotNull(generatedCode);
            Assert.StartsWith("FA", generatedCode, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode=@name", new (string Name, object? Value)[] { ("@name", generatedCode) }));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItemFoundation f INNER JOIN dbo.InventoryItems i ON i.InventoryItemID=f.InventoryItemId WHERE i.ItemCode=@name AND f.InventoryClassId=1", new (string Name, object? Value)[] { ("@name", generatedCode) }));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode=@name AND AvailableQuantity=50", new (string Name, object? Value)[] { ("@name", generatedCode) }));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId=ae.InventoryReceiptPostingId INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId=rp.GoodsReceiptItemId INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId=gri.GoodsReceiptId WHERE gr.ReceiptNumber=@name AND ae.AccountingEventType=7 AND ae.PostingAmount=60000", new (string Name, object? Value)[] { ("@name", $"FAB-{invoiceNumber}") }));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.JournalEntryLines debitLine INNER JOIN dbo.JournalEntries je ON je.JournalEntryId=debitLine.JournalEntryId INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=je.AccountingEventId INNER JOIN dbo.JournalEntryLines creditLine ON creditLine.JournalEntryId=je.JournalEntryId INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId=debitLine.LedgerAccountId INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId=creditLine.LedgerAccountId WHERE ae.AccountingEventType=7 AND debitAccount.AccountCode=N'1101' AND creditAccount.AccountCode=N'2100' AND debitLine.DebitAmount=60000 AND creditLine.CreditAmount=60000 AND je.ReferenceNumber LIKE @name", new (string Name, object? Value)[] { ("@name", $"GoodsReceipt:FAB-{invoiceNumber}:%") }));

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
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE ReceiptNumber=@name", ("@name", $"FAB-{invoiceNumber}")));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode=@name", ("@name", generatedCode)));
        }
        finally
        {
            await CleanupOfficialFabricBatchAsync(connectionString, invoiceNumber);
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task OfficialSupplierInvoiceContract_UsesPurchaseClearingAndSupplierLiability()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var invoiceNumber = $"ES7-CONTRACT-{suffix}";
        var operation = Guid.NewGuid();

        SupplierFinancialInvoiceResult? invoice = null;

        try
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var runtime = new SupplierFinancialRuntime();
                invoice = await runtime.CreateInvoiceAsync(connection, transaction,
                    new SupplierFinancialInvoiceRequest(support.SupplierId, invoiceNumber, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30), 60000m, null, operation, "ES7-contract-test"),
                    CancellationToken.None);

                Assert.Equal(60000m, invoice.OutstandingAmount + invoice.AmountPaid);

                await transaction.CommitAsync();
            }

            Assert.NotNull(invoice);
            Assert.Equal(60000m, await ReadDecimalAsync(connectionString,
                "SELECT TotalAmount FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@id",
                ("@id", invoice.SupplierInvoiceId)));

            Assert.Equal(1, await CountAsync(connectionString,
                @"SELECT COUNT(*)
                  FROM dbo.AccountingEvents ae
                  INNER JOIN dbo.JournalEntries je ON je.AccountingEventId = ae.AccountingEventId
                  INNER JOIN dbo.JournalEntryLines debit ON debit.JournalEntryId = je.JournalEntryId
                  INNER JOIN dbo.LedgerAccounts debitAccount ON debitAccount.LedgerAccountId = debit.LedgerAccountId
                  INNER JOIN dbo.JournalEntryLines credit ON credit.JournalEntryId = je.JournalEntryId
                  INNER JOIN dbo.LedgerAccounts creditAccount ON creditAccount.LedgerAccountId = credit.LedgerAccountId
                  WHERE ae.SourceType = N'SupplierInvoice'
                    AND ae.SourceId = @invoiceId
                    AND ae.AccountingEventType = 28
                    AND ae.PostingAmount = 60000
                    AND debitAccount.AccountCode = N'1120'
                    AND creditAccount.AccountCode = N'2100'
                    AND debit.DebitAmount = 60000
                    AND credit.CreditAmount = 60000;",
                new (string Name, object? Value)[] { ("@invoiceId", invoice.SupplierInvoiceId) }));
        }
        finally
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                DELETE FROM dbo.SupplierFinancialInvoices WHERE SupplierInvoiceId IN (
                    SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE InvoiceNumber = @invoiceNumber);
                DELETE FROM dbo.SupplierInvoices WHERE InvoiceNumber = @invoiceNumber;
                DELETE FROM dbo.AccountingEvents WHERE SourceType = N'SupplierInvoice' AND SourceId IN (
                    SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE InvoiceNumber = @invoiceNumber);
                DELETE FROM dbo.SupplierNotifications WHERE ReferenceType = N'SupplierInvoice' AND ReferenceId IN (
                    SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE InvoiceNumber = @invoiceNumber);
                DELETE FROM dbo.SupplierLedgerEntries WHERE SourceType = N'SupplierInvoice' AND SourceId IN (
                    SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE InvoiceNumber = @invoiceNumber);",
                connection);
            command.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
            await command.ExecuteNonQueryAsync();
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task OfficialSupplierInvoiceReceiptInventoryMatch_ClosesPurchaseClearingAndKeepsSupplierLiabilitySingle()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var invoiceNumber = $"INV-RECEIPT-MATCH-{suffix}";
        var operation = Guid.NewGuid();
        var inventoryItemCode = $"FA-LINKED-{suffix}";

        SupplierFinancialInvoiceResult? invoice = null;

        try
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var runtime = new SupplierFinancialRuntime();
                invoice = await runtime.CreateInvoiceAsync(connection, transaction,
                    new SupplierFinancialInvoiceRequest(support.SupplierId, invoiceNumber, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30), 100000m, null, operation, "MATCH-TEST"),
                    CancellationToken.None);

                Assert.Equal(100000m, invoice.OutstandingAmount + invoice.AmountPaid);
                await transaction.CommitAsync();
            }

            var supplierInvoiceId = invoice.SupplierInvoiceId;
            int supplierInvoiceLineId;
            int goodsReceiptId;
            int goodsReceiptItemId;

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                supplierInvoiceLineId = Convert.ToInt32(await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.SupplierInvoiceLines (SupplierInvoiceId, InventoryItemId, Quantity, UnitCost, RollCount, SourceOperationId, Status, CreatedBy)
                      OUTPUT INSERTED.SupplierInvoiceLineId
                      VALUES (@supplierInvoiceId, NULL, 10.0, 10000.0, NULL, @sourceOperationId, N'Posted', N'ES7-Receipt-Link');",
                    ("@supplierInvoiceId", supplierInvoiceId), ("@sourceOperationId", Guid.NewGuid())));

                goodsReceiptId = Convert.ToInt32(await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.GoodsReceipts (SupplierId, ReceiptNumber, ReceiptDate, CreatedAt, ReceiptStatus)
                      OUTPUT INSERTED.GoodsReceiptId
                      VALUES (@supplierId, @receiptNumber, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Confirmed');",
                    ("@supplierId", support.SupplierId), ("@receiptNumber", $"GR-{invoiceNumber}")));

                goodsReceiptItemId = Convert.ToInt32(await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId, ItemName, ReceivedQuantity, UnitCost, LineTotal, ItemType, SupplierInvoiceLineId, LineStatus)
                      OUTPUT INSERTED.GoodsReceiptItemId
                      VALUES (@goodsReceiptId, N'Linked receipt item', 10.0, 10000.0, 100000.0, N'Fabric', @supplierInvoiceLineId, N'Confirmed');",
                    ("@goodsReceiptId", goodsReceiptId), ("@supplierInvoiceLineId", supplierInvoiceLineId)));

                await transaction.CommitAsync();
            }

            var repository = CreateRepository(connectionString);
            var result = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = goodsReceiptItemId,
                ItemCode = inventoryItemCode,
                FabricTypeCode = "FabricLinkedMatch",
                ColorValue = "Blue",
                UnitId = 1,
                OpposingLedgerAccountCode = "1120",
                SourceOperationId = Guid.NewGuid(),
                Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = $"FA-{suffix}", ColorValue = "Blue", Quantity = 10m }]
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(10m, result.Quantity);

            var inventoryBalance = await ReadDecimalAsync(connectionString,
                @"SELECT SUM(CASE WHEN i.CurrentQuantity IS NOT NULL THEN i.CurrentQuantity ELSE 0 END)
                  FROM dbo.InventoryItems i
                  INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemID
                  INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                  WHERE rp.GoodsReceiptItemId = @goodsReceiptItemId;",
                ("@goodsReceiptItemId", goodsReceiptItemId));
            Assert.Equal(10m, inventoryBalance);

            var goodsReceiptPostingCounts = await ReadIntAsync(connectionString,
                @"SELECT COUNT(*) FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@goodsReceiptItemId AND SourceOperationId IS NOT NULL;",
                ("@goodsReceiptItemId", goodsReceiptItemId));
            Assert.Equal(1, goodsReceiptPostingCounts);

            var supplierLiabilitySingle = await ReadIntAsync(connectionString,
                @"SELECT COUNT(*)
                  FROM dbo.JournalEntryLines jel
                  INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
                  INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
                  INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
                  WHERE ae.SourceType = N'SupplierInvoice'
                    AND ae.SourceId = @supplierInvoiceId
                    AND la.AccountCode = N'2100'
                    AND jel.CreditAmount > 0;",
                ("@supplierInvoiceId", supplierInvoiceId));
            Assert.Equal(1, supplierLiabilitySingle);

            var purchaseClearingZero = await ReadDecimalAsync(connectionString,
                @"SELECT COALESCE(SUM(CASE WHEN la.AccountCode = N'1120' THEN jel.DebitAmount - jel.CreditAmount ELSE 0 END), 0)
                  FROM dbo.JournalEntryLines jel
                  INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
                  INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
                  WHERE je.ReferenceNumber LIKE N'GoodsReceipt:%' OR je.ReferenceNumber LIKE N'%' + CAST(@supplierInvoiceId AS nvarchar(20)) + N'%';",
                ("@supplierInvoiceId", supplierInvoiceId));
            Assert.Equal(0m, purchaseClearingZero);

            var linkedCount = await CountAsync(connectionString,
                @"SELECT COUNT(*)
                  FROM dbo.GoodsReceiptItems gri
                  INNER JOIN dbo.SupplierInvoiceLines sil ON sil.SupplierInvoiceLineId = gri.SupplierInvoiceLineId
                  WHERE gri.SupplierInvoiceLineId = @supplierInvoiceLineId AND gri.GoodsReceiptItemId = @goodsReceiptItemId AND sil.SupplierInvoiceId = @supplierInvoiceId;",
                ("@supplierInvoiceLineId", supplierInvoiceLineId), ("@goodsReceiptItemId", goodsReceiptItemId), ("@supplierInvoiceId", supplierInvoiceId));
            Assert.Equal(1, linkedCount);
        }
        finally
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (
                    SELECT je.JournalEntryId
                    FROM dbo.JournalEntries je
                    INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
                    WHERE ae.SourceType = N'SupplierInvoice' AND ae.SourceId = @supplierInvoiceId
                       OR ae.InventoryReceiptPostingId IN (
                            SELECT rp.InventoryReceiptPostingId
                            FROM dbo.InventoryReceiptPostings rp
                            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                            WHERE gr.ReceiptNumber = @receiptNumber));

                DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (
                    SELECT ae.AccountingEventId
                    FROM dbo.AccountingEvents ae
                    WHERE ae.SourceType = N'SupplierInvoice' AND ae.SourceId = @supplierInvoiceId
                       OR ae.InventoryReceiptPostingId IN (
                            SELECT rp.InventoryReceiptPostingId
                            FROM dbo.InventoryReceiptPostings rp
                            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                            WHERE gr.ReceiptNumber = @receiptNumber));

                DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (
                    SELECT ae.AccountingEventId
                    FROM dbo.AccountingEvents ae
                    WHERE ae.SourceType = N'SupplierInvoice' AND ae.SourceId = @supplierInvoiceId
                       OR ae.InventoryReceiptPostingId IN (
                            SELECT rp.InventoryReceiptPostingId
                            FROM dbo.InventoryReceiptPostings rp
                            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                            WHERE gr.ReceiptNumber = @receiptNumber));

                DELETE FROM dbo.SupplierFinancialInvoices WHERE AccountingEventId IN (
                    SELECT ae.AccountingEventId
                    FROM dbo.AccountingEvents ae
                    WHERE ae.SourceType = N'SupplierInvoice' AND ae.SourceId = @supplierInvoiceId
                       OR ae.InventoryReceiptPostingId IN (
                            SELECT rp.InventoryReceiptPostingId
                            FROM dbo.InventoryReceiptPostings rp
                            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                            WHERE gr.ReceiptNumber = @receiptNumber));

                DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (
                    SELECT rp.InventoryReceiptPostingId
                    FROM dbo.InventoryReceiptPostings rp
                    INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                    INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                    WHERE gr.ReceiptNumber = @receiptNumber);

                DELETE FROM dbo.InventoryTransactions WHERE AccountingEventId IN (
                    SELECT ae.AccountingEventId
                    FROM dbo.AccountingEvents ae
                    WHERE ae.SourceType = N'SupplierInvoice' AND ae.SourceId = @supplierInvoiceId
                       OR ae.InventoryReceiptPostingId IN (
                            SELECT rp.InventoryReceiptPostingId
                            FROM dbo.InventoryReceiptPostings rp
                            INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                            INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                            WHERE gr.ReceiptNumber = @receiptNumber));

                UPDATE dbo.InventoryReceiptPostings
                SET AccountingEventId = NULL
                WHERE GoodsReceiptItemId IN (
                    SELECT gri.GoodsReceiptItemId
                    FROM dbo.GoodsReceiptItems gri
                    INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                    WHERE gr.ReceiptNumber = @receiptNumber);

                DELETE FROM dbo.AccountingEvents WHERE SourceType = N'SupplierInvoice' AND SourceId = @supplierInvoiceId;
                DELETE FROM dbo.AccountingEvents WHERE InventoryReceiptPostingId IN (
                    SELECT rp.InventoryReceiptPostingId
                    FROM dbo.InventoryReceiptPostings rp
                    INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                    INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                    WHERE gr.ReceiptNumber = @receiptNumber);

                DELETE FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (
                    SELECT gri.GoodsReceiptItemId
                    FROM dbo.GoodsReceiptItems gri
                    INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                    WHERE gr.ReceiptNumber = @receiptNumber);

                DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId IN (
                    SELECT gri.GoodsReceiptItemId
                    FROM dbo.GoodsReceiptItems gri
                    INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                    WHERE gr.ReceiptNumber = @receiptNumber);

                DELETE FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @receiptPattern;
                DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                    SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE ReceiptNumber = @receiptNumber);
                DELETE FROM dbo.SupplierInvoiceLines WHERE SupplierInvoiceId = @supplierInvoiceId;
                DELETE FROM dbo.SupplierInvoices WHERE SupplierInvoiceId = @supplierInvoiceId;
                DELETE FROM dbo.GoodsReceipts WHERE ReceiptNumber = @receiptNumber;",
                connection);
            command.Parameters.AddWithValue("@receiptNumber", $"GR-{invoiceNumber}");
            command.Parameters.AddWithValue("@supplierInvoiceId", invoice?.SupplierInvoiceId ?? 0);
            command.Parameters.AddWithValue("@receiptPattern", $"GoodsReceipt:{invoiceNumber}%");
            await command.ExecuteNonQueryAsync();
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task OfficialFabricBatch_TwoDifferentReceiptItems_CreateSixIndependentRollIdentities()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var invoiceNumber = $"MULTI-ITEM-{uniqueSuffix}";
        var batch = new CreateFabricBatchDto
        {
            SupplierId = support.SupplierId,
            InvoiceNumber = invoiceNumber,
            PurchaseDate = DateTime.UtcNow,
            Rolls =
            [
                new CreateFabricRollDto { FabricCode = "MANUAL-A-1", CatalogNumber = "CAT-A-1", FabricType = "Item A Fabric", FabricColor = "Blue", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 120m },
                new CreateFabricRollDto { FabricCode = "MANUAL-A-2", CatalogNumber = "CAT-A-2", FabricType = "Item A Fabric", FabricColor = "Blue", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 120m },
                new CreateFabricRollDto { FabricCode = "MANUAL-A-3", CatalogNumber = "CAT-A-3", FabricType = "Item A Fabric", FabricColor = "Blue", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 120m },
                new CreateFabricRollDto { FabricCode = "MANUAL-B-1", CatalogNumber = "CAT-B-1", FabricType = "Item B Fabric", FabricColor = "Red", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 130m },
                new CreateFabricRollDto { FabricCode = "MANUAL-B-2", CatalogNumber = "CAT-B-2", FabricType = "Item B Fabric", FabricColor = "Red", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 130m },
                new CreateFabricRollDto { FabricCode = "MANUAL-B-3", CatalogNumber = "CAT-B-3", FabricType = "Item B Fabric", FabricColor = "Red", FabricWidth = 58m, QuantityYards = 25m, YardPrice = 130m }
            ]
        };

        try
        {
            var result = await CreateRepository(connectionString).ReceiveFabricBatchAsync(batch, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(6, result.TotalRolls);

            var itemCodes = await QueryStringListAsync(connectionString,
                @"SELECT DISTINCT i.ItemCode
                  FROM dbo.InventoryItems i
                  INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemID
                  INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                  INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                  INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                  WHERE gr.ReceiptNumber = @receiptNumber
                  ORDER BY i.ItemCode;",
                ("@receiptNumber", $"FAB-{invoiceNumber}"));
            var fabricRollCodes = await QueryStringListAsync(connectionString,
                @"SELECT DISTINCT fr.RollCode
                  FROM dbo.FabricRolls fr
                  INNER JOIN dbo.InventoryReceiptLines rl ON rl.FabricRollId = fr.FabricRollId
                  INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                  INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                  INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                  WHERE gr.ReceiptNumber = @receiptNumber
                  ORDER BY fr.RollCode;",
                ("@receiptNumber", $"FAB-{invoiceNumber}"));
            Assert.Equal(6, itemCodes.Count);
            Assert.Equal(6, fabricRollCodes.Count);
            Assert.All(itemCodes, value => Assert.StartsWith("FA", value, StringComparison.OrdinalIgnoreCase));
            Assert.All(fabricRollCodes, value => Assert.StartsWith("FA", value, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await CleanupOfficialFabricBatchAsync(connectionString, invoiceNumber);
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task OfficialFabricBatch_PreventsCrossItemPhysicalIdentityReuse()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var invoiceNumber = $"CROSS-ITEM-{Guid.NewGuid():N}";
        var sharedRollCode = $"FA-CROSS-{Guid.NewGuid():N}";

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var purchaseOrderId = await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.PurchaseOrders (PurchaseOrderNumber, SupplierId, OrderDate, ExpectedDeliveryDate, Status, TotalAmount, CreatedAt)
                      OUTPUT INSERTED.PurchaseOrderId
                      VALUES (@number, @supplierId, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Open', 0, SYSUTCDATETIME());",
                    ("@number", $"PO-CROSS-{Guid.NewGuid():N}"), ("@supplierId", support.SupplierId));
                var goodsReceiptId = await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.GoodsReceipts (SupplierId, PurchaseOrderId, ReceiptNumber, ReceiptDate, Notes, CreatedAt)
                      OUTPUT INSERTED.GoodsReceiptId
                      VALUES (@supplierId, @purchaseOrderId, @receiptNumber, SYSUTCDATETIME(), N'Cross-item guard seed', SYSUTCDATETIME());",
                    ("@supplierId", support.SupplierId), ("@purchaseOrderId", purchaseOrderId), ("@receiptNumber", $"FAB-{invoiceNumber}"));
                var firstItemId = await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId, ItemName, ReceivedQuantity, UnitCost, LineTotal, ItemType)
                      OUTPUT INSERTED.GoodsReceiptItemId
                      VALUES (@receiptId, N'Cross item A', 6.0, 25.5, 153.0, N'Fabric');",
                    ("@receiptId", goodsReceiptId));
                var secondItemId = await InsertScalarAsync(connection, transaction,
                    @"INSERT INTO dbo.GoodsReceiptItems (GoodsReceiptId, ItemName, ReceivedQuantity, UnitCost, LineTotal, ItemType)
                      OUTPUT INSERTED.GoodsReceiptItemId
                      VALUES (@receiptId, N'Cross item B', 6.0, 25.5, 153.0, N'Fabric');",
                    ("@receiptId", goodsReceiptId));
                await transaction.CommitAsync();

                var first = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = firstItemId,
                    ItemCode = $"FA-OWNER-{Guid.NewGuid():N}",
                    FabricTypeCode = "CrossItemGuard",
                    ColorValue = "Blue",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = Guid.NewGuid(),
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = sharedRollCode, ColorValue = "Blue", Quantity = 6m }]
                }, CancellationToken.None);
                Assert.NotNull(first);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = secondItemId,
                    ItemCode = $"FA-REUSE-{Guid.NewGuid():N}",
                    FabricTypeCode = "CrossItemGuard",
                    ColorValue = "Red",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = Guid.NewGuid(),
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = sharedRollCode, ColorValue = "Red", Quantity = 6m }]
                }, CancellationToken.None));
                Assert.Contains("different", exception.Message, StringComparison.OrdinalIgnoreCase);

                var ownerItemId = await ReadIntAsync(connectionString,
                    @"SELECT rp.GoodsReceiptItemId FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryReceiptLines rl ON rl.FabricRollId = fr.FabricRollId INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId WHERE fr.RollCode = @rollCode;",
                    ("@rollCode", sharedRollCode));
                Assert.Equal(firstItemId, ownerItemId);
            }
            finally
            {
                try
                {
                    if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                        await transaction.RollbackAsync();
                }
                catch
                {
                    // ignore: the transaction may already have been committed in the successful path.
                }
            }
        }

        await CleanupOfficialFabricBatchAsync(connectionString, invoiceNumber);
        await CleanupOfficialBatchSupportAsync(connectionString, support);
    }

    [Fact]
    public async Task OfficialFabricBatch_ReplayDoesNotAllocateNewCodesOrRows()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var invoiceNumber = $"REPLAY-{Guid.NewGuid():N}";
        var request = new CreateFabricBatchDto
        {
            SupplierId = support.SupplierId,
            InvoiceNumber = invoiceNumber,
            PurchaseDate = DateTime.UtcNow,
            Rolls =
            [
                new CreateFabricRollDto { FabricCode = "MANUAL-REPLAY-A", CatalogNumber = "CAT-RP-A", FabricType = "Replay A", FabricColor = "Navy", FabricWidth = 58m, QuantityYards = 10m, YardPrice = 100m },
                new CreateFabricRollDto { FabricCode = "MANUAL-REPLAY-B", CatalogNumber = "CAT-RP-B", FabricType = "Replay B", FabricColor = "Black", FabricWidth = 58m, QuantityYards = 12m, YardPrice = 100m }
            ]
        };

        var repository = CreateRepository(connectionString);

        try
        {
            var first = await repository.ReceiveFabricBatchAsync(request, CancellationToken.None);
            Assert.NotNull(first);
            Assert.Equal(2, first!.TotalRolls);

            var firstItems = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode LIKE 'FA%' ");
            var firstRolls = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode LIKE 'FA%' ");
            var firstLines = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryReceiptLines");
            var firstStorage = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations");
            var firstTransactions = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions");
            var firstEvents = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents");

            var replay = await repository.ReceiveFabricBatchAsync(request, CancellationToken.None);
            Assert.NotNull(replay);
            Assert.True(replay is not null);
            Assert.Equal(first.TotalRolls, replay!.TotalRolls);

            var afterItems = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItems WHERE ItemCode LIKE 'FA%' ");
            var afterRolls = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode LIKE 'FA%' ");
            var afterLines = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryReceiptLines");
            var afterStorage = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations");
            var afterTransactions = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions");
            var afterEvents = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents");

            Assert.Equal(firstItems, afterItems);
            Assert.Equal(firstRolls, afterRolls);
            Assert.Equal(firstLines, afterLines);
            Assert.Equal(firstStorage, afterStorage);
            Assert.Equal(firstTransactions, afterTransactions);
            Assert.Equal(firstEvents, afterEvents);
        }
        finally
        {
            await CleanupOfficialFabricBatchAsync(connectionString, invoiceNumber);
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task OfficialFabricBatch_DifferentOperationGeneratesNewOfficialIdentity()
    {
        var connectionString = GetConnectionString();
        var support = await SeedOfficialBatchSupportAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var firstInvoice = $"DIFF-OP-{Guid.NewGuid():N}";
        var secondInvoice = $"DIFF-OP-{Guid.NewGuid():N}";

        try
        {
            var first = await repository.ReceiveFabricBatchAsync(new CreateFabricBatchDto
            {
                SupplierId = support.SupplierId,
                InvoiceNumber = firstInvoice,
                PurchaseDate = DateTime.UtcNow,
                Rolls = [new CreateFabricRollDto { FabricCode = "MANUAL-OP-1", CatalogNumber = "CAT-OP-1", FabricType = "Diff op A", FabricColor = "Green", FabricWidth = 58m, QuantityYards = 8m, YardPrice = 120m }]
            }, CancellationToken.None);
            var second = await repository.ReceiveFabricBatchAsync(new CreateFabricBatchDto
            {
                SupplierId = support.SupplierId,
                InvoiceNumber = secondInvoice,
                PurchaseDate = DateTime.UtcNow,
                Rolls = [new CreateFabricRollDto { FabricCode = "MANUAL-OP-2", CatalogNumber = "CAT-OP-2", FabricType = "Diff op B", FabricColor = "Green", FabricWidth = 58m, QuantityYards = 9m, YardPrice = 120m }]
            }, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);

            var officialCodes = await QueryStringListAsync(connectionString,
                @"SELECT DISTINCT i.ItemCode FROM dbo.InventoryItems i INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemID INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId WHERE gr.ReceiptNumber IN (@first, @second) ORDER BY i.ItemCode;",
                ("@first", $"FAB-{firstInvoice}"), ("@second", $"FAB-{secondInvoice}"));

            Assert.Equal(2, officialCodes.Count);
            Assert.All(officialCodes, code => Assert.StartsWith("FA", code, StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(officialCodes[0], officialCodes[1]);
        }
        finally
        {
            await CleanupOfficialFabricBatchAsync(connectionString, firstInvoice);
            await CleanupOfficialFabricBatchAsync(connectionString, secondInvoice);
            await CleanupOfficialBatchSupportAsync(connectionString, support);
        }
    }

    [Fact]
    public async Task LiveStorageBatch_UsesOfficialServerAllocatedFabricAndCatalogIdentity()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            var request = new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = "REQUEST-LEGACY-ITEM",
                FabricTypeCode = "COTTON-TEST",
                ColorValue = "WHITE",
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = Guid.NewGuid(),
                CatalogNumber = "REQUEST-CAT-777",
                Rolls =
                [
                    new ReceiveFabricInventoryRollDto { FabricCode = "ROLL-LEGACY-ONE", ColorValue = "WHITE", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = "ROLL-LEGACY-TWO", ColorValue = "WHITE", Quantity = 3m }
                ]
            };

            var result = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5m, result.Quantity);
            Assert.StartsWith("FA", result.ItemCode, StringComparison.OrdinalIgnoreCase);

            var itemCodes = await QueryStringListAsync(connectionString,
                @"SELECT DISTINCT i.ItemCode
                  FROM dbo.InventoryItems i
                  INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemID
                  INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                  WHERE rp.GoodsReceiptItemId = @goodsReceiptItemId
                  ORDER BY i.ItemCode;",
                ("@goodsReceiptItemId", seed.FabricReceiptItemId));

            Assert.Equal(2, itemCodes.Count);
            Assert.All(itemCodes, code => Assert.StartsWith("FA", code, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode LIKE 'FA%' AND InventoryItemId IN (SELECT InventoryItemId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@item))", ("@item", seed.FabricReceiptItemId)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(DISTINCT Barcode) FROM dbo.InventoryItems WHERE InventoryItemID IN (SELECT InventoryItemId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@item)) AND Barcode IS NOT NULL AND Barcode LIKE 'CAT%'", ("@item", seed.FabricReceiptItemId)));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task StorageLinkedFabricBatch_DoesNotCreatePurchaseOrderOrGoodsReceipt()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        var beforePurchaseOrders = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.PurchaseOrders");
        var beforeGoodsReceipts = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts");
        var beforeStorageAllocations = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId));

        try
        {
            var result = await repository.ReceiveFabricBatchAsync(new CreateFabricBatchDto
            {
                SupplierId = seed.SupplierId,
                InvoiceNumber = "STORAGE-LINK-TEST",
                Rolls =
                [
                    new CreateFabricRollDto
                    {
                        FabricCode = seed.FabricItemCode,
                        FabricType = "Storage linked fabric",
                        FabricColor = "White",
                        FabricWidth = 58m,
                        QuantityYards = 6m,
                        YardPrice = 25.5m,
                        GoodsReceiptItemId = seed.FabricReceiptItemId,
                        StorageOperationId = Guid.NewGuid()
                    }
                ]
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result.TotalRolls);
            Assert.Equal(6m, result.TotalYards);
            Assert.Equal(153m, result.TotalCost);
            Assert.Equal(beforePurchaseOrders, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.PurchaseOrders"));
            Assert.Equal(beforeGoodsReceipts, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts"));
            Assert.Equal(beforeStorageAllocations + 1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId)));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task StorageLinkedFabricBatch_ReplayWithSameOperationIsIdempotent()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var sameOperationId = Guid.NewGuid();
        var beforeStorageAllocations = await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId));

        try
        {
            var request = new CreateFabricBatchDto
            {
                SupplierId = seed.SupplierId,
                InvoiceNumber = "STORAGE-LINK-REPLAY",
                Rolls =
                [
                    new CreateFabricRollDto
                    {
                        FabricCode = seed.FabricItemCode,
                        FabricType = "Storage linked fabric",
                        FabricColor = "White",
                        FabricWidth = 58m,
                        QuantityYards = 3m,
                        YardPrice = 25.5m,
                        GoodsReceiptItemId = seed.FabricReceiptItemId,
                        StorageOperationId = sameOperationId
                    },
                    new CreateFabricRollDto
                    {
                        FabricCode = seed.FabricItemCode,
                        FabricType = "Storage linked fabric",
                        FabricColor = "White",
                        FabricWidth = 58m,
                        QuantityYards = 3m,
                        YardPrice = 25.5m,
                        GoodsReceiptItemId = seed.FabricReceiptItemId,
                        StorageOperationId = sameOperationId
                    }
                ]
            };

            var first = await repository.ReceiveFabricBatchAsync(request, CancellationToken.None);
            var replay = await repository.ReceiveFabricBatchAsync(request, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(replay);
            Assert.Equal(2, first!.TotalRolls);
            Assert.Equal(6m, first.TotalYards);
            Assert.Equal(beforeStorageAllocations + 1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item AND StorageOperationId=@operation", ("@item", seed.FabricReceiptItemId), ("@operation", sameOperationId)));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
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
                    ColorValue = "WHITE",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId,
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, ColorValue = "WHITE", Quantity = 6m }]
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
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId,
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, ColorValue = "WHITE", Quantity = 6m }]
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
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId
                },
                CancellationToken.None));

            var consumableReceipt = await repository.ReceiveConsumableInventoryAsync(
                new ReceiveConsumableInventoryDto
                {
                    GoodsReceiptItemId = seed.ConsumableReceiptItemId,
                    ItemCode = seed.ConsumableItemCode,
                    UnitId = 3,
                    OpposingLedgerAccountCode = "2100",
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
    public async Task FoundationReceipt_UsesApprovedOpposingAccountAndCreatesBalancedAccountingEvent()
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
                    FabricTypeCode = "ACCOUNTING-CONTRACT",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = Guid.NewGuid(),
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = "ACC-CONTRACT-001", Quantity = 6m }]
                },
                CancellationToken.None);

            Assert.NotNull(receipt);
            Assert.False(receipt!.IsExisting);
            Assert.Equal(7, await ReadEventTypeAsync(connectionString, receipt.AccountingEventId));
            Assert.Equal(0m, await ReadJournalDifferenceAsync(connectionString, receipt.AccountingEventId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventId=@id AND InventoryReceiptPostingId=@posting", ("@id", receipt.AccountingEventId), ("@posting", receipt.SourceRecordId)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE AccountingEventId=@id", ("@id", receipt.AccountingEventId)));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReceiveFabricInventoryAsync(
                new ReceiveFabricInventoryDto
                {
                    GoodsReceiptItemId = seed.FabricReceiptItemId,
                    ItemCode = seed.FabricItemCode,
                    FabricTypeCode = "ACCOUNTING-CONTRACT",
                    UnitId = 1,
                    OpposingLedgerAccountCode = "1000",
                    SourceOperationId = Guid.NewGuid(),
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = "ACC-CONTRACT-002", Quantity = 6m }]
                },
                CancellationToken.None));

            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE InventoryReceiptPostingId=@posting AND AccountingEventType=7", ("@posting", receipt.SourceRecordId)));
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
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = seed.FabricReceiptOperationId,
                Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = "REPLAY-ROLL-001", Quantity = 6m }]
            }, CancellationToken.None);
            Assert.NotNull(first);
            Assert.False(first!.IsExisting);

            var replay = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = "REPLAY-ROLL",
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = seed.FabricReceiptOperationId,
                Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = "REPLAY-ROLL-001", Quantity = 6m }]
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
    public async Task FoundationReceipt_ReusesExistingActiveFabricCodeInsteadOfAllocatingReplacement()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);

        try
        {
            await using (var setupConnection = new SqlConnection(connectionString))
            {
                await setupConnection.OpenAsync();
                await using var setupTransaction = (SqlTransaction)await setupConnection.BeginTransactionAsync();
                try
                {
                    var itemId = await InsertScalarAsync(setupConnection, setupTransaction,
                        @"
                        INSERT INTO dbo.InventoryItems
                            (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity, IsActive, CreatedAt, UpdatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
                        OUTPUT INSERTED.InventoryItemID
                        VALUES
                            (@itemCode, N'Active foundation fabric reuse seed', N'Foundation', N'Yard', 6, 6, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'ReUSE-EXISTING-FABRIC', N'White', NULL, NULL, NULL, NULL);",
                        ("@itemCode", seed.FabricItemCode));

                    await InsertScalarAsync(setupConnection, setupTransaction,
                        @"
                        INSERT INTO dbo.InventoryItemFoundation
                            (InventoryItemId, InventoryClassId, UnitId, CurrencyCode, OriginalQuantity, AvailableQuantity, ConsumedQuantity, OfficialUnitCost, OperationalValue, CreatedAt, UpdatedAt)
                        VALUES
                            (@itemId, 1, 1, N'YER', 6, 6, 0, 25.50, 153.00, SYSUTCDATETIME(), SYSUTCDATETIME());",
                        ("@itemId", itemId));

                    await setupTransaction.CommitAsync();
                }
                catch
                {
                    await setupTransaction.RollbackAsync();
                    throw;
                }
            }

            var receipt = await repository.ReceiveFabricInventoryAsync(new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = "REUSE-EXISTING-FABRIC",
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = Guid.NewGuid(),
                Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, Quantity = 6m }]
            }, CancellationToken.None);

            Assert.NotNull(receipt);
            Assert.False(receipt!.IsExisting);
            Assert.Equal(seed.FabricItemCode, await ReadStringAsync(connectionString, "SELECT ItemCode FROM dbo.InventoryItems WHERE InventoryItemID=@id", ("@id", receipt.InventoryItemId)));
            Assert.Equal(12m, await ReadDecimalAsync(connectionString, "SELECT AvailableQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", receipt.InventoryItemId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id AND AvailableQuantity=12 AND OriginalQuantity=12", ("@id", receipt.InventoryItemId)));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationReceipt_MultiRollBatch_StoresEachRollWithoutDuplication()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var operationId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        try
        {
            var request = new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                CatalogNumber = $"CAT-STORE-{suffix}",
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = $"MULTI-ROLL-{suffix}",
                FabricWidth = 58m,
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = operationId,
                Rolls =
                [
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-MR-{suffix}-001", ColorValue = "Navy", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-MR-{suffix}-002", ColorValue = "Black", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-MR-{suffix}-003", ColorValue = "Beige", Quantity = 2m }
                ]
            };

            var result = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(result);
            Assert.False(result!.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId=@item AND SourceOperationId=@operation", ("@item", seed.FabricReceiptItemId), ("@operation", operationId)));
            Assert.Equal(3, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls WHERE RollCode IN (@r1,@r2,@r3)", ("@r1", $"FA-MR-{suffix}-001"), ("@r2", $"FA-MR-{suffix}-002"), ("@r3", $"FA-MR-{suffix}-003")));
            Assert.Equal(3, await CountAsync(connectionString, "SELECT COUNT(DISTINCT SourceOperationId) FROM dbo.InventoryTransactions WHERE SourceOperationId IS NOT NULL AND InventoryItemId IN (SELECT InventoryItemId FROM dbo.InventoryItems WHERE ItemCode IN (@r1,@r2,@r3))", ("@r1", $"FA-MR-{suffix}-001"), ("@r2", $"FA-MR-{suffix}-002"), ("@r3", $"FA-MR-{suffix}-003")));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT SUM(ReceivedQuantity) FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId=@id", result.SourceRecordId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item AND StorageOperationId=@operation", ("@item", seed.FabricReceiptItemId), ("@operation", operationId)));
            Assert.Equal(6m, await ReadDecimalAsync(connectionString, "SELECT SUM(StoredQuantity) FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId=@item", ("@item", seed.FabricReceiptItemId)));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationReceipt_SingleSupplierInvoiceLine_KeepsSharedCatalogAcrossDistinctRollFabricCodes()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var operationId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sharedCatalog = $"CAT-SHARED-{suffix}";

        try
        {
            var request = new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                CatalogNumber = sharedCatalog,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = $"CAT-SHARED-{suffix}",
                FabricWidth = 58m,
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = operationId,
                Rolls =
                [
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-001", ColorValue = "Navy", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-002", ColorValue = "Black", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-003", ColorValue = "Beige", Quantity = 2m }
                ]
            };

            var result = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(result);
            Assert.False(result!.IsExisting);

            var firstCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-001");
            var secondCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-002");
            var thirdCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-003");

            Assert.NotNull(firstCatalog);
            Assert.NotNull(secondCatalog);
            Assert.NotNull(thirdCatalog);
            Assert.StartsWith("CAT", firstCatalog!);
            Assert.StartsWith("CAT", secondCatalog!);
            Assert.StartsWith("CAT", thirdCatalog!);
            Assert.Equal(firstCatalog, secondCatalog);
            Assert.Equal(firstCatalog, thirdCatalog);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(DISTINCT Barcode) FROM dbo.InventoryItems WHERE ItemCode IN (@c1,@c2,@c3)", ("@c1", $"FA-CAT-{suffix}-001"), ("@c2", $"FA-CAT-{suffix}-002"), ("@c3", $"FA-CAT-{suffix}-003")));
            Assert.Equal(3, await CountAsync(connectionString, "SELECT COUNT(DISTINCT i.ItemCode) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode IN (@c1,@c2,@c3)", ("@c1", $"FA-CAT-{suffix}-001"), ("@c2", $"FA-CAT-{suffix}-002"), ("@c3", $"FA-CAT-{suffix}-003")));

            var replay = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(replay);
            Assert.True(replay!.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(DISTINCT Barcode) FROM dbo.InventoryItems WHERE ItemCode IN (@c1,@c2,@c3)", ("@c1", $"FA-CAT-{suffix}-001"), ("@c2", $"FA-CAT-{suffix}-002"), ("@c3", $"FA-CAT-{suffix}-003")));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationReceipt_MultiFabricItemBatch_GeneratesDistinctCatalogNumbersPerDistinctFabricItem()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var operationId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        try
        {
            var request = new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = $"CAT-UNIQUE-{suffix}",
                FabricWidth = 58m,
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = operationId,
                Rolls =
                [
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-001", ColorValue = "Navy", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-002", ColorValue = "Navy", Quantity = 2m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-003", ColorValue = "Black", Quantity = 1m },
                    new ReceiveFabricInventoryRollDto { FabricCode = $"FA-CAT-{suffix}-004", ColorValue = "Black", Quantity = 1m }
                ]
            };

            var result = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(result);
            Assert.False(result!.IsExisting);

            var firstCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-001");
            var secondCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-002");
            var thirdCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-003");
            var fourthCatalog = await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-004");

            Assert.NotNull(firstCatalog);
            Assert.NotNull(secondCatalog);
            Assert.NotNull(thirdCatalog);
            Assert.NotNull(fourthCatalog);
            Assert.StartsWith("CAT", firstCatalog!);
            Assert.StartsWith("CAT", secondCatalog!);
            Assert.StartsWith("CAT", thirdCatalog!);
            Assert.StartsWith("CAT", fourthCatalog!);
            Assert.NotEqual(firstCatalog, secondCatalog);
            Assert.NotEqual(thirdCatalog, fourthCatalog);
            Assert.Equal(4, await CountAsync(connectionString, "SELECT COUNT(DISTINCT Barcode) FROM dbo.InventoryItems WHERE ItemCode IN (@c1,@c2,@c3,@c4)", ("@c1", $"FA-CAT-{suffix}-001"), ("@c2", $"FA-CAT-{suffix}-002"), ("@c3", $"FA-CAT-{suffix}-003"), ("@c4", $"FA-CAT-{suffix}-004")));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode=@code AND i.Barcode=@catalog", ("@code", $"FA-CAT-{suffix}-001"), ("@catalog", firstCatalog)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode=@code AND i.Barcode=@catalog", ("@code", $"FA-CAT-{suffix}-002"), ("@catalog", secondCatalog)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode=@code AND i.Barcode=@catalog", ("@code", $"FA-CAT-{suffix}-003"), ("@catalog", thirdCatalog)));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode=@code AND i.Barcode=@catalog", ("@code", $"FA-CAT-{suffix}-004"), ("@catalog", fourthCatalog)));

            var replay = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(replay);
            Assert.True(replay!.IsExisting);
            Assert.Equal(firstCatalog, await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-001"));
            Assert.Equal(secondCatalog, await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-002"));
            Assert.Equal(thirdCatalog, await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-003"));
            Assert.Equal(fourthCatalog, await ReadStringAsync(connectionString, "SELECT Barcode FROM dbo.InventoryItems WHERE ItemCode=@code", $"FA-CAT-{suffix}-004"));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task FoundationReceipt_TenRollBatch_KeepsOneSharedCatalogButUniqueFabricCodes()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sharedCatalog = $"CAT-TEN-{suffix}";

        try
        {
            var request = new ReceiveFabricInventoryDto
            {
                GoodsReceiptItemId = seed.FabricReceiptItemId,
                CatalogNumber = sharedCatalog,
                ItemCode = seed.FabricItemCode,
                FabricTypeCode = $"TEN-ROLL-{suffix}",
                FabricWidth = 58m,
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = Guid.NewGuid(),
                Rolls = Enumerable.Range(1, 10)
                    .Select(index => new ReceiveFabricInventoryRollDto
                    {
                        FabricCode = $"FA-TEN-{suffix}-{index:D2}",
                        ColorValue = index % 2 == 0 ? "Black" : "Navy",
                        Quantity = 0.6m
                    })
                    .ToList()
            };

            var result = await repository.ReceiveFabricInventoryAsync(request, CancellationToken.None);
            Assert.NotNull(result);
            Assert.False(result!.IsExisting);

            var distinctFabricCodes = await CountAsync(connectionString,
                "SELECT COUNT(DISTINCT i.ItemCode) FROM dbo.FabricRolls fr INNER JOIN dbo.InventoryItems i ON i.InventoryItemID = fr.InventoryItemId WHERE i.ItemCode IN (@c1,@c2,@c3,@c4,@c5,@c6,@c7,@c8,@c9,@c10)",
                ("@c1", $"FA-TEN-{suffix}-01"), ("@c2", $"FA-TEN-{suffix}-02"), ("@c3", $"FA-TEN-{suffix}-03"), ("@c4", $"FA-TEN-{suffix}-04"), ("@c5", $"FA-TEN-{suffix}-05"), ("@c6", $"FA-TEN-{suffix}-06"), ("@c7", $"FA-TEN-{suffix}-07"), ("@c8", $"FA-TEN-{suffix}-08"), ("@c9", $"FA-TEN-{suffix}-09"), ("@c10", $"FA-TEN-{suffix}-10"));
            Assert.Equal(10, distinctFabricCodes);

            var catalogCount = await CountAsync(connectionString,
                "SELECT COUNT(DISTINCT Barcode) FROM dbo.InventoryItems WHERE ItemCode IN (@c1,@c2,@c3,@c4,@c5,@c6,@c7,@c8,@c9,@c10)",
                ("@c1", $"FA-TEN-{suffix}-01"), ("@c2", $"FA-TEN-{suffix}-02"), ("@c3", $"FA-TEN-{suffix}-03"), ("@c4", $"FA-TEN-{suffix}-04"), ("@c5", $"FA-TEN-{suffix}-05"), ("@c6", $"FA-TEN-{suffix}-06"), ("@c7", $"FA-TEN-{suffix}-07"), ("@c8", $"FA-TEN-{suffix}-08"), ("@c9", $"FA-TEN-{suffix}-09"), ("@c10", $"FA-TEN-{suffix}-10"));
            Assert.Equal(1, catalogCount);
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
                UnitId = 1,
                OpposingLedgerAccountCode = "2100",
                SourceOperationId = partialOperationId,
                Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = $"ROLL-PARTIAL-{Guid.NewGuid():N}", Quantity = 6m }]
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
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId,
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, Quantity = 6m }]
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
                    UnitId = unitId,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId,
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, Quantity = 6m }]
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
                    UnitId = 1,
                    OpposingLedgerAccountCode = "2100",
                    SourceOperationId = seed.FabricReceiptOperationId,
                    Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricRollCode, Quantity = 6m }]
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
            var repository = CreateOrderRepository(connectionString);
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
            var repository = CreateOrderRepository(connectionString);
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
            var repository = CreateOrderRepository(connectionString);
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
            var repository = CreateOrderRepository(connectionString);
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
            var repository = CreateOrderRepository(connectionString);
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
            var repository = CreateOrderRepository(connectionString);
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
            var user = new CurrentUserDto(1, "fabric-user", "Fabric User", "Customer", true, DateTime.UtcNow, "Customer", 1, null, null);
            var controller = new OrdersController(new OrderService(repository, null!, null!), new FixedAuthenticatedUserContext(user));
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
            var repository = CreateOrderRepository(connectionString);
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
            UnitId = 1,
            OpposingLedgerAccountCode = "2100",
            SourceOperationId = seed.FabricReceiptOperationId,
            Rolls = [new ReceiveFabricInventoryRollDto { FabricCode = seed.FabricItemCode, Quantity = 6m }]
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
            DELETE FROM dbo.TailoringPieceSaleValues WHERE PieceID IN(SELECT PieceID FROM dbo.Pieces WHERE OrderItemID IN(SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders)));
            DELETE FROM dbo.Pieces WHERE OrderItemID IN(SELECT OrderItemID FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders));
            DELETE FROM dbo.OrderItems WHERE OrderID IN(SELECT OrderId FROM @orders);
            DELETE FROM dbo.Orders WHERE OrderID IN(SELECT OrderId FROM @orders);", connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private sealed class FixedAuthenticatedUserContext(CurrentUserDto? user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
    }

    private sealed class TestPricingEngine : IPricingEngineService
    {
        public Task<PricingEngineResponseDto> CalculateAsync(PricingEngineRequestDto request, CancellationToken cancellationToken)
        {
            var finalPricePerPiece = 25.5m * request.Consumption / 36m + 5m;
            return Task.FromResult(new PricingEngineResponseDto(
                true,
                Array.Empty<string>(),
                request.ProductTypeId,
                request.FabricCode,
                "Inch",
                request.Consumption,
                request.Quantity,
                1m,
                request.Consumption,
                request.Consumption * request.Quantity,
                1m,
                1m,
                1m,
                1m,
                4m,
                finalPricePerPiece,
                finalPricePerPiece * request.Quantity,
                0m,
                0m,
                request.Consumption + 4m,
                0m,
                0m,
                finalPricePerPiece,
                finalPricePerPiece * request.Quantity,
                "Test",
                "Test"));
        }
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

    private static OrderRepository CreateOrderRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new OrderRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new MeasurementConsumptionRules(),
            new TestPricingEngine());
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_TAILORING_EDIT_TEST_20261101;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog ?? string.Empty;

        var allowedDatabases = new[]
        {
            "LUMAR_ERP_TEST_R3_ROLLS",
            "LUMAR_ERP_TEST_R3_ROLLS_LOCAL",
            "LUMAR_TAILORING_EDIT_TEST_20261101"
        };

        if (string.Equals(databaseName, "LUMAR_ERP_ES_VALIDATION_R3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "LUMAR_ERP_VIP_2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "LUMAR_ERP", StringComparison.OrdinalIgnoreCase)
            || !allowedDatabases.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Fabric foundation tests are fail-closed and must target an isolated test database only. Detected '{databaseName}'.");
        }

        return builder.ConnectionString;
    }

    private static async Task<TestSeed> SeedAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var fabricCode = $"FAB-FOUND-{suffix[..12]}";
        var seed = new TestSeed(
            fabricCode,
            $"CON-FOUND-{suffix[..12]}",
            fabricCode,
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
            var reversalDefinitionExisted = await ReadBoolAsync(connection, transaction, "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 33) THEN 1 ELSE 0 END AS bit)");
            var reversalDefinitionWasEnabled = await ReadBoolAsync(connection, transaction, "SELECT CAST(ISNULL((SELECT IsEnabled FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 33), 0) AS bit)");
            await SeedLedgerAccountsAsync(connection, transaction);
            await SeedInventoryCatalogsAsync(connection, transaction);
            await SeedPricingProductTypesAsync(connection, transaction);
            await using (var reversalDefinitionCommand = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 33)
                BEGIN
                    INSERT INTO dbo.AccountingEventDefinitions
                        (AccountingEventType, EventName, SourceType, DebitAccountRole, CreditAccountRole, CashDirection, RequiresCashMovement, LiabilityImpact, ExpenseImpact, InventoryImpact, IsEnabled, IsBusinessRuntimeEnabled)
                    VALUES
                        (33, N'Reversal', N'AccountingEvent', NULL, NULL, NULL, 0, 0, 0, 0, 1, 0);
                END
                ELSE
                BEGIN
                    UPDATE dbo.AccountingEventDefinitions
                    SET EventName = N'Reversal',
                        SourceType = N'AccountingEvent',
                        DebitAccountRole = NULL,
                        CreditAccountRole = NULL,
                        CashDirection = NULL,
                        RequiresCashMovement = 0,
                        LiabilityImpact = 0,
                        ExpenseImpact = 0,
                        InventoryImpact = 0,
                        IsEnabled = 1,
                        IsBusinessRuntimeEnabled = 0
                    WHERE AccountingEventType = 33;
                END;", connection, transaction))
            {
                await reversalDefinitionCommand.ExecuteNonQueryAsync();
            }
            var cashAccountId = await SeedCashAccountAsync(connection, transaction, suffix);
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
                OrderId = orderId,
                CashAccountId = cashAccountId,
                ReversalDefinitionExisted = reversalDefinitionExisted,
                ReversalDefinitionWasEnabled = reversalDefinitionWasEnabled
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
            DECLARE @eventIds TABLE (AccountingEventId bigint PRIMARY KEY);
            DECLARE @postingIds TABLE (InventoryReceiptPostingId bigint PRIMARY KEY);
            DECLARE @lineIds TABLE (InventoryReceiptLineId bigint PRIMARY KEY);
            DECLARE @allocationIds TABLE (StorageAllocationId bigint PRIMARY KEY);
            DECLARE @transactionIds TABLE (TransactionId int PRIMARY KEY);

            INSERT INTO @postingIds
            SELECT DISTINCT rp.InventoryReceiptPostingId
            FROM dbo.InventoryReceiptPostings rp
            WHERE rp.SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation)
               OR rp.GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);

            INSERT INTO @lineIds
            SELECT DISTINCT irl.InventoryReceiptLineId
            FROM dbo.InventoryReceiptLines irl
            WHERE irl.InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);

            INSERT INTO @eventIds
            SELECT DISTINCT ae.AccountingEventId
            FROM dbo.AccountingEvents ae
            WHERE ae.InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds)
               OR ae.FabricConsumptionSourceId IN (
                    SELECT FabricConsumptionSourceId FROM dbo.FabricConsumptionSources WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation))
               OR ae.ProductionMaterialConsumptionId IN (
                    SELECT ProductionMaterialConsumptionId FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation))
               OR ae.OrderId = @orderId
               OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId)
               OR ae.MeasurementCardPrintHistoryId IN (SELECT MeasurementCardPrintHistoryId FROM dbo.MeasurementCardPrintHistory WHERE OrderId = @orderId)
               OR ae.ReadyMadeInventoryProductId IN (SELECT ReadyMadeInventoryProductId FROM dbo.ReadyMadeInventoryProducts WHERE OrderId = @orderId)
               OR ae.CustomerAdvanceApplicationId IN (
                    SELECT AdvanceApplicationId
                    FROM dbo.CustomerAdvanceApplications
                    WHERE OrderId = @orderId
                       OR AdvancePaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId))
               OR ae.ToolIssuanceId IN (SELECT ToolIssuanceId FROM dbo.ToolIssuances WHERE OrderId = @orderId)
               OR ae.ToolIssuanceReversalId IN (SELECT ToolIssuanceReversalId FROM dbo.ToolIssuanceReversals WHERE OrderId = @orderId)
               OR ae.ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE OrderId = @orderId)
               OR ae.ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId = @orderId)
               OR ae.ReadyMadeSaleCostPostingId IN (SELECT ReadyMadeSaleCostPostingId FROM dbo.ReadyMadeSaleCostPostings WHERE OrderId = @orderId);

            INSERT INTO @transactionIds
            SELECT DISTINCT it.TransactionID
            FROM dbo.InventoryTransactions it
            WHERE it.SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
               OR it.AccountingEventId IN (SELECT AccountingEventId FROM @eventIds)
               OR it.TransactionID IN (SELECT InventoryTransactionId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds));

            INSERT INTO @allocationIds
            SELECT DISTINCT sga.StorageAllocationId
            FROM dbo.GoodsReceiptItemStorageAllocations sga
            WHERE sga.GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId)
               OR sga.StorageOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation); 

            UPDATE dbo.InventoryTransactions
            SET AccountingEventId = NULL
            WHERE TransactionID IN (SELECT TransactionId FROM @transactionIds)
               OR SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation);

            UPDATE dbo.InventoryReceiptPostings
            SET AccountingEventId = NULL
            WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);

            UPDATE dbo.InventoryReceiptLines
            SET FabricRollId = NULL, InventoryTransactionId = NULL, AccountingEventId = NULL
            WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);

            UPDATE dbo.FabricConsumptionSources
            SET AccountingEventId = NULL,
                InventoryTransactionId = NULL,
                PieceId = NULL,
                FabricRollId = NULL
            WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
               OR PieceId = @pieceId
               OR AccountingEventId IN (SELECT AccountingEventId FROM @eventIds)
               OR InventoryTransactionId IN (SELECT TransactionId FROM @transactionIds);

            UPDATE dbo.ProductionMaterialConsumptions
            SET AccountingEventId = NULL,
                InventoryTransactionId = NULL
            WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
               OR AccountingEventId IN (SELECT AccountingEventId FROM @eventIds)
               OR InventoryTransactionId IN (SELECT TransactionId FROM @transactionIds);

            UPDATE dbo.AccountingEvents
            SET PaymentId = NULL,
                OrderId = NULL,
                MeasurementCardPrintHistoryId = NULL,
                ReadyMadeInventoryProductId = NULL,
                CustomerAdvanceApplicationId = NULL,
                InventoryReceiptPostingId = NULL,
                FabricConsumptionSourceId = NULL,
                ProductionMaterialConsumptionId = NULL,
                ReadyMadeSaleCostPostingId = NULL,
                ImportedReadyMadeInventoryReceiptId = NULL,
                ImportedReadyMadeSaleCostPostingId = NULL,
                ToolIssuanceId = NULL,
                ToolIssuanceReversalId = NULL,
                SourceType = NULL,
                SourceId = NULL,
                SourceOperationId = NULL,
                OriginalAccountingEventId = NULL
            WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds)
               OR OrderId = @orderId
               OR PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId)
               OR SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
               OR CustomerAdvanceApplicationId IN (
                    SELECT AdvanceApplicationId
                    FROM dbo.CustomerAdvanceApplications
                    WHERE OrderId = @orderId
                       OR AdvancePaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId));

            DELETE FROM dbo.GoodsReceiptItemStorageAllocations
            WHERE StorageAllocationId IN (SELECT StorageAllocationId FROM @allocationIds);

            DELETE FROM dbo.JournalEntryLines
            WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries
            WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions
            WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents
            WHERE AccountingEventId IN (
                SELECT ae.AccountingEventId
                FROM dbo.AccountingEvents ae
                WHERE ae.AccountingEventId IN (SELECT AccountingEventId FROM @eventIds)
                   OR ae.OrderId = @orderId
                   OR ae.PaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId)
                   OR ae.CustomerAdvanceApplicationId IN (
                        SELECT AdvanceApplicationId
                        FROM dbo.CustomerAdvanceApplications
                        WHERE OrderId = @orderId
                           OR AdvancePaymentId IN (SELECT PaymentID FROM dbo.Payments WHERE OrderID = @orderId))
                   OR ae.InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds)
                   OR ae.FabricConsumptionSourceId IN (
                        SELECT FabricConsumptionSourceId
                        FROM dbo.FabricConsumptionSources
                        WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation))
                   OR ae.ProductionMaterialConsumptionId IN (
                        SELECT ProductionMaterialConsumptionId
                        FROM dbo.ProductionMaterialConsumptions
                        WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation))
                   OR ae.MeasurementCardPrintHistoryId IN (SELECT MeasurementCardPrintHistoryId FROM dbo.MeasurementCardPrintHistory WHERE OrderId = @orderId)
                   OR ae.ReadyMadeInventoryProductId IN (SELECT ReadyMadeInventoryProductId FROM dbo.ReadyMadeInventoryProducts WHERE OrderId = @orderId)
                   OR ae.ReadyMadeSaleCostPostingId IN (SELECT ReadyMadeSaleCostPostingId FROM dbo.ReadyMadeSaleCostPostings WHERE OrderId = @orderId)
                   OR ae.ImportedReadyMadeInventoryReceiptId IN (SELECT ImportedReadyMadeInventoryReceiptId FROM dbo.ImportedReadyMadeInventoryReceipts WHERE OrderId = @orderId)
                   OR ae.ImportedReadyMadeSaleCostPostingId IN (SELECT ImportedReadyMadeSaleCostPostingId FROM dbo.ImportedReadyMadeSaleCostPostings WHERE OrderId = @orderId)
                   OR ae.ToolIssuanceId IN (SELECT ToolIssuanceId FROM dbo.ToolIssuances WHERE OrderId = @orderId)
                   OR ae.ToolIssuanceReversalId IN (SELECT ToolIssuanceReversalId FROM dbo.ToolIssuanceReversals WHERE OrderId = @orderId)
            );
            DELETE FROM dbo.FabricConsumptionSources
            WHERE SourceOperationId IN (@fabricReceiptOperation,@consumableReceiptOperation,@fabricConsumptionOperation,@consumableConsumptionOperation)
               OR PieceId = @pieceId
               OR AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);

            DELETE FROM dbo.FabricRolls
            WHERE InventoryItemId IN (
                SELECT InventoryItemID
                FROM dbo.InventoryItems
                WHERE ItemCode IN (@fabricCode,@consumableCode)
            );

            DELETE FROM dbo.InventoryReceiptLines
            WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);

            DELETE FROM dbo.InventoryTransactions
            WHERE TransactionID IN (SELECT TransactionId FROM @transactionIds);

            DELETE FROM dbo.InventoryReceiptPostings
            WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);

            DELETE FROM dbo.FabricConsumptionSources WHERE SourceOperationId = @fabricConsumptionOperation;
            DELETE FROM dbo.ProductionMaterialConsumptions WHERE SourceOperationId = @consumableConsumptionOperation;

            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (
                SELECT InventoryItemID FROM dbo.InventoryItems WHERE ItemCode IN (@fabricCode,@consumableCode)
            );
            DELETE FROM dbo.InventoryItems WHERE ItemCode IN (@fabricCode,@consumableCode);

            DELETE FROM dbo.TailoringPieceSaleValues WHERE PieceID = @pieceId;
            DELETE FROM dbo.Pieces WHERE PieceID = @pieceId;
            DELETE FROM dbo.OrderItems WHERE OrderItemID = @orderItemId;
            DELETE FROM dbo.ProductionOrders WHERE ProductionOrderId = @productionOrderId;
            DELETE FROM dbo.BillOfMaterials WHERE BillOfMaterialsId = @bomId;
            DELETE FROM dbo.Products WHERE ProductId = @productId;
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptItemId IN (@fabricReceiptItemId,@consumableReceiptItemId);
            DELETE FROM dbo.GoodsReceipts WHERE GoodsReceiptId = @goodsReceiptId;
            DELETE FROM dbo.PurchaseOrders WHERE PurchaseOrderId = @purchaseOrderId;
            DELETE FROM dbo.Orders WHERE OrderID = @orderId;
            IF @reversalDefinitionExists = 1
            BEGIN
                UPDATE dbo.AccountingEventDefinitions
                SET IsEnabled = @reversalDefinitionWasEnabled,
                    IsBusinessRuntimeEnabled = 0
                WHERE AccountingEventType = 33;
            END
            ELSE
            BEGIN
                DELETE FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 33;
            END;

            DELETE FROM dbo.CashMovements WHERE CashAccountId = @cashAccountId;
            DELETE FROM dbo.SupplierFinancialPayments WHERE CashAccountId = @cashAccountId;
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.System_Settings
                WHERE SettingName = N'GeneralCashAccountId'
                  AND TRY_CONVERT(int, SettingValue) = @cashAccountId
            )
            BEGIN
                DELETE FROM dbo.CashAccounts WHERE CashAccountId = @cashAccountId;
            END
            DELETE FROM dbo.Suppliers WHERE SupplierId = @supplierId;
            /* The seed customer is intentionally left behind in this isolated test database because the
               live schema includes customer identity / trigger logic that is not compatible with a
               direct delete here. The contract-cleanup logic already removes the customer-facing order,
               receipt, and payment artifacts that are required to maintain integrity. */", connection);
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
        command.Parameters.AddWithValue("@cashAccountId", seed.CashAccountId);
        command.Parameters.AddWithValue("@reversalDefinitionExists", seed.ReversalDefinitionExisted);
        command.Parameters.AddWithValue("@reversalDefinitionWasEnabled", seed.ReversalDefinitionWasEnabled);
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
            DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId IN (
                SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                    SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId));
            DELETE FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (
                SELECT InventoryReceiptLineId FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (
                    SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (
                        SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                            SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId))));
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN (
                SELECT InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (
                    SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                        SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId)));
            DELETE FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN (
                SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                    SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId));
            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (
                SELECT i.InventoryItemId
                FROM dbo.InventoryItems i
                INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemId
                INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                WHERE gr.SupplierId = @supplierId);
            DELETE FROM dbo.InventoryItems WHERE InventoryItemId IN (
                SELECT i.InventoryItemId
                FROM dbo.InventoryItems i
                INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemId
                INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                WHERE gr.SupplierId = @supplierId);
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN (
                SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId);
            DELETE FROM dbo.GoodsReceipts WHERE SupplierId = @supplierId;
            DELETE FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId IN (
                SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE SupplierId = @supplierId);
            DELETE FROM dbo.PurchaseOrders WHERE SupplierId = @supplierId;
            DELETE FROM dbo.Suppliers WHERE SupplierId = @supplierId;", connection);
        command.Parameters.AddWithValue("@supplierId", support.SupplierId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupOfficialFabricBatchAsync(string connectionString, string invoiceNumber, string? fabricCode = null)
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
            UPDATE dbo.AccountingEvents
            SET SourceType = NULL,
                SourceId = NULL,
                OriginalAccountingEventId = NULL,
                PaymentId = NULL,
                OrderId = NULL,
                InventoryReceiptPostingId = NULL,
                FabricConsumptionSourceId = NULL,
                ProductionMaterialConsumptionId = NULL
            WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            UPDATE dbo.AccountingEvents
            SET SourceType = NULL,
                SourceId = NULL,
                OriginalAccountingEventId = NULL,
                PaymentId = NULL,
                OrderId = NULL,
                InventoryReceiptPostingId = NULL,
                FabricConsumptionSourceId = NULL,
                ProductionMaterialConsumptionId = NULL
            WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.JournalEntryLines WHERE JournalEntryId IN (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds));
            DELETE FROM dbo.JournalEntries WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.FinancialTransactions WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.AccountingEvents WHERE AccountingEventId IN (SELECT AccountingEventId FROM @eventIds);
            DELETE FROM dbo.InventoryTransactions WHERE TransactionID IN (SELECT TransactionId FROM @transactionIds);
            DELETE FROM dbo.GoodsReceiptItemStorageAllocations WHERE GoodsReceiptItemId IN (SELECT GoodsReceiptItemId FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@receiptId);
            DELETE FROM dbo.FabricRolls WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptLineId IN (SELECT InventoryReceiptLineId FROM @lineIds);
            DELETE FROM dbo.InventoryReceiptPostings WHERE InventoryReceiptPostingId IN (SELECT InventoryReceiptPostingId FROM @postingIds);
            DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN (
                SELECT i.InventoryItemId
                FROM dbo.InventoryItems i
                INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemId
                INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                WHERE gr.ReceiptNumber = @receiptNumber);
            DELETE FROM dbo.InventoryItems WHERE InventoryItemId IN (
                SELECT i.InventoryItemId
                FROM dbo.InventoryItems i
                INNER JOIN dbo.InventoryReceiptLines rl ON rl.InventoryItemId = i.InventoryItemId
                INNER JOIN dbo.InventoryReceiptPostings rp ON rp.InventoryReceiptPostingId = rl.InventoryReceiptPostingId
                INNER JOIN dbo.GoodsReceiptItems gri ON gri.GoodsReceiptItemId = rp.GoodsReceiptItemId
                INNER JOIN dbo.GoodsReceipts gr ON gr.GoodsReceiptId = gri.GoodsReceiptId
                WHERE gr.ReceiptNumber = @receiptNumber);
            DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@receiptId;
            DELETE FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@receiptId;
            DELETE FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId=@purchaseOrderId;
            DELETE FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@purchaseOrderId;", connection);
        command.Parameters.AddWithValue("@receiptNumber", $"FAB-{invoiceNumber}");
        if (fabricCode is not null)
        {
            command.Parameters.AddWithValue("@fabricCode", fabricCode);
        }
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
        var requiredAccounts = new[]
        {
            new { Code = "1000", Name = "Cash", Type = "Asset" },
            new { Code = "1101", Name = "Fabric Inventory", Type = "Asset" },
            new { Code = "1102", Name = "Consumables Inventory", Type = "Asset" },
            new { Code = "1130", Name = "Work In Progress", Type = "Asset" },
            new { Code = "1160", Name = "Customer Advances", Type = "Asset" },
            new { Code = "1200", Name = "Accounts Receivable", Type = "Asset" },
            new { Code = "2100", Name = "Accounts Payable", Type = "Liability" },
            new { Code = "4200", Name = "Sales Revenue", Type = "Revenue" },
            new { Code = "5300", Name = "Operating Consumables Expense", Type = "Expense" }
        };

        foreach (var account in requiredAccounts)
        {
            await using var command = new SqlCommand(@"
                IF EXISTS (
                    SELECT 1
                    FROM dbo.LedgerAccounts
                    WHERE AccountCode = @code)
                BEGIN
                    UPDATE dbo.LedgerAccounts
                    SET AccountName = @name,
                        AccountType = @type,
                        IsActive = 1,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE AccountCode = @code;
                    RETURN;
                END;

                DECLARE @cashControlLedgerId int = (SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'1000' AND IsActive = 1 ORDER BY LedgerAccountId);
                IF @code = N'1000' AND @cashControlLedgerId IS NOT NULL
                BEGIN
                    UPDATE dbo.LedgerAccounts
                    SET AccountCode = @code,
                        AccountName = @name,
                        AccountType = @type,
                        IsActive = 1,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE LedgerAccountId = @cashControlLedgerId;
                    RETURN;
                END;

                IF @code = N'1000' AND @cashControlLedgerId IS NULL
                BEGIN
                    SET IDENTITY_INSERT dbo.LedgerAccounts ON;
                    INSERT INTO dbo.LedgerAccounts (LedgerAccountId, AccountCode, AccountName, AccountType, IsActive, CreatedAt, UpdatedAt)
                    VALUES ((SELECT ISNULL(MAX(LedgerAccountId),0) + 1 FROM dbo.LedgerAccounts), @code, @name, @type, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                    SET IDENTITY_INSERT dbo.LedgerAccounts OFF;
                    RETURN;
                END;

                INSERT INTO dbo.LedgerAccounts (AccountCode, AccountName, AccountType, IsActive, CreatedAt, UpdatedAt)
                VALUES (@code, @name, @type, 1, SYSUTCDATETIME(), SYSUTCDATETIME());", connection, transaction);
            command.Parameters.AddWithValue("@code", account.Code);
            command.Parameters.AddWithValue("@name", account.Name);
            command.Parameters.AddWithValue("@type", account.Type);
            await command.ExecuteNonQueryAsync();
        }

        await using var verification = new SqlCommand(@"
            IF (SELECT COUNT(*) FROM dbo.LedgerAccounts WHERE IsActive = 1 AND AccountCode IN (N'1000',N'1101',N'1102',N'1130',N'1160',N'1200',N'2100',N'4200',N'5300')) <> 9
                THROW 51002, N'Fabric integration fixture requires the nine reference ledger accounts.', 1;

            DECLARE @cashControlLedgerId int = (SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'1000' AND IsActive = 1 ORDER BY LedgerAccountId);
            IF @cashControlLedgerId IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CashAccounts ca JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = ca.LedgerControlAccountId WHERE ca.IsActive = 1 AND la.AccountCode = N'1000' AND la.LedgerAccountId <> @cashControlLedgerId)
                UPDATE dbo.CashAccounts SET LedgerControlAccountId = @cashControlLedgerId WHERE IsActive = 1 AND CashAccountId = (SELECT TOP (1) CashAccountId FROM dbo.CashAccounts WHERE IsActive = 1 ORDER BY CashAccountId);", connection, transaction);
        await verification.ExecuteNonQueryAsync();
    }

    private static async Task SeedInventoryCatalogsAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var classCommand = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryClassCatalog WHERE InventoryClassId = 1)
                INSERT INTO dbo.InventoryClassCatalog (InventoryClassId, Code, NameAr)
                VALUES (1, N'FABRIC', N'قماش');

            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryClassCatalog WHERE InventoryClassId = 2)
                INSERT INTO dbo.InventoryClassCatalog (InventoryClassId, Code, NameAr)
                VALUES (2, N'CONSUMABLE', N'مادة مستهلكة');", connection, transaction);
        await classCommand.ExecuteNonQueryAsync();

        await using var unitCommand = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 1)
                INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
                VALUES (1, N'YARD', N'ياردة', 36.000000);

            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 2)
                INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
                VALUES (2, N'INCH', N'بوصة', 1.000000);

            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryUnitCatalog WHERE UnitId = 3)
                INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
                VALUES (3, N'PIECE', N'قطعة', NULL);", connection, transaction);
        await unitCommand.ExecuteNonQueryAsync();

        await using var verification = new SqlCommand(@"
            IF (SELECT COUNT(*) FROM dbo.InventoryClassCatalog) < 2 OR (SELECT COUNT(*) FROM dbo.InventoryUnitCatalog) < 3
                THROW 51003, N'Fabric integration fixture requires the inventory class and unit catalogs.', 1;", connection, transaction);
        await verification.ExecuteNonQueryAsync();
    }

    private static async Task SeedPricingProductTypesAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM dbo.PricingProductTypes WHERE Code = N'FAB-TEST-DEFAULT')
            BEGIN
                INSERT INTO dbo.PricingProductTypes (Code, NameAr, NameEn, Category, Scope, IsActive, CreatedAt, UpdatedAt)
                VALUES (N'FAB-TEST-DEFAULT', N'قماش تجريبي', N'Fabric Test Default', N'Garment', N'Tailoring', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            END;", connection, transaction);
        await command.ExecuteNonQueryAsync();

        await using var verification = new SqlCommand(@"
            IF (SELECT COUNT(*) FROM dbo.PricingProductTypes WHERE IsActive = 1) < 1
                THROW 51004, N'Fabric integration fixture requires at least one active pricing product type for order creation.', 1;", connection, transaction);
        await verification.ExecuteNonQueryAsync();
    }

    private static async Task<int> SeedCashAccountAsync(SqlConnection connection, SqlTransaction transaction, string suffix)
    {
        const string sql = @"
            DECLARE @generalCashAccountId int = TRY_CONVERT(int, (SELECT TOP (1) SettingValue FROM dbo.System_Settings WITH (UPDLOCK, HOLDLOCK) WHERE SettingName = N'GeneralCashAccountId' ORDER BY SettingId));
            IF @generalCashAccountId IS NULL OR @generalCashAccountId <= 0
                THROW 51005, N'Fabric integration fixture requires GeneralCashAccountId to be configured to a valid cash account.', 1;

            DECLARE @cashControlLedgerId int = (SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountCode = N'1000' AND IsActive = 1 ORDER BY LedgerAccountId);
            IF @cashControlLedgerId IS NULL
                THROW 51005, N'Fabric integration fixture requires the cash control ledger account 1000 to exist and be active.', 1;

            DECLARE @cashAccountId int = (
                SELECT TOP (1) ca.CashAccountId
                FROM dbo.CashAccounts ca WITH (UPDLOCK, HOLDLOCK)
                WHERE ca.CashAccountId = @generalCashAccountId
                  AND ca.IsActive = 1
                  AND ca.CashAccountType = 1
                  AND ca.AllowsReceipts = 1
                  AND ca.AllowsDisbursements = 1
                  AND ca.CurrencyCode = N'YER'
                  AND ca.LedgerControlAccountId = @cashControlLedgerId
                ORDER BY ca.CashAccountId
            );

            IF @cashAccountId IS NULL
                THROW 51005, N'Fabric integration fixture requires the active GeneralCashAccountId to point to an eligible cash account.', 1;

            SELECT @cashAccountId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@suffix", suffix);
        var result = await command.ExecuteScalarAsync();
        var cashAccountId = Convert.ToInt32(result);
        if (cashAccountId <= 0)
            throw new InvalidOperationException("Fabric integration fixture requires the current GeneralCashAccountId to resolve to a valid active cash account.");

        return cashAccountId;
    }

    private static async Task<bool> ReadBoolAsync(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync();
        return result is bool b ? b : Convert.ToBoolean(result ?? false);
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

    private static async Task<List<string>> QueryStringListAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        var values = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var value = reader[0];
            if (value is not DBNull and not null)
            {
                values.Add(Convert.ToString(value)!);
            }
        }

        return values;
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

    private static async Task<int> ReadIntAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
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

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, long value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
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

    private static async Task<long> ReadLongAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ReadStringAsync(string connectionString, string sql, string value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", value);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? null : Convert.ToString(result);
    }

    private static async Task<string?> ReadStringAsync(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? null : Convert.ToString(result);
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
        public int CashAccountId { get; init; }
        public bool ReversalDefinitionExisted { get; init; }
        public bool ReversalDefinitionWasEnabled { get; init; }
    }

    private sealed record OfficialBatchSupportSeed(int SupplierId);
}

