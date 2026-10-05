using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ToolIssuanceContractsTests
{
    [Fact]
    public void ToolOperationalIssueAccountingType_IsDefined()
    {
        Assert.Equal((byte)14, (byte)AccountingEventType.ToolOperationalIssue);
    }

    [Fact]
    public void ToolOperationalReversalAccountingType_IsDefined()
    {
        Assert.Equal((byte)15, (byte)AccountingEventType.ToolOperationalReversal);
    }

    [Fact]
    public void ToolIssuanceDtos_AreAvailable()
    {
        var issue = new CreateToolOperationalIssueDto
        {
            InventoryItemId = 1,
            Quantity = 2m,
            OfficialUnitCost = 50m,
            OperationalReason = "Issue for project",
            Notes = "Daily tool issue"
        };

        var custody = new CreateToolCustodyIssueDto
        {
            InventoryItemId = 1,
            Quantity = 2m,
            OfficialUnitCost = 50m,
            BeneficiaryName = "Ali",
            DestinationType = "Project",
            DestinationName = "Site A",
            LoanReason = "Use"
        };

        Assert.Equal(1, issue.InventoryItemId);
        Assert.Equal("Ali", custody.BeneficiaryName);
        Assert.Equal("Daily tool issue", issue.Notes);
    }

    [Fact]
    public async Task OperationalIssue_UsesInventoryStockCost_AndPersistsOptionalNotes()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Operational stock cost");
        var sourceOperationId = Guid.NewGuid();

        var issuance = await repository.IssueToolOperationalAsync(new CreateToolOperationalIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 999m,
            OperationalReason = "Project dispatch",
            Notes = "Saved in transaction notes",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        }, CancellationToken.None);

        Assert.NotNull(issuance);
        Assert.Equal(25.5m, issuance!.OfficialUnitCost);
        Assert.Equal(25.5m, issuance.PostingAmount / issuance.Quantity);
    }

    [Fact]
    public async Task OperationalIssue_WithSameSourceOperationId_ReturnsOriginalRecord()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Operational Idempotency");
        var sourceOperationId = Guid.NewGuid();
        var request = new CreateToolOperationalIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            OperationalReason = "Idempotency regression test",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        };

        var first = await repository.IssueToolOperationalAsync(request, CancellationToken.None);
        Assert.NotNull(first);

        var second = await repository.IssueToolOperationalAsync(request, CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(first!.ToolIssuanceId, second!.ToolIssuanceId);
        Assert.Equal(first.InventoryTransactionId, second.InventoryTransactionId);
        Assert.Equal(first.AccountingEventId, second.AccountingEventId);
        Assert.Equal(first.Status, second.Status);
    }

    [Fact]
    public async Task OperationalIssue_WithConflictingPayload_ThrowsSourceOperationConflict()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Operational Conflict");
        var sourceOperationId = Guid.NewGuid();

        await repository.IssueToolOperationalAsync(new CreateToolOperationalIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            OperationalReason = "Original reason",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        }, CancellationToken.None);

        var conflict = new CreateToolOperationalIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            OperationalReason = "Different reason",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.IssueToolOperationalAsync(conflict, CancellationToken.None));
        Assert.Contains("SOURCE_OPERATION_CONFLICT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustodyIssue_WithSameSourceOperationId_ReturnsOriginalRecord()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Custody Idempotency");
        var sourceOperationId = Guid.NewGuid();
        var request = new CreateToolCustodyIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            BeneficiaryName = "Idempotency fixture",
            DestinationType = "Department",
            DestinationName = "Audit Unit",
            LoanReason = "Test custody flow",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        };

        var first = await repository.IssueToolCustodyAsync(request, CancellationToken.None);
        Assert.NotNull(first);

        var second = await repository.IssueToolCustodyAsync(request, CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(first!.ToolIssuanceId, second!.ToolIssuanceId);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.InventoryTransactionId, second.InventoryTransactionId);
    }

    [Fact]
    public async Task ReturnCustody_WithSameSourceOperationId_ReturnsOriginalRecord()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Return Idempotency");
        var sourceOperationId = Guid.NewGuid();
        var issuance = await repository.IssueToolCustodyAsync(new CreateToolCustodyIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            BeneficiaryName = "Return fixture",
            DestinationType = "Department",
            DestinationName = "Audit Unit",
            LoanReason = "Return idempotency",
            ConfirmedByUserId = 1,
            SourceOperationId = sourceOperationId
        }, CancellationToken.None);
        Assert.NotNull(issuance);

        var request = new ReturnToolCustodyDto
        {
            ToolIssuanceId = issuance!.ToolIssuanceId,
            ReturnedQuantity = 0.5m,
            ReturnNotes = "Half return",
            ConfirmedByUserId = 1,
            SourceOperationId = Guid.NewGuid()
        };

        var first = await repository.ReturnToolCustodyAsync(request, CancellationToken.None);
        Assert.NotNull(first);

        var replay = await repository.ReturnToolCustodyAsync(new ReturnToolCustodyDto
        {
            ToolIssuanceId = issuance.ToolIssuanceId,
            ReturnedQuantity = 0.5m,
            ReturnNotes = "Half return",
            ConfirmedByUserId = 1,
            SourceOperationId = request.SourceOperationId
        }, CancellationToken.None);

        Assert.NotNull(replay);
        Assert.Equal(first!.ToolIssuanceId, replay!.ToolIssuanceId);
        Assert.Equal(first.Status, replay.Status);
    }

    [Fact]
    public async Task ReverseOperationalIssue_WithSameSourceOperationId_ReturnsOriginalRecord()
    {
        var repository = CreateRepository();
        var tool = await CreateUsedToolAsync(repository, "Reversal Idempotency");
        var opSource = Guid.NewGuid();
        var issuance = await repository.IssueToolOperationalAsync(new CreateToolOperationalIssueDto
        {
            InventoryItemId = tool.InventoryItemId,
            Quantity = 1m,
            OfficialUnitCost = 25.5m,
            OperationalReason = "Reversal idempotency fixture",
            ConfirmedByUserId = 1,
            SourceOperationId = opSource
        }, CancellationToken.None);

        Assert.NotNull(issuance);
        var request = new ReverseToolOperationalIssueDto
        {
            ToolIssuanceId = issuance!.ToolIssuanceId,
            ReversalReason = "Correcting fixture",
            Notes = "Live idempotency check",
            ReversedBy = 1,
            SourceOperationId = Guid.NewGuid()
        };

        var first = await repository.ReverseToolOperationalIssueAsync(request, CancellationToken.None);
        Assert.NotNull(first);

        var replay = await repository.ReverseToolOperationalIssueAsync(new ReverseToolOperationalIssueDto
        {
            ToolIssuanceId = issuance.ToolIssuanceId,
            ReversalReason = "Correcting fixture",
            Notes = "Live idempotency check",
            ReversedBy = 1,
            SourceOperationId = request.SourceOperationId
        }, CancellationToken.None);

        Assert.NotNull(replay);
        Assert.Equal(first!.ToolIssuanceId, replay!.ToolIssuanceId);
        Assert.Equal(first.Status, replay.Status);
    }

    private static InventoryRepository CreateRepository()
    {
        var connectionString = GetConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new InventoryRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static async Task<InventoryItemDto> CreateUsedToolAsync(InventoryRepository repository, string name)
    {
        var created = await repository.UpsertToolItemAsync(new CreateToolItemDto
        {
            ProductName = $"{name}-{Guid.NewGuid():N}",
            ProductType = "أداة",
            Unit = "Piece",
            Quantity = 10m,
            UnitPrice = 25.5m,
            Notes = "Fixture for idempotency regression test"
        }, CancellationToken.None);

        Assert.NotNull(created);

        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var transaction = connection.BeginTransaction();

        var receiptId = await InsertUsedToolReceiptAsync(connection, transaction, created!.InventoryItemId);
        var receiptItemId = await InsertUsedToolReceiptItemAsync(connection, transaction, receiptId, created.InventoryItemId, 10m, 25.5m);
        var operationId = Guid.NewGuid();

        await using (var cmd = new SqlCommand(@"
            INSERT INTO dbo.GoodsReceiptItemStorageAllocations
                (GoodsReceiptItemId, ItemType, StorageOperationId, StoredQuantity, InventoryItemId, InventoryTransactionId, AccountingEventId, InventoryReceiptPostingId, ImportedReadyMadeInventoryReceiptId, CreatedAt)
            VALUES
                (@goodsReceiptItemId, N'UsedTool', @storageOperationId, @storedQuantity, @inventoryItemId, NULL, NULL, NULL, NULL, @createdAt)", connection, transaction))
        {
            cmd.Parameters.AddWithValue("@goodsReceiptItemId", receiptItemId);
            cmd.Parameters.Add("@storageOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = operationId;
            cmd.Parameters.AddWithValue("@storedQuantity", 10m);
            cmd.Parameters.AddWithValue("@inventoryItemId", created.InventoryItemId);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await transaction.CommitAsync(CancellationToken.None);
        return created;
    }

    private static async Task<int> InsertUsedToolReceiptAsync(SqlConnection connection, SqlTransaction transaction, int inventoryItemId)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.GoodsReceipts
                (SupplierId, PurchaseOrderId, ReceiptNumber, ReceiptDate, Notes, CreatedAt, WarehouseId, SourceOperationId, ReceiptStatus, OriginalGoodsReceiptId)
            OUTPUT INSERTED.GoodsReceiptId
            VALUES
                (@supplierId, NULL, @receiptNumber, @receiptDate, @notes, @createdAt, NULL, @sourceOperationId, N'Confirmed', NULL)", connection, transaction);

        var receiptNumber = $"TOOLFIX-{Guid.NewGuid():N}";
        command.Parameters.AddWithValue("@supplierId", 235);
        command.Parameters.AddWithValue("@receiptNumber", receiptNumber);
        command.Parameters.AddWithValue("@receiptDate", DateTime.UtcNow);
        command.Parameters.AddWithValue("@notes", "Fixture storage allocation for used tool");
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();

        var result = await command.ExecuteScalarAsync(CancellationToken.None);
        return result is int receiptId ? receiptId : Convert.ToInt32(result ?? 0);
    }

    private static async Task<int> InsertUsedToolReceiptItemAsync(SqlConnection connection, SqlTransaction transaction, int receiptId, int inventoryItemId, decimal quantity, decimal unitCost)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.GoodsReceiptItems
                (GoodsReceiptId, ItemName, ReceivedQuantity, UnitCost, LineTotal, InventoryItemId, SourceOperationId, OriginalGoodsReceiptItemId, LineStatus, SupplierInvoiceLineId, ItemType, RollCount, ProductType, UnitCode, ItemCount, ReceivedItemCount)
            OUTPUT INSERTED.GoodsReceiptItemId
            VALUES
                (@goodsReceiptId, @itemName, @receivedQuantity, @unitCost, @lineTotal, @inventoryItemId, @sourceOperationId, NULL, N'Confirmed', NULL, N'UsedTool', NULL, N'أداة', N'Piece', @quantity, @quantity)", connection, transaction);

        command.Parameters.AddWithValue("@goodsReceiptId", receiptId);
        command.Parameters.AddWithValue("@itemName", "Fixture used tool");
        command.Parameters.AddWithValue("@receivedQuantity", quantity);
        command.Parameters.AddWithValue("@unitCost", unitCost);
        command.Parameters.AddWithValue("@lineTotal", quantity * unitCost);
        command.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.Add("@sourceOperationId", System.Data.SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();

        var result = await command.ExecuteScalarAsync(CancellationToken.None);
        return result is int itemId ? itemId : Convert.ToInt32(result ?? 0);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }
}
