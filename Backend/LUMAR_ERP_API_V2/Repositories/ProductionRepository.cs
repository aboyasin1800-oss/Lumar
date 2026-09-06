using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ProductionRepository(ReadOnlySqlConnectionFactory connections) : IProductionRepository
{
    public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken ct) => QueryAsync("SELECT p.PieceID, p.OrderItemID, p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedDate, oi.PieceType FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID ORDER BY p.CreatedDate DESC, p.PieceID DESC", MapPiece, null, ct);
    public async Task<PieceDto?> GetPieceByIdAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT p.PieceID, p.OrderItemID, p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedDate, oi.PieceType FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID WHERE p.PieceID = @id", MapPiece, id, ct)).SingleOrDefault();
    public async Task<WorkCardDto?> GetWorkCardAsync(int id, CancellationToken ct)
    {
        const string sql = """
            SELECT p.PieceID, oi.OrderItemID, o.OrderID, o.OrderNumber, p.PieceNumber, p.TrackingCode,
                   c.CustomerCode, c.CustomerName, c.PhoneNumber, oi.PieceType, oi.Quantity, oi.FabricType, oi.FabricColor,
                   oi.Notes1, oi.Notes2, oi.MeasurementSnapshot, o.DeliveryDate, p.PieceStatus
            FROM dbo.Pieces p
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            INNER JOIN dbo.Customers c ON c.CustomerID = o.CustomerID
            WHERE p.PieceID = @id
            """;
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var card = new WorkCardDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.GetString(9), reader.GetInt32(10), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.NullableString("Notes1"), reader.NullableString("Notes2"), reader.NullableString("MeasurementSnapshot"), reader.NullableDateTime("DeliveryDate"), reader.GetString(17), []);
        await reader.CloseAsync();

        const string trackingSql = "SELECT TrackingEventID, OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, EmployeeCode, Notes, IsReverted, RevertedAt, PieceID, ReadyMadeProductionOrderPieceInstanceId FROM dbo.TrackingEvents WHERE PieceID = @id ORDER BY EventTime DESC, TrackingEventID DESC";
        await using var trackingCommand = new SqlCommand(trackingSql, connection);
        trackingCommand.Parameters.AddWithValue("@id", id);
        await using var trackingReader = await trackingCommand.ExecuteReaderAsync(ct);
        var history = new List<TrackingEventDto>();
        while (await trackingReader.ReadAsync(ct)) history.Add(MapTracking(trackingReader));
        return card with { TrackingHistory = history };
    }
    public Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int id, CancellationToken ct) => QueryAsync("SELECT TrackingEventID, OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, EmployeeCode, Notes, IsReverted, RevertedAt, PieceID, ReadyMadeProductionOrderPieceInstanceId FROM dbo.TrackingEvents WHERE PieceID = @id ORDER BY EventTime DESC, TrackingEventID DESC", reader => new TrackingEventDto(reader.GetInt32(0), reader.NullableInt32("OrderItemID"), reader.NullableInt32("OrderID"), reader.NullableString("TrackingCode"), reader.GetString(4), reader.GetString(5), reader.GetDateTime(6), reader.NullableString("EmployeeCode"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDateTime("RevertedAt"), reader.NullableInt32("PieceID"), reader.NullableInt32("ReadyMadeProductionOrderPieceInstanceId")), id, ct);
    public Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken ct) => QueryAsync("SELECT DISTINCT Stage, Status FROM dbo.TrackingEvents ORDER BY Stage, Status", reader => new ProductionStageDto(reader.GetString(0), reader.GetString(1)), null, ct);

    public async Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*), SUM(CASE WHEN p.PieceStatus IN (N'Printing',N'Cutting',N'InProduction') THEN 1 ELSE 0 END), SUM(CASE WHEN p.PieceStatus=N'Ready' THEN 1 ELSE 0 END), SUM(CASE WHEN p.PieceStatus=N'Delivered' THEN 1 ELSE 0 END), (SELECT COUNT(DISTINCT Stage) FROM dbo.TrackingEvents), (SELECT COUNT(*) FROM dbo.ReadyMadeInventoryProducts WHERE Status=N'AvailableForSale' AND IsActive=1), SUM(CASE WHEN o.DeliveryDate < SYSDATETIME() AND o.OrderStatus NOT IN (N'Delivered',N'Cancelled') THEN 1 ELSE 0 END) FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID INNER JOIN dbo.Orders o ON o.OrderID=oi.OrderID";
        await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        return new ProductionDashboardDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
    }
    public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderId,ProductionOrderNumber,ProductionName,TotalCost,ProfitPercentage,SuggestedSellingPrice,Status,Notes,CreatedAt FROM dbo.ReadyMadeProductionOrders ORDER BY CreatedAt DESC,ReadyMadeProductionOrderId DESC", reader => new ReadyMadeProductionOrderDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetString(6),reader.NullableString("Notes"),reader.GetDateTime(8)), null, ct);
    public async Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken ct) => (await QueryAsync("SELECT ReadyMadeProductionOrderId,ProductionOrderNumber,ProductionName,TotalCost,ProfitPercentage,SuggestedSellingPrice,Status,Notes,CreatedAt FROM dbo.ReadyMadeProductionOrders WHERE ReadyMadeProductionOrderId = @id", reader => new ReadyMadeProductionOrderDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetString(6),reader.NullableString("Notes"),reader.GetDateTime(8)), orderId, ct)).SingleOrDefault();
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderItemId,ReadyMadeProductionOrderId,PieceType,Quantity,FabricCode,FabricType,FabricColor,CatalogNumber,FabricCost,PieceCost,LineTotal,MeasurementSnapshot,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderItems WHERE ReadyMadeProductionOrderId=@id ORDER BY ReadyMadeProductionOrderItemId", reader => new ReadyMadeProductionOrderItemDto(reader.GetInt32(0),reader.GetInt32(1),reader.GetString(2),reader.GetInt32(3),reader.NullableString("FabricCode"),reader.NullableString("FabricType"),reader.NullableString("FabricColor"),reader.NullableString("CatalogNumber"),reader.NullableDecimal("FabricCost"),reader.NullableDecimal("PieceCost"),reader.NullableDecimal("LineTotal"),reader.NullableString("MeasurementSnapshot"),reader.GetString(12),reader.GetDateTime(13)), id, ct);
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderPieceInstanceId,ReadyMadeProductionOrderItemId,PieceNumber,TrackingCode,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE ReadyMadeProductionOrderItemId=@id ORDER BY PieceNumber,ReadyMadeProductionOrderPieceInstanceId", reader => new ReadyMadeProductionPieceDto(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetDateTime(5)), id, ct);
    public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int id, CancellationToken ct) => QueryAsync("SELECT TrackingEventID,OrderItemID,OrderID,TrackingCode,Stage,Status,EventTime,EmployeeCode,Notes,IsReverted,RevertedAt,PieceID,ReadyMadeProductionOrderPieceInstanceId FROM dbo.TrackingEvents WHERE ReadyMadeProductionOrderPieceInstanceId=@id ORDER BY EventTime,TrackingEventID", MapTracking, id, ct);
    public async Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken ct)
    {
        if (order.Items.Count == 0) throw new InvalidOperationException("يجب إضافة بند واحد على الأقل.");
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var orderNumber = await GetNextReadyMadeOrderNumberAsync(connection, transaction, ct);
            var status = "New";
            const string orderSql = @"
                INSERT INTO dbo.ReadyMadeProductionOrders
                    (ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice, Status, Notes, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderId, INSERTED.ProductionOrderNumber, INSERTED.ProductionName, INSERTED.TotalCost, INSERTED.ProfitPercentage, INSERTED.SuggestedSellingPrice, INSERTED.Status, INSERTED.CreatedAt
                VALUES (@number, @name, @totalCost, @profit, @sell, @status, @notes, @createdAt);";
            await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
            orderCommand.Parameters.AddWithValue("@number", orderNumber);
            orderCommand.Parameters.AddWithValue("@name", order.ProductionName.Trim());
            orderCommand.Parameters.AddWithValue("@totalCost", order.TotalCost);
            orderCommand.Parameters.AddWithValue("@profit", order.ProfitPercentage);
            orderCommand.Parameters.AddWithValue("@sell", order.SuggestedSellingPrice);
            orderCommand.Parameters.AddWithValue("@status", status);
            AddNullable(orderCommand, "@notes", order.Notes);
            orderCommand.Parameters.AddWithValue("@createdAt", now);
            await using var orderReader = await orderCommand.ExecuteReaderAsync(ct);
            if (!await orderReader.ReadAsync(ct)) throw new InvalidOperationException("فشل إنشاء أمر الإنتاج." );
            var orderId = orderReader.GetInt32(0);
            var createdOrderNumber = orderReader.GetString(1);
            var createdName = orderReader.GetString(2);
            var createdCost = orderReader.GetDecimal(3);
            var createdProfit = orderReader.GetDecimal(4);
            var createdSell = orderReader.GetDecimal(5);
            var createdStatus = orderReader.GetString(6);
            var createdAt = orderReader.GetDateTime(7);
            await orderReader.CloseAsync();

            foreach (var item in order.Items)
            {
                if (string.IsNullOrWhiteSpace(item.PieceType)) throw new InvalidOperationException("نوع القطعة مطلوب.");
                if (item.Quantity <= 0) throw new InvalidOperationException("كمية القطع يجب أن تكون أكبر من صفر.");

                const string itemSql = @"
                    INSERT INTO dbo.ReadyMadeProductionOrderItems
                        (ReadyMadeProductionOrderId, PieceType, Quantity, FabricCode, FabricType, FabricColor, CatalogNumber, FabricCost, PieceCost, LineTotal, MeasurementSnapshot, PieceStatus, CreatedAt)
                    OUTPUT INSERTED.ReadyMadeProductionOrderItemId
                    VALUES (@orderId, @pieceType, @quantity, @fabricCode, @fabricType, @fabricColor, @catalogNumber, @fabricCost, @pieceCost, @lineTotal, @measurementSnapshot, N'New', @createdAt);";
                await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
                itemCommand.Parameters.AddWithValue("@orderId", orderId);
                itemCommand.Parameters.AddWithValue("@pieceType", item.PieceType.Trim());
                itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
                AddNullable(itemCommand, "@fabricCode", item.FabricCode);
                AddNullable(itemCommand, "@fabricType", item.FabricType);
                AddNullable(itemCommand, "@fabricColor", item.FabricColor);
                AddNullable(itemCommand, "@catalogNumber", item.CatalogNumber);
                AddNullable(itemCommand, "@fabricCost", item.FabricCost ?? 0m);
                AddNullable(itemCommand, "@pieceCost", item.PieceCost ?? 0m);
                AddNullable(itemCommand, "@lineTotal", item.LineTotal ?? 0m);
                AddNullable(itemCommand, "@measurementSnapshot", item.MeasurementSnapshot);
                itemCommand.Parameters.AddWithValue("@createdAt", now);
                var itemId = (int)(await itemCommand.ExecuteScalarAsync(ct))!;

                for (var pieceNumber = 1; pieceNumber <= item.Quantity; pieceNumber++)
                {
                    var trackingCode = await GetNextTrackingCodeAsync(connection, transaction, ct);
                    const string pieceSql = @"
                        INSERT INTO dbo.ReadyMadeProductionOrderPieceInstances
                            (ReadyMadeProductionOrderItemId, PieceNumber, TrackingCode, PieceStatus, CreatedAt)
                        VALUES (@itemId, @pieceNumber, @trackingCode, N'New', @createdAt);";
                    await using var pieceCommand = new SqlCommand(pieceSql, connection, transaction);
                    pieceCommand.Parameters.AddWithValue("@itemId", itemId);
                    pieceCommand.Parameters.AddWithValue("@pieceNumber", pieceNumber);
                    pieceCommand.Parameters.AddWithValue("@trackingCode", trackingCode);
                    pieceCommand.Parameters.AddWithValue("@createdAt", now);
                    await pieceCommand.ExecuteNonQueryAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
            return new ReadyMadeProductionOrderCreateResultDto(orderId, createdOrderNumber, createdName, createdCost, createdProfit, createdSell, createdStatus, createdAt);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken ct) => QueryAsync("SELECT o.OrderID,o.OrderNumber,o.CustomerID,c.CustomerCode,c.CustomerName,c.PhoneNumber,o.OrderStatus,o.DeliveryDate FROM dbo.Orders o INNER JOIN dbo.Customers c ON c.CustomerID=o.CustomerID ORDER BY CASE WHEN o.DeliveryDate IS NULL THEN 1 ELSE 0 END,o.DeliveryDate,o.OrderID DESC", reader => new ProductionDeliveryDto(reader.GetInt32(0),reader.GetString(1),reader.GetInt32(2),reader.NullableString("CustomerCode"),reader.NullableString("CustomerName"),reader.NullableString("PhoneNumber"),reader.GetString(6),reader.NullableDateTime("DeliveryDate")), null, ct);

    private static PieceDto MapPiece(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5), reader.GetString(6));

    private static async Task<string> GetNextReadyMadeOrderNumberAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "ProductionTrackingPrefix", "RMP-", ct);
        var sql = @"
            SELECT ISNULL(MAX(CAST(SUBSTRING(ProductionOrderNumber, CHARINDEX('-', ProductionOrderNumber) + 1, 20) AS int)), 0) + 1
            FROM dbo.ReadyMadeProductionOrders WITH (TABLOCKX, HOLDLOCK)
            WHERE ProductionOrderNumber LIKE @prefix;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", $"{prefix}%");
        var next = (int)(await command.ExecuteScalarAsync(ct))!;
        return $"{prefix}{next:D6}";
    }

    private static async Task<string> GetNextTrackingCodeAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "PieceTrackingPrefix", "TRK-", ct);
        var sql = @"
            SELECT ISNULL(MAX(CAST(REPLACE(TrackingCode, @prefix2, '') AS int)), 0) + 1
            FROM (
                SELECT TrackingCode FROM dbo.Pieces WITH (TABLOCKX, HOLDLOCK)
                UNION ALL
                SELECT TrackingCode FROM dbo.ReadyMadeProductionOrderPieceInstances WITH (TABLOCKX, HOLDLOCK)
                UNION ALL
                SELECT TrackingCode FROM dbo.ReadyMadeInventoryProducts WITH (TABLOCKX, HOLDLOCK)
            ) q
            WHERE TrackingCode LIKE @prefix;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", $"{prefix}%");
        command.Parameters.AddWithValue("@prefix2", prefix);
        var next = (int)(await command.ExecuteScalarAsync(ct))!;
        return $"{prefix}{next:D6}";
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value is string text ? (string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim()) : value ?? DBNull.Value);
    private static TrackingEventDto MapTracking(SqlDataReader reader) => new(reader.GetInt32(0), reader.NullableInt32("OrderItemID"), reader.NullableInt32("OrderID"), reader.NullableString("TrackingCode"), reader.GetString(4), reader.GetString(5), reader.GetDateTime(6), reader.NullableString("EmployeeCode"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDateTime("RevertedAt"), reader.NullableInt32("PieceID"), reader.NullableInt32("ReadyMadeProductionOrderPieceInstanceId"));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? id, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id is not null) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}