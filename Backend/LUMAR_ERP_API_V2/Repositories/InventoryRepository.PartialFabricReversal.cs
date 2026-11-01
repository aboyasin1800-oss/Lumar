using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed partial class InventoryRepository
{
    public static async Task<InventoryFoundationPostingResultDto> ReverseFabricConsumptionInTransactionAsync(
        SqlConnection connection, SqlTransaction transaction, ReverseFabricConsumptionDto request, CancellationToken ct)
    {
        if (transaction.Connection != connection || request.SourceOperationId == Guid.Empty ||
            request.FabricConsumptionSourceId <= 0 || request.PieceId <= 0 || request.QuantityInches <= 0 ||
            string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ReversedBy))
            throw new ArgumentException("العكس الجزئي يتطلب مصدرًا وقطعة وكمية ومعرف عملية ومعاملة نشطة.");

        const string sourceSql = """
            SELECT source.InventoryItemId,item.ItemCode,source.ConsumedQuantity,source.UnitId,
                   source.OfficialUnitCost,source.OperationalAmount,source.PostingAmount,
                   source.AccountingEventId,source.FabricRollId,source.OrderItemId,source.PieceId,event.Status
            FROM dbo.FabricConsumptionSources source WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.InventoryItems item ON item.InventoryItemID=source.InventoryItemId
            JOIN dbo.AccountingEvents event WITH (UPDLOCK,HOLDLOCK) ON event.AccountingEventId=source.AccountingEventId
            WHERE source.FabricConsumptionSourceId=@sourceId AND event.AccountingEventType=8
              AND EXISTS (SELECT 1 FROM dbo.Pieces piece WITH (UPDLOCK,HOLDLOCK)
                  WHERE piece.PieceID=@pieceId AND piece.OrderItemID=source.OrderItemId
                    AND (source.PieceId IS NULL OR source.PieceId=piece.PieceID))
            """;
        await using var sourceCommand = new SqlCommand(sourceSql, connection, transaction);
        sourceCommand.Parameters.AddWithValue("@sourceId", request.FabricConsumptionSourceId);
        sourceCommand.Parameters.AddWithValue("@pieceId", request.PieceId);
        await using var sourceReader = await sourceCommand.ExecuteReaderAsync(ct);
        if (!await sourceReader.ReadAsync(ct))
            throw new InvalidOperationException("مصدر صرف القماش لا يخص القطعة أو لا يملك حدثًا محاسبيًا معتمدًا.");
        var inventoryId = sourceReader.GetInt32(0);
        var fabricCode = sourceReader.GetString(1);
        var originalQuantity = sourceReader.GetDecimal(2);
        var unitId = sourceReader.GetInt16(3);
        var originalUnitCost = sourceReader.GetDecimal(4);
        var originalOperational = sourceReader.GetDecimal(5);
        var originalPosting = sourceReader.GetDecimal(6);
        var originalEventId = sourceReader.GetInt64(7);
        long? rollId = sourceReader.IsDBNull(8) ? null : sourceReader.GetInt64(8);
        var originalStatus = sourceReader.GetString(11);
        await sourceReader.DisposeAsync();
        if (unitId is not (1 or 2)) throw new InvalidOperationException("وحدة المصدر غير مدعومة للعكس.");
        var quantity = decimal.Round(request.QuantityInches / (unitId == 1 ? 36m : 1m), 6, MidpointRounding.AwayFromZero);
        if (quantity <= 0) throw new ArgumentException("كمية العكس أقل من الدقة المخزنية.");

        await using (var existingCommand = new SqlCommand("""
            SELECT FabricConsumptionPartialReversalId,FabricConsumptionSourceId,PieceId,ReturnedQuantity,
                   OperationalAmount,PostingAmount,AccountingEventId,InventoryTransactionId
            FROM dbo.FabricConsumptionPartialReversals WITH (UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operationId
            """, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("@operationId", request.SourceOperationId);
            await using var existing = await existingCommand.ExecuteReaderAsync(ct);
            if (await existing.ReadAsync(ct))
            {
                if (existing.GetInt64(1) != request.FabricConsumptionSourceId || existing.GetInt32(2) != request.PieceId || existing.GetDecimal(3) != quantity)
                    throw new InvalidOperationException("معرف عملية العكس مستخدم لبيانات أخرى.");
                if (existing.IsDBNull(6) || existing.IsDBNull(7))
                    throw new InvalidOperationException("روابط العكس السابق غير مكتملة.");
                return new InventoryFoundationPostingResultDto(existing.GetInt64(0), inventoryId, fabricCode,
                    quantity, existing.GetDecimal(4), existing.GetDecimal(5), existing.GetInt64(6), existing.GetInt32(7), true);
            }
        }

        if (!string.Equals(originalStatus, "Posted", StringComparison.Ordinal))
            throw new InvalidOperationException("تم عكس المصدر بالكامل ولا يمكن تجاوز قيمته الأصلية.");
        await using var totalsCommand = new SqlCommand("""
            SELECT ISNULL(SUM(ReturnedQuantity),0),ISNULL(SUM(OperationalAmount),0),ISNULL(SUM(PostingAmount),0)
            FROM dbo.FabricConsumptionPartialReversals WITH (UPDLOCK,HOLDLOCK) WHERE FabricConsumptionSourceId=@sourceId
            """, connection, transaction);
        totalsCommand.Parameters.AddWithValue("@sourceId", request.FabricConsumptionSourceId);
        await using var totals = await totalsCommand.ExecuteReaderAsync(ct);
        await totals.ReadAsync(ct);
        var remainingQuantity = originalQuantity - totals.GetDecimal(0);
        var remainingOperational = originalOperational - totals.GetDecimal(1);
        var remainingPosting = originalPosting - totals.GetDecimal(2);
        await totals.DisposeAsync();
        if (quantity > remainingQuantity)
            throw new InvalidOperationException("كمية العكس تتجاوز المتبقي من المصدر الأصلي.");
        var amount = quantity == remainingQuantity ? remainingOperational
            : decimal.Round(quantity * originalUnitCost, 6, MidpointRounding.AwayFromZero);
        var posting = quantity == remainingQuantity ? remainingPosting
            : decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0 || amount > remainingOperational || posting <= 0 || posting > remainingPosting)
            throw new InvalidOperationException("قيمة العكس تتجاوز المصدر الأصلي أو تقل عن الدقة المحاسبية.");

        var now = DateTime.UtcNow;
        await using var insert = new SqlCommand("""
            INSERT dbo.FabricConsumptionPartialReversals
              (FabricConsumptionSourceId,PieceId,SourceOperationId,ReturnedQuantity,OperationalAmount,PostingAmount,Reason,ReversedBy,CreatedAt)
            OUTPUT INSERTED.FabricConsumptionPartialReversalId
            VALUES(@sourceId,@pieceId,@operationId,@quantity,@amount,@posting,@reason,@actor,@now)
            """, connection, transaction);
        insert.Parameters.AddWithValue("@sourceId", request.FabricConsumptionSourceId);
        insert.Parameters.AddWithValue("@pieceId", request.PieceId);
        insert.Parameters.AddWithValue("@operationId", request.SourceOperationId);
        AddDecimal(insert, "@quantity", quantity);
        AddDecimal(insert, "@amount", amount);
        AddDecimal(insert, "@posting", posting, 2);
        insert.Parameters.AddWithValue("@reason", request.Reason.Trim());
        insert.Parameters.AddWithValue("@actor", request.ReversedBy.Trim());
        insert.Parameters.AddWithValue("@now", now);
        var reversalId = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
        var reference = $"FabricPartialReversal:{reversalId}";
        var accounting = await FoundationPostingGateway.ReverseFabricPartialAsync(connection, transaction,
            reversalId, originalEventId, request.SourceOperationId, reference, request.Reason, request.ReversedBy, ct);
        await UpdateFabricInventoryAsync(connection, transaction, inventoryId, rollId, -quantity, -amount, now, ct);
        var movementId = await InsertFoundationInventoryTransactionAsync(connection, transaction, inventoryId,
            "FabricInventoryReturned", -quantity, reference, amount, originalUnitCost, request.SourceOperationId, now, ct);
        await using var link = new SqlCommand("""
            UPDATE dbo.FabricConsumptionPartialReversals SET AccountingEventId=@eventId,InventoryTransactionId=@movementId
              WHERE FabricConsumptionPartialReversalId=@reversalId;
            UPDATE dbo.InventoryTransactions SET AccountingEventId=@eventId WHERE TransactionID=@movementId;
            IF @completed=1 UPDATE dbo.AccountingEvents SET Status=N'Reversed' WHERE AccountingEventId=@originalId;
            """, connection, transaction);
        link.Parameters.AddWithValue("@eventId", accounting.AccountingEventId);
        link.Parameters.AddWithValue("@movementId", movementId);
        link.Parameters.AddWithValue("@reversalId", reversalId);
        link.Parameters.AddWithValue("@originalId", originalEventId);
        link.Parameters.AddWithValue("@completed", quantity == remainingQuantity);
        await link.ExecuteNonQueryAsync(ct);
        return new InventoryFoundationPostingResultDto(reversalId, inventoryId, fabricCode, quantity, amount,
            posting, accounting.AccountingEventId, movementId, false);
    }
}