using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed partial class FabricConsumablesFoundationIntegrationTests
{
    [Fact]
    public async Task TailoringFabric_PartialReversal_PreservesOtherPiece_AndIsAtomicAndIdempotent()
    {
        var connectionString = GetConnectionString();
        Assert.Equal("LUMAR_TAILORING_EDIT_TEST_20261101", new SqlConnectionStringBuilder(connectionString).InitialCatalog);
        var seed = await SeedAsync(connectionString);
        var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
        int secondPieceId;
        InventoryFoundationPostingResultDto consumed;
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            secondPieceId = await InsertScalarAsync(connection, transaction, """
                UPDATE dbo.OrderItems SET Quantity=2 WHERE OrderItemID=@itemId;
                INSERT dbo.Pieces(OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate)
                OUTPUT INSERTED.PieceID VALUES(@itemId,@tracking,N'New',2,SYSUTCDATETIME());
                """, ("@itemId", seed.OrderItemId), ("@tracking", $"PARTIAL-{Guid.NewGuid():N}"));
            consumed = await InventoryRepository.ConsumeFabricCodeInventoryAsync(connection, transaction,
                new ConsumeFabricCodeInventoryDto
                {
                    FabricCode = seed.FabricItemCode,
                    OrderItemId = seed.OrderItemId,
                    QuantityInches = 72m,
                    SourceOperationId = Guid.NewGuid()
                }, CancellationToken.None);
            await transaction.CommitAsync();
        }

        var firstRequest = new ReverseFabricConsumptionDto(consumed.SourceRecordId, seed.PieceId,
            36m, Guid.NewGuid(), "Partial cancellation test", "Test");
        var first = await ReverseAsync(firstRequest);
        Assert.Equal(25.5m, first.OperationalAmount);
        Assert.Equal(5m, await AvailableAsync());
        Assert.Equal(127.5m, await ReadDecimalAsync(connectionString,
            "SELECT OperationalValue FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId));
        Assert.Equal(1, await CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.Pieces WHERE PieceID=@id AND PieceStatus=N'New'", secondPieceId));

        var replay = await ReverseAsync(firstRequest);
        Assert.True(replay.IsExisting);
        Assert.Equal(first.SourceRecordId, replay.SourceRecordId);
        Assert.Equal(5m, await AvailableAsync());

        var secondRequest = new ReverseFabricConsumptionDto(consumed.SourceRecordId, secondPieceId,
            36m, Guid.NewGuid(), "Second partial cancellation", "Test");
        await ReverseAsync(secondRequest, rollback: true);
        Assert.Equal(5m, await AvailableAsync());
        Assert.Equal(1, await ReversalCountAsync());

        await ReverseAsync(secondRequest);
        Assert.Equal(6m, await AvailableAsync());
        Assert.Equal(2, await ReversalCountAsync());
        Assert.Equal(51m, await ReadDecimalAsync(connectionString,
            "SELECT SUM(OperationalAmount) FROM dbo.FabricConsumptionPartialReversals WHERE FabricConsumptionSourceId=@id", consumed.SourceRecordId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReverseAsync(
            secondRequest with { SourceOperationId = Guid.NewGuid(), QuantityInches = 1m }));
        Assert.Equal(6m, await AvailableAsync());
        Assert.Equal(2, await ReversalCountAsync());

        Assert.Equal(0, await CountAsync(connectionString, """
            SELECT COUNT(*) FROM (
              SELECT reversal.AccountingEventId
              FROM dbo.FabricConsumptionPartialReversals reversal
              JOIN dbo.JournalEntries journal ON journal.AccountingEventId=reversal.AccountingEventId
              JOIN dbo.JournalEntryLines line ON line.JournalEntryId=journal.JournalEntryId
              WHERE reversal.FabricConsumptionSourceId=@id
              GROUP BY reversal.AccountingEventId
              HAVING SUM(line.DebitAmount)<>SUM(line.CreditAmount)
            ) unbalanced
            """, consumed.SourceRecordId));
        Assert.Equal(2m, await ReadDecimalAsync(connectionString,
            "SELECT ConsumedQuantity FROM dbo.FabricConsumptionSources WHERE FabricConsumptionSourceId=@id", consumed.SourceRecordId));

        Task<decimal> AvailableAsync() => ReadDecimalAsync(connectionString,
            "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id", receipt.InventoryItemId);
        Task<int> ReversalCountAsync() => CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.FabricConsumptionPartialReversals WHERE FabricConsumptionSourceId=@id", consumed.SourceRecordId);
        async Task<InventoryFoundationPostingResultDto> ReverseAsync(ReverseFabricConsumptionDto request, bool rollback = false)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var result = await InventoryRepository.ReverseFabricConsumptionInTransactionAsync(connection, transaction, request, CancellationToken.None);
            if (rollback) await transaction.RollbackAsync();
            else await transaction.CommitAsync();
            return result;
        }
    }
}