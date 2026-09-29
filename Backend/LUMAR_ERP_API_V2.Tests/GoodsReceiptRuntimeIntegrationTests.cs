using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class GoodsReceiptRuntimeIntegrationTests
{
    [Fact]
    public async Task ReceiptRuntime_MatchesPostsRetriesAndSafelyReverses()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var repository = CreateRepository(connectionString);
        var operation = Guid.NewGuid();
        var receiptNumber = $"ES6-GR-{seed.Suffix}";
        try
        {
            var request = new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = receiptNumber, ReceiptDate = DateTime.UtcNow, CreatedBy = "ES6-Test", SourceOperationId = operation,
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId }]
            };
            var result = await repository.CreateGoodsReceiptAsync(request, CancellationToken.None);
            Assert.False(result.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@id AND ReceiptStatus=N'Posted'", result.GoodsReceiptId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @value AND WarehouseId=(SELECT WarehouseId FROM dbo.GoodsReceipts WHERE ReceiptNumber=REPLACE(REPLACE(@value,N'GoodsReceipt:',N''),N':%',N''))", $"GoodsReceipt:{receiptNumber}:%"));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptDifferences d JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=d.GoodsReceiptItemId WHERE i.GoodsReceiptId=@id", result.GoodsReceiptId));

            var retry = await repository.CreateGoodsReceiptAsync(request, CancellationToken.None);
            Assert.True(retry.IsExisting);
            Assert.Equal(result.GoodsReceiptId, retry.GoodsReceiptId);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE SourceOperationId=@operation", operation));

            var reversal = await repository.ReverseGoodsReceiptAsync(new ReverseGoodsReceiptDto { GoodsReceiptId = result.GoodsReceiptId, SourceOperationId = Guid.NewGuid(), Reason = "Integration test", ReversedBy = "ES6-Test" }, CancellationToken.None);
            Assert.False(reversal.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@id AND ReceiptStatus=N'Reversed'", result.GoodsReceiptId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptReversalLines WHERE GoodsReceiptReversalId=@id", reversal.GoodsReceiptReversalId));
            Assert.Equal(1m, await DecimalAsync(connectionString, "SELECT CurrentQuantity FROM dbo.InventoryItems WHERE InventoryItemID=@id", seed.InventoryItemId));

            var direct = await repository.CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, WarehouseId = seed.WarehouseId, ReceiptNumber = $"ES6-DIRECT-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 2m, UnitCost = 15m }]
            }, CancellationToken.None);
            Assert.False(direct.IsExisting);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE GoodsReceiptId=@id AND PurchaseOrderId IS NULL", direct.GoodsReceiptId));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_RecordsPurchaseOrderAndInvoiceQuantityAndCostDifferences()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            var receipt = await CreateRepository(connectionString).CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = $"ES6-DIFF-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 5m, UnitCost = 13m, SupplierInvoiceLineId = seed.InvoiceLineId }]
            }, CancellationToken.None);

            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptDifferences d JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=d.GoodsReceiptItemId WHERE i.GoodsReceiptId=@id AND d.DifferenceType=N'PurchaseOrderQuantityVariance'", receipt.GoodsReceiptId));
            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptDifferences d JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=d.GoodsReceiptItemId WHERE i.GoodsReceiptId=@id AND d.DifferenceType IN(N'PurchaseOrderCostVariance',N'SupplierInvoiceVariance')", receipt.GoodsReceiptId));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_ConcurrentSameOperationCreatesOneReceiptAndOneInventoryPosting()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var operation = Guid.NewGuid();
        var receiptNumber = $"ES6-CONCURRENT-{seed.Suffix}";
        try
        {
            CreateGoodsReceiptDto Request() => new()
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = receiptNumber, CreatedBy = "ES6-Test", SourceOperationId = operation,
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId }]
            };

            var first = CreateRepository(connectionString).CreateGoodsReceiptAsync(Request(), CancellationToken.None);
            var second = CreateRepository(connectionString).CreateGoodsReceiptAsync(Request(), CancellationToken.None);
            var results = await Task.WhenAll(first, second);

            Assert.All(results, result => Assert.Equal(results[0].GoodsReceiptId, result.GoodsReceiptId));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE SourceOperationId=@operation", operation));
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @value", $"GoodsReceipt:{receiptNumber}:%"));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_PostingFailureRollsBackHeaderAndPreviouslyPostedItems()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        var operation = Guid.NewGuid();
        var receiptNumber = $"ES6-ROLLBACK-{seed.Suffix}";
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRepository(connectionString).CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = receiptNumber, CreatedBy = "ES6-Test", SourceOperationId = operation,
                Items =
                [
                    new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId },
                    new CreateGoodsReceiptItemDto { InventoryItemId = int.MaxValue, Quantity = 1m, UnitCost = 1m }
                ]
            }, CancellationToken.None));

            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE SourceOperationId=@operation", operation));
            Assert.Equal(0, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @value", $"GoodsReceipt:{receiptNumber}:%"));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_AllowsMultiplePartialReceiptsForOneSupplierInvoiceLine()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            async Task CreatePartialAsync(string suffix) => await CreateRepository(connectionString).CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = $"ES6-PARTIAL-{suffix}-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 2m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId }]
            }, CancellationToken.None);

            await CreatePartialAsync("A");
            await CreatePartialAsync("B");

            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItems WHERE SupplierInvoiceLineId=@id", seed.InvoiceLineId));
            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @value", $"GoodsReceipt:ES6-PARTIAL-%-{seed.Suffix}:Item:%"));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_OneReceiptCanMatchMultipleSupplierInvoiceLines()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            var result = await CreateRepository(connectionString).CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = $"ES6-MULTI-LINE-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items =
                [
                    new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId },
                    new CreateGoodsReceiptItemDto { InventoryItemId = seed.SecondInventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.SecondInvoiceLineId }
                ]
            }, CancellationToken.None);

            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId=@id AND SupplierInvoiceLineId IS NOT NULL", result.GoodsReceiptId));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_MultipleReceiptsCanMatchMultipleSupplierInvoiceLines()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            async Task CreatePartialAsync(string label) => await CreateRepository(connectionString).CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = $"ES6-MULTI-{label}-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items =
                [
                    new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 2m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId },
                    new CreateGoodsReceiptItemDto { InventoryItemId = seed.SecondInventoryItemId, Quantity = 2m, UnitCost = 12m, SupplierInvoiceLineId = seed.SecondInvoiceLineId }
                ]
            }, CancellationToken.None);

            await CreatePartialAsync("A");
            await CreatePartialAsync("B");

            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItems WHERE SupplierInvoiceLineId=@id", seed.InvoiceLineId));
            Assert.Equal(2, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptItems WHERE SupplierInvoiceLineId=@id", seed.SecondInvoiceLineId));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    [Fact]
    public async Task ReceiptRuntime_ConcurrentReversalCreatesOnlyOneReversal()
    {
        var connectionString = GetConnectionString();
        var seed = await SeedAsync(connectionString);
        try
        {
            var repository = CreateRepository(connectionString);
            var receipt = await repository.CreateGoodsReceiptAsync(new CreateGoodsReceiptDto
            {
                SupplierId = seed.SupplierId, PurchaseOrderId = seed.PurchaseOrderId, WarehouseId = seed.WarehouseId,
                ReceiptNumber = $"ES6-REV-CONCURRENT-{seed.Suffix}", CreatedBy = "ES6-Test", SourceOperationId = Guid.NewGuid(),
                Items = [new CreateGoodsReceiptItemDto { InventoryItemId = seed.InventoryItemId, Quantity = 4m, UnitCost = 12m, SupplierInvoiceLineId = seed.InvoiceLineId }]
            }, CancellationToken.None);

            async Task<GoodsReceiptReversalResult?> AttemptAsync()
            {
                try
                {
                    return await CreateRepository(connectionString).ReverseGoodsReceiptAsync(new ReverseGoodsReceiptDto
                    {
                        GoodsReceiptId = receipt.GoodsReceiptId, SourceOperationId = Guid.NewGuid(), Reason = "Concurrent test", ReversedBy = "ES6-Test"
                    }, CancellationToken.None);
                }
                catch (InvalidOperationException) { return null; }
            }

            var results = await Task.WhenAll(AttemptAsync(), AttemptAsync());
            Assert.Single(results, result => result is not null);
            Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM dbo.GoodsReceiptReversals WHERE OriginalGoodsReceiptId=@id", receipt.GoodsReceiptId));
        }
        finally { await CleanupAsync(connectionString, seed); }
    }

    private static async Task<Seed> SeedAsync(string cs)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var t = (SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            var reversalWasEnabled = await ScalarAsync(c, t, "SELECT CONVERT(int,IsEnabled) FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=33");
            await ExecuteAsync(c, t, "UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1,IsBusinessRuntimeEnabled=0 WHERE AccountingEventType=33");
            var supplier = await ScalarAsync(c, t, "INSERT dbo.Suppliers(SupplierCode,SupplierName,IsActive,CreatedAt) OUTPUT INSERTED.SupplierId VALUES(@code,N'ES6 supplier',1,SYSUTCDATETIME())", ("@code", $"ES6-S-{suffix}"));
            var warehouse = await ScalarAsync(c, t, "INSERT dbo.Warehouses(WarehouseCode,WarehouseName,IsActive) OUTPUT INSERTED.WarehouseId VALUES(@code,N'ES6 warehouse',1)", ("@code", $"ES6-W-{suffix}"));
            var item = await ScalarAsync(c, t, @"INSERT dbo.InventoryItems(ItemCode,ItemName,Category,Unit,CurrentQuantity,AvailableQuantity,ReservedQuantity,IsActive,CreatedAt,UpdatedAt) OUTPUT INSERTED.InventoryItemID VALUES(@code,N'ES6 item',N'Foundation',N'Piece',1,1,0,1,SYSUTCDATETIME(),SYSUTCDATETIME());", ("@code", $"ES6-I-{suffix}"));
            await ExecuteAsync(c, t, "INSERT dbo.InventoryItemFoundation(InventoryItemId,InventoryClassId,UnitId,CurrencyCode,OriginalQuantity,AvailableQuantity,ConsumedQuantity,OfficialUnitCost,OperationalValue,CreatedAt,UpdatedAt) VALUES(@item,2,3,'YER',1,1,0,12,12,SYSUTCDATETIME(),SYSUTCDATETIME())", ("@item", item));
            var secondItem = await ScalarAsync(c, t, @"INSERT dbo.InventoryItems(ItemCode,ItemName,Category,Unit,CurrentQuantity,AvailableQuantity,ReservedQuantity,IsActive,CreatedAt,UpdatedAt) OUTPUT INSERTED.InventoryItemID VALUES(@code,N'ES6 second item',N'Foundation',N'Piece',1,1,0,1,SYSUTCDATETIME(),SYSUTCDATETIME());", ("@code", $"ES6-I2-{suffix}"));
            await ExecuteAsync(c, t, "INSERT dbo.InventoryItemFoundation(InventoryItemId,InventoryClassId,UnitId,CurrencyCode,OriginalQuantity,AvailableQuantity,ConsumedQuantity,OfficialUnitCost,OperationalValue,CreatedAt,UpdatedAt) VALUES(@item,2,3,'YER',1,1,0,12,12,SYSUTCDATETIME(),SYSUTCDATETIME())", ("@item", secondItem));
            var po = await ScalarAsync(c, t, "INSERT dbo.PurchaseOrders(PurchaseOrderNumber,SupplierId,OrderDate,Status,TotalAmount,CreatedAt) OUTPUT INSERTED.PurchaseOrderId VALUES(@number,@supplier,SYSUTCDATETIME(),N'Open',48,SYSUTCDATETIME())", ("@number", $"ES6-PO-{suffix}"), ("@supplier", supplier));
            await ExecuteAsync(c, t, "INSERT dbo.PurchaseOrderItems(PurchaseOrderId,ItemName,Quantity,UnitCost,LineTotal) VALUES(@po,N'ES6 item',4,12,48)", ("@po", po));
            var invoice = await ScalarAsync(c, t, "INSERT dbo.SupplierInvoices(SupplierId,PurchaseOrderId,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,AmountPaid,Status,Notes,CreatedAt) OUTPUT INSERTED.SupplierInvoiceId VALUES(@supplier,@po,@number,SYSUTCDATETIME(),SYSUTCDATETIME(),48,0,N'Open',NULL,SYSUTCDATETIME())", ("@supplier", supplier), ("@po", po), ("@number", $"ES6-INV-{suffix}"));
            var invoiceLine = await ScalarAsync(c, t, "INSERT dbo.SupplierInvoiceLines(SupplierInvoiceId,InventoryItemId,Quantity,UnitCost,RollCount,SourceOperationId,Status,CreatedBy) OUTPUT INSERTED.SupplierInvoiceLineId VALUES(@invoice,@item,4,12,NULL,@operation,N'Posted',N'ES6-Test')", ("@invoice", invoice), ("@item", item), ("@operation", Guid.NewGuid()));
            var secondInvoiceLine = await ScalarAsync(c, t, "INSERT dbo.SupplierInvoiceLines(SupplierInvoiceId,InventoryItemId,Quantity,UnitCost,RollCount,SourceOperationId,Status,CreatedBy) OUTPUT INSERTED.SupplierInvoiceLineId VALUES(@invoice,@item,4,12,NULL,@operation,N'Posted',N'ES6-Test')", ("@invoice", invoice), ("@item", secondItem), ("@operation", Guid.NewGuid()));
            await t.CommitAsync(); return new Seed(suffix, supplier, warehouse, item, secondItem, po, invoice, invoiceLine, secondInvoiceLine, reversalWasEnabled == 1);
        }
        catch { await t.RollbackAsync(); throw; }
    }

    private static async Task CleanupAsync(string cs, Seed seed)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(@"
            DECLARE @eventIds TABLE(Id bigint PRIMARY KEY); INSERT @eventIds SELECT AccountingEventId FROM dbo.AccountingEvents WHERE InventoryReceiptPostingId IN(SELECT p.InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings p JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=p.GoodsReceiptItemId JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix);
            INSERT @eventIds SELECT AccountingEventId FROM dbo.GoodsReceiptReversalLines WHERE GoodsReceiptItemId IN(SELECT i.GoodsReceiptItemId FROM dbo.GoodsReceiptItems i JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix);
            UPDATE dbo.InventoryTransactions SET AccountingEventId=NULL WHERE ReferenceNumber LIKE @reference; UPDATE dbo.InventoryReceiptPostings SET AccountingEventId=NULL WHERE GoodsReceiptItemId IN(SELECT i.GoodsReceiptItemId FROM dbo.GoodsReceiptItems i JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix); UPDATE dbo.InventoryReceiptLines SET AccountingEventId=NULL,InventoryTransactionId=NULL WHERE InventoryReceiptPostingId IN(SELECT p.InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings p JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=p.GoodsReceiptItemId JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix);
            DELETE jel FROM dbo.JournalEntryLines jel JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId JOIN @eventIds e ON e.Id=je.AccountingEventId; DELETE je FROM dbo.JournalEntries je JOIN @eventIds e ON e.Id=je.AccountingEventId; DELETE ft FROM dbo.FinancialTransactions ft JOIN @eventIds e ON e.Id=ft.AccountingEventId; DELETE ae FROM dbo.AccountingEvents ae JOIN @eventIds e ON e.Id=ae.AccountingEventId;
            DELETE FROM dbo.GoodsReceiptReversalLines WHERE GoodsReceiptItemId IN(SELECT i.GoodsReceiptItemId FROM dbo.GoodsReceiptItems i JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix); DELETE FROM dbo.InventoryTransactions WHERE ReferenceNumber LIKE @reference; DELETE FROM dbo.InventoryReceiptLines WHERE InventoryReceiptPostingId IN(SELECT p.InventoryReceiptPostingId FROM dbo.InventoryReceiptPostings p JOIN dbo.GoodsReceiptItems i ON i.GoodsReceiptItemId=p.GoodsReceiptItemId JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix); DELETE FROM dbo.InventoryReceiptPostings WHERE GoodsReceiptItemId IN(SELECT i.GoodsReceiptItemId FROM dbo.GoodsReceiptItems i JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix); DELETE FROM dbo.GoodsReceiptDifferences WHERE GoodsReceiptItemId IN(SELECT i.GoodsReceiptItemId FROM dbo.GoodsReceiptItems i JOIN dbo.GoodsReceipts r ON r.GoodsReceiptId=i.GoodsReceiptId WHERE r.ReceiptNumber LIKE @prefix); DELETE FROM dbo.GoodsReceiptReversals WHERE OriginalGoodsReceiptId IN(SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE ReceiptNumber LIKE @prefix); DELETE FROM dbo.GoodsReceiptItems WHERE GoodsReceiptId IN(SELECT GoodsReceiptId FROM dbo.GoodsReceipts WHERE ReceiptNumber LIKE @prefix); DELETE FROM dbo.GoodsReceipts WHERE ReceiptNumber LIKE @prefix;
            DELETE FROM dbo.SupplierInvoiceLines WHERE SupplierInvoiceLineId IN(@line,@secondLine); DELETE FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoice; DELETE FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId=@po; DELETE FROM dbo.PurchaseOrders WHERE PurchaseOrderId=@po; DELETE FROM dbo.InventoryItemFoundation WHERE InventoryItemId IN(@item,@secondItem); DELETE FROM dbo.InventoryItems WHERE InventoryItemID IN(@item,@secondItem); DELETE FROM dbo.Warehouses WHERE WarehouseId=@warehouse; DELETE FROM dbo.Suppliers WHERE SupplierId=@supplier; UPDATE dbo.AccountingEventDefinitions SET IsEnabled=@reversalEnabled,IsBusinessRuntimeEnabled=0 WHERE AccountingEventType=33;", c);
        cmd.Parameters.AddWithValue("@prefix", $"ES6-%{seed.Suffix}"); cmd.Parameters.AddWithValue("@reference", $"GoodsReceipt:%{seed.Suffix}%"); cmd.Parameters.AddWithValue("@line", seed.InvoiceLineId); cmd.Parameters.AddWithValue("@secondLine", seed.SecondInvoiceLineId); cmd.Parameters.AddWithValue("@invoice", seed.InvoiceId); cmd.Parameters.AddWithValue("@po", seed.PurchaseOrderId); cmd.Parameters.AddWithValue("@item", seed.InventoryItemId); cmd.Parameters.AddWithValue("@secondItem", seed.SecondInventoryItemId); cmd.Parameters.AddWithValue("@warehouse", seed.WarehouseId); cmd.Parameters.AddWithValue("@supplier", seed.SupplierId); cmd.Parameters.AddWithValue("@reversalEnabled", seed.ReversalWasEnabled); await cmd.ExecuteNonQueryAsync();
    }

    private static InventoryRepository CreateRepository(string cs) { var options = Options.Create(new DatabaseOptions { ConnectionString = cs }); return new InventoryRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options)); }
    private static string GetConnectionString() { var b = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString") ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True"); if (!string.Equals(b.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase) && !string.Equals(b.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ES-6 tests require an approved ES validation database."); return b.ConnectionString; }
    private static async Task<int> CountAsync(string cs, string sql, object value) { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c); cmd.Parameters.AddWithValue(sql.Contains("@warehouse") ? "@warehouse" : sql.Contains("@operation") ? "@operation" : sql.Contains("@value") ? "@value" : "@id", value); return Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
    private static async Task<decimal> DecimalAsync(string cs, string sql, object value) { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c); cmd.Parameters.AddWithValue("@id", value); return Convert.ToDecimal(await cmd.ExecuteScalarAsync()); }
    private static async Task<int> ScalarAsync(SqlConnection c, SqlTransaction t, string sql, params (string Name, object Value)[] values) { await using var cmd = new SqlCommand(sql, c, t); foreach (var value in values) cmd.Parameters.AddWithValue(value.Name, value.Value); return Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
    private static async Task ExecuteAsync(SqlConnection c, SqlTransaction t, string sql, params (string Name, object Value)[] values) { await using var cmd = new SqlCommand(sql, c, t); foreach (var value in values) cmd.Parameters.AddWithValue(value.Name, value.Value); await cmd.ExecuteNonQueryAsync(); }
    private sealed record Seed(string Suffix, int SupplierId, int WarehouseId, int InventoryItemId, int SecondInventoryItemId, int PurchaseOrderId, int InvoiceId, int InvoiceLineId, int SecondInvoiceLineId, bool ReversalWasEnabled);
}