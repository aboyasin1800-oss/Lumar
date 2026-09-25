using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class CancelledPieceDispositionRepository(OperationalSqlConnectionFactory operationalConnections) : ICancelledPieceDispositionRepository
{
    public async Task<CancelledPieceDispositionDto?> GetByPieceIdAsync(int pieceId, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = @"
            SELECT CancelledPieceDispositionId, PieceId, Decision, Reason, DecidedBy, DecidedAt, TransferStatus, ReadyMadeInventoryProductId, TransferredAt, CreatedAt
            FROM dbo.CancelledPieceDisposition WITH (NOLOCK)
            WHERE PieceId = @pieceId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    public async Task<CancelledPieceDispositionDto> SaveDecisionAsync(int pieceId, string decision, string? reason, string? decidedBy, CancellationToken ct)
    {
        var normalizedDecision = NormalizeDecision(decision);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var pieceContext = await GetPieceContextAsync(connection, transaction, pieceId, ct);
            if (pieceContext is null)
            {
                throw new InvalidOperationException("The specified piece does not exist.");
            }

            var existing = await GetDecisionForUpdateAsync(connection, transaction, pieceId, ct);
            if (existing is not null)
            {
                if (!string.Equals(existing.Decision, normalizedDecision, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A conflicting disposition decision already exists for this piece.");
                }

                var currentReason = string.IsNullOrWhiteSpace(reason) ? existing.Reason : reason.Trim();
                var currentBy = string.IsNullOrWhiteSpace(decidedBy) ? existing.DecidedBy : decidedBy.Trim();

                const string updateSql = @"
                    UPDATE dbo.CancelledPieceDisposition
                    SET Reason = @reason,
                        DecidedBy = @decidedBy,
                        DecidedAt = @decidedAt,
                        CreatedAt = ISNULL(CreatedAt, @createdAt)
                    WHERE PieceId = @pieceId;";
                await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@pieceId", pieceId);
                AddNullable(updateCommand, "@reason", currentReason);
                AddNullable(updateCommand, "@decidedBy", currentBy);
                updateCommand.Parameters.AddWithValue("@decidedAt", DateTime.UtcNow);
                updateCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                await updateCommand.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
                return await GetByPieceIdAsync(pieceId, ct) ?? throw new InvalidOperationException("Unable to load the persisted decision.");
            }

            var orderIsCancelled = string.Equals(pieceContext.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
            if (!orderIsCancelled)
            {
                throw new InvalidOperationException("The order must be cancelled before a decision can be saved.");
            }

            var now = DateTime.UtcNow;
            const string insertSql = @"
                INSERT INTO dbo.CancelledPieceDisposition
                    (PieceId, Decision, Reason, DecidedBy, DecidedAt, TransferStatus, ReadyMadeInventoryProductId, TransferredAt, CreatedAt)
                OUTPUT INSERTED.CancelledPieceDispositionId, INSERTED.PieceId, INSERTED.Decision, INSERTED.Reason, INSERTED.DecidedBy, INSERTED.DecidedAt, INSERTED.TransferStatus, INSERTED.ReadyMadeInventoryProductId, INSERTED.TransferredAt, INSERTED.CreatedAt
                VALUES (@pieceId, @decision, @reason, @decidedBy, @decidedAt, @transferStatus, NULL, NULL, @createdAt);";
            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("@pieceId", pieceId);
            insertCommand.Parameters.AddWithValue("@decision", normalizedDecision);
            AddNullable(insertCommand, "@reason", reason?.Trim());
            AddNullable(insertCommand, "@decidedBy", decidedBy?.Trim());
            insertCommand.Parameters.AddWithValue("@decidedAt", now);
            insertCommand.Parameters.AddWithValue("@transferStatus", normalizedDecision.Equals("ContinueToReadyInventory", StringComparison.OrdinalIgnoreCase) ? "Pending" : "NotTransferred");
            insertCommand.Parameters.AddWithValue("@createdAt", now);
            await using var insertReader = await insertCommand.ExecuteReaderAsync(ct);
            if (!await insertReader.ReadAsync(ct))
            {
                throw new InvalidOperationException("Unable to persist the cancellation disposition decision.");
            }

            await transaction.CommitAsync(ct);
            return Map(insertReader);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<CancelledPieceDispositionDto?> ExecuteDecisionAsync(int pieceId, CancellationToken ct)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var pieceContext = await GetPieceContextAsync(connection, transaction, pieceId, ct);
            if (pieceContext is null)
            {
                throw new InvalidOperationException("The specified piece does not exist.");
            }

            var disposition = await GetDecisionForUpdateAsync(connection, transaction, pieceId, ct);
            if (disposition is null)
            {
                throw new InvalidOperationException("No final disposition decision has been saved for this piece.");
            }

            if (string.Equals(disposition.Decision, "StopAndHold", StringComparison.OrdinalIgnoreCase))
            {
                const string updateSql = @"
                    UPDATE dbo.CancelledPieceDisposition
                    SET TransferStatus = N'NotTransferred',
                        ReadyMadeInventoryProductId = NULL,
                        TransferredAt = NULL,
                        DecidedAt = ISNULL(DecidedAt, @decidedAt)
                    WHERE PieceId = @pieceId;";
                await using var update = new SqlCommand(updateSql, connection, transaction);
                update.Parameters.AddWithValue("@pieceId", pieceId);
                update.Parameters.AddWithValue("@decidedAt", DateTime.UtcNow);
                await update.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
                return await GetByPieceIdAsync(pieceId, ct);
            }

            if (string.Equals(disposition.Decision, "ContinueToReadyInventory", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(disposition.TransferStatus) && string.Equals(disposition.TransferStatus, "Transferred", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.CommitAsync(ct);
                return disposition;
            }

            if (!string.Equals(pieceContext.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The related order is not cancelled.");
            }

            var startedProduction = pieceContext.PieceStartedProduction || pieceContext.TrackingEventCount > 0;
            if (!startedProduction)
            {
                throw new InvalidOperationException("The piece has not started production, so it cannot be transferred to ready inventory.");
            }

            var assemblyComplete = pieceContext.HasAssemblyStage && (
                string.Equals(pieceContext.PieceStatus, "Ready", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pieceContext.PieceStatus, "Assembly", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pieceContext.PieceStatus, "ReadyForSale", StringComparison.OrdinalIgnoreCase));

            if (!assemblyComplete)
            {
                throw new InvalidOperationException("The piece is not yet complete at Assembly.");
            }

            var materialCost = await ResolveActualCostAsync(connection, transaction, pieceContext.OrderItemId, ct);
            if (materialCost <= 0m)
            {
                const string blockedSql = @"
                    UPDATE dbo.CancelledPieceDisposition
                    SET TransferStatus = N'Blocked',
                        ReadyMadeInventoryProductId = NULL,
                        TransferredAt = NULL,
                        Reason = ISNULL(Reason, N'Cost not available for ready inventory transfer.')
                    WHERE PieceId = @pieceId;";
                await using var blocked = new SqlCommand(blockedSql, connection, transaction);
                blocked.Parameters.AddWithValue("@pieceId", pieceId);
                await blocked.ExecuteNonQueryAsync(ct);

                await transaction.CommitAsync(ct);
                return await GetByPieceIdAsync(pieceId, ct);
            }

            var transferResult = await InsertReadyMadeInventoryProductAsync(connection, transaction, pieceContext, materialCost, ct);
            if (transferResult is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            const string transferSql = @"
                UPDATE dbo.CancelledPieceDisposition
                SET TransferStatus = N'Transferred',
                    ReadyMadeInventoryProductId = @readyMadeInventoryProductId,
                    TransferredAt = @transferredAt
                WHERE PieceId = @pieceId;";
            await using var transferCommand = new SqlCommand(transferSql, connection, transaction);
            transferCommand.Parameters.AddWithValue("@pieceId", pieceId);
            transferCommand.Parameters.AddWithValue("@readyMadeInventoryProductId", transferResult.Value);
            transferCommand.Parameters.AddWithValue("@transferredAt", DateTime.UtcNow);
            await transferCommand.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
            return await GetByPieceIdAsync(pieceId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<PieceTransferContext?> GetPieceContextAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.PieceID,
                   p.OrderItemID,
                   p.TrackingCode,
                   p.PieceStatus,
                   p.PieceNumber,
                   oi.PieceType,
                   oi.ProductTypeId,
                   oi.FabricCode,
                   oi.FabricType,
                   oi.FabricColor,
                   oi.MeasurementSnapshot,
                   o.OrderID,
                   o.OrderStatus,
                   o.OrderNumber,
                   ISNULL((SELECT COUNT(*) FROM dbo.TrackingEvents te WITH (NOLOCK) WHERE te.PieceID = p.PieceID), 0) AS TrackingEventCount,
                   ISNULL((SELECT TOP (1) CASE WHEN te.Stage = N'Assembly' THEN 1 ELSE 0 END FROM dbo.TrackingEvents te WITH (NOLOCK) WHERE te.PieceID = p.PieceID ORDER BY te.EventTime DESC, te.TrackingEventID DESC), 0) AS HasAssemblyStage
            FROM dbo.Pieces p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            WHERE p.PieceID = @pieceId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var pieceStatus = reader.GetString(3);
        var trackingEventCount = reader.GetInt32(15);
        var hasAssemblyStage = reader.GetInt32(16) == 1;
        var pieceStartedProduction = !string.Equals(pieceStatus, "New", StringComparison.OrdinalIgnoreCase) || trackingEventCount > 0;

        return new PieceTransferContext(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            pieceStatus,
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.NullableString("FabricCode"),
            reader.NullableString("FabricType"),
            reader.NullableString("FabricColor"),
            reader.NullableString("MeasurementSnapshot"),
            reader.GetInt32(11),
            reader.GetString(12),
            reader.GetString(13),
            trackingEventCount,
            hasAssemblyStage,
            pieceStartedProduction
        );
    }

    private static async Task<CancelledPieceDispositionDto?> GetDecisionForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, CancellationToken ct)
    {
        const string sql = @"
            SELECT CancelledPieceDispositionId, PieceId, Decision, Reason, DecidedBy, DecidedAt, TransferStatus, ReadyMadeInventoryProductId, TransferredAt, CreatedAt
            FROM dbo.CancelledPieceDisposition WITH (UPDLOCK, HOLDLOCK)
            WHERE PieceId = @pieceId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    private static async Task<decimal> ResolveActualCostAsync(SqlConnection connection, SqlTransaction transaction, int orderItemId, CancellationToken ct)
    {
        const string sql = @"
            SELECT ISNULL(SUM(CAST(TotalCost AS decimal(18,2))), 0)
            FROM dbo.OrderItemFabrics WITH (NOLOCK)
            WHERE OrderItemID = @orderItemId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        var value = await command.ExecuteScalarAsync(ct);
        return value is decimal cost ? cost : 0m;
    }

    private static async Task<int?> InsertReadyMadeInventoryProductAsync(SqlConnection connection, SqlTransaction transaction, PieceTransferContext pieceContext, decimal actualCost, CancellationToken ct)
    {
        var productionOrderNumber = $"RMP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var productionName = $"CancelledPiece-{pieceContext.PieceId}";
        var now = DateTime.UtcNow;

        const string orderSql = @"
            INSERT INTO dbo.ReadyMadeProductionOrders
                (ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice, Status, Notes, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderId, INSERTED.ProductionOrderNumber, INSERTED.ProductionName
            VALUES (@productionOrderNumber, @productionName, @totalCost, 0, @suggestedSellingPrice, N'Completed', N'Cancelled piece conversion to ready inventory', @createdAt);";
        await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
        orderCommand.Parameters.AddWithValue("@productionOrderNumber", productionOrderNumber);
        orderCommand.Parameters.AddWithValue("@productionName", productionName);
        orderCommand.Parameters.AddWithValue("@totalCost", actualCost);
        orderCommand.Parameters.AddWithValue("@suggestedSellingPrice", actualCost);
        orderCommand.Parameters.AddWithValue("@createdAt", now);
        await using var orderReader = await orderCommand.ExecuteReaderAsync(ct);
        if (!await orderReader.ReadAsync(ct))
        {
            return null;
        }

        var readyMadeOrderId = orderReader.GetInt32(0);
        var readyMadeOrderNumber = orderReader.GetString(1);
        await orderReader.CloseAsync();

        const string itemSql = @"
            INSERT INTO dbo.ReadyMadeProductionOrderItems
                (ReadyMadeProductionOrderId, ProductTypeId, PieceType, Quantity, FabricCode, FabricType, FabricColor, CatalogNumber, FabricCost, PieceCost, LineTotal, MeasurementSnapshot, PieceStatus, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderItemId
            VALUES (@readyMadeOrderId, @productTypeId, @pieceType, 1, @fabricCode, @fabricType, @fabricColor, @catalogNumber, @fabricCost, @pieceCost, @lineTotal, @measurementSnapshot, N'Ready', @createdAt);";
        await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
        itemCommand.Parameters.AddWithValue("@readyMadeOrderId", readyMadeOrderId);
        itemCommand.Parameters.AddWithValue("@productTypeId", pieceContext.ProductTypeId);
        itemCommand.Parameters.AddWithValue("@pieceType", pieceContext.PieceType);
        AddNullable(itemCommand, "@fabricCode", pieceContext.FabricCode);
        AddNullable(itemCommand, "@fabricType", pieceContext.FabricType);
        AddNullable(itemCommand, "@fabricColor", pieceContext.FabricColor);
        AddNullable(itemCommand, "@catalogNumber", null);
        itemCommand.Parameters.AddWithValue("@fabricCost", actualCost);
        itemCommand.Parameters.AddWithValue("@pieceCost", actualCost);
        itemCommand.Parameters.AddWithValue("@lineTotal", actualCost);
        AddNullable(itemCommand, "@measurementSnapshot", pieceContext.MeasurementSnapshot);
        itemCommand.Parameters.AddWithValue("@createdAt", now);
        var readyMadeOrderItemId = (int)(await itemCommand.ExecuteScalarAsync(ct))!;

        const string pieceSql = @"
            INSERT INTO dbo.ReadyMadeProductionOrderPieceInstances
                (ReadyMadeProductionOrderItemId, PieceNumber, TrackingCode, PieceStatus, CreatedAt)
            OUTPUT INSERTED.ReadyMadeProductionOrderPieceInstanceId
            VALUES (@readyMadeOrderItemId, 1, @trackingCode, N'Ready', @createdAt);";
        await using var pieceCommand = new SqlCommand(pieceSql, connection, transaction);
        pieceCommand.Parameters.AddWithValue("@readyMadeOrderItemId", readyMadeOrderItemId);
        pieceCommand.Parameters.AddWithValue("@trackingCode", pieceContext.TrackingCode);
        pieceCommand.Parameters.AddWithValue("@createdAt", now);
        var readyMadePieceInstanceId = (int)(await pieceCommand.ExecuteScalarAsync(ct))!;

        const string readyInventorySql = @"
            INSERT INTO dbo.ReadyMadeInventoryProducts
                (ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadeProductionOrderPieceInstanceId,
                 ProductTypeId, ProductionOrderNumber, ProductionName, PieceType, PieceNumber, TrackingCode, FabricCode, FabricType,
                 FabricColor, CatalogNumber, FabricUnit, FabricWidth, FabricWidthUnit, ActualCost, SuggestedSellingPrice,
                 MeasurementSnapshot, ReadyForSaleAt, Status, Source, Notes, IsActive, CreatedAt)
            OUTPUT INSERTED.ReadyMadeInventoryProductId
            VALUES (@readyMadeProductionOrderId, @readyMadeProductionOrderItemId, @readyMadeProductionOrderPieceInstanceId,
                    @productTypeId, @productionOrderNumber, @productionName, @pieceType, 1, @trackingCode, @fabricCode, @fabricType,
                    @fabricColor, @catalogNumber, N'Piece', NULL, NULL, @actualCost, @suggestedSellingPrice,
                    @measurementSnapshot, @readyForSaleAt, N'AvailableForSale', N'CancelledOrderPieceTransfer', N'Cancelled order piece transferred after assembly.', 1, @createdAt);";
        await using var inventoryCommand = new SqlCommand(readyInventorySql, connection, transaction);
        inventoryCommand.Parameters.AddWithValue("@readyMadeProductionOrderId", readyMadeOrderId);
        inventoryCommand.Parameters.AddWithValue("@readyMadeProductionOrderItemId", readyMadeOrderItemId);
        inventoryCommand.Parameters.AddWithValue("@readyMadeProductionOrderPieceInstanceId", readyMadePieceInstanceId);
        inventoryCommand.Parameters.AddWithValue("@productTypeId", pieceContext.ProductTypeId);
        inventoryCommand.Parameters.AddWithValue("@productionOrderNumber", readyMadeOrderNumber);
        inventoryCommand.Parameters.AddWithValue("@productionName", productionName);
        inventoryCommand.Parameters.AddWithValue("@pieceType", pieceContext.PieceType);
        inventoryCommand.Parameters.AddWithValue("@trackingCode", pieceContext.TrackingCode);
        AddNullable(inventoryCommand, "@fabricCode", pieceContext.FabricCode);
        AddNullable(inventoryCommand, "@fabricType", pieceContext.FabricType);
        AddNullable(inventoryCommand, "@fabricColor", pieceContext.FabricColor);
        AddNullable(inventoryCommand, "@catalogNumber", null);
        inventoryCommand.Parameters.AddWithValue("@actualCost", actualCost);
        inventoryCommand.Parameters.AddWithValue("@suggestedSellingPrice", actualCost);
        AddNullable(inventoryCommand, "@measurementSnapshot", pieceContext.MeasurementSnapshot);
        inventoryCommand.Parameters.AddWithValue("@readyForSaleAt", now);
        inventoryCommand.Parameters.AddWithValue("@createdAt", now);
        var insertedProductId = (int?)(await inventoryCommand.ExecuteScalarAsync(ct));
        if (insertedProductId is null)
        {
            return null;
        }

        if (WipToFinishedGoodsFinancialTransactionPolicy.ShouldCreateTransaction(pieceContext.TrackingCode, actualCost, false))
        {
            const string financialSql = @"
                INSERT INTO dbo.FinancialTransactions
                    (ReferenceNumber, TransactionType, Amount, Description, CreatedAt)
                SELECT @referenceNumber, @transactionType, @amount, @description, @createdAt
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.FinancialTransactions WITH (UPDLOCK, HOLDLOCK)
                    WHERE ReferenceNumber = @referenceNumber
                      AND TransactionType = N'WipToFinishedGoods');";

            await using var financialCommand = new SqlCommand(financialSql, connection, transaction);
            financialCommand.Parameters.AddWithValue("@referenceNumber", pieceContext.TrackingCode);
            financialCommand.Parameters.AddWithValue("@transactionType", WipToFinishedGoodsFinancialTransactionPolicy.TransactionType);
            financialCommand.Parameters.AddWithValue("@amount", actualCost);
            financialCommand.Parameters.AddWithValue("@description", WipToFinishedGoodsFinancialTransactionPolicy.GetDescription());
            financialCommand.Parameters.AddWithValue("@createdAt", now);
            var financialTransactionRows = await financialCommand.ExecuteNonQueryAsync(ct);
            if (financialTransactionRows != 1)
                throw new InvalidOperationException("توجد سجلات مالية متعارضة للعملية الحالية.");

            var posting = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection,
                transaction,
                pieceContext.TrackingCode,
                WipToFinishedGoodsFinancialTransactionPolicy.TransactionType,
                actualCost,
                WipToFinishedGoodsFinancialTransactionPolicy.GetDescription(),
                ct);
            posting.ThrowIfFailure();
        }

        return insertedProductId;
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value is string text ? (string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim()) : value ?? DBNull.Value);

    private static CancelledPieceDispositionDto Map(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetInt32(1),
        reader.GetString(2),
        reader.NullableString("Reason"),
        reader.NullableString("DecidedBy"),
        reader.GetDateTime(5),
        reader.GetString(6),
        reader.NullableInt32("ReadyMadeInventoryProductId"),
        reader.NullableDateTime("TransferredAt"),
        reader.GetDateTime(9));

    private static string NormalizeDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new InvalidOperationException("A decision is required.");
        }

        decision = decision.Trim();
        if (string.Equals(decision, "ContinueToReadyInventory", StringComparison.OrdinalIgnoreCase)) return "ContinueToReadyInventory";
        if (string.Equals(decision, "StopAndHold", StringComparison.OrdinalIgnoreCase)) return "StopAndHold";
        throw new InvalidOperationException("Decision must be either ContinueToReadyInventory or StopAndHold.");
    }

    private sealed record PieceTransferContext(
        int PieceId,
        int OrderItemId,
        string TrackingCode,
        string PieceStatus,
        int PieceNumber,
        string PieceType,
        int ProductTypeId,
        string? FabricCode,
        string? FabricType,
        string? FabricColor,
        string? MeasurementSnapshot,
        int OrderId,
        string OrderStatus,
        string OrderNumber,
        int TrackingEventCount,
        bool HasAssemblyStage,
        bool PieceStartedProduction);
}
