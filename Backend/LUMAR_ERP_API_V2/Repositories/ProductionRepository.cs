using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
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
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderItemId,ReadyMadeProductionOrderId,PieceType,Quantity,FabricCode,FabricType,FabricColor,CatalogNumber,FabricCost,PieceCost,LineTotal,MeasurementSnapshot,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderItems WHERE ReadyMadeProductionOrderId=@id ORDER BY ReadyMadeProductionOrderItemId", reader => new ReadyMadeProductionOrderItemDto(reader.GetInt32(0),reader.GetInt32(1),reader.GetString(2),reader.GetInt32(3),reader.NullableString("FabricCode"),reader.NullableString("FabricType"),reader.NullableString("FabricColor"),reader.NullableString("CatalogNumber"),reader.NullableDecimal("FabricCost"),reader.NullableDecimal("PieceCost"),reader.NullableDecimal("LineTotal"),reader.NullableString("MeasurementSnapshot"),reader.GetString(12),reader.GetDateTime(13)), id, ct);
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderPieceInstanceId,ReadyMadeProductionOrderItemId,PieceNumber,TrackingCode,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE ReadyMadeProductionOrderItemId=@id ORDER BY PieceNumber,ReadyMadeProductionOrderPieceInstanceId", reader => new ReadyMadeProductionPieceDto(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetDateTime(5)), id, ct);
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken ct) => QueryAsync("SELECT o.OrderID,o.OrderNumber,o.CustomerID,c.CustomerCode,c.CustomerName,c.PhoneNumber,o.OrderStatus,o.DeliveryDate FROM dbo.Orders o INNER JOIN dbo.Customers c ON c.CustomerID=o.CustomerID ORDER BY CASE WHEN o.DeliveryDate IS NULL THEN 1 ELSE 0 END,o.DeliveryDate,o.OrderID DESC", reader => new ProductionDeliveryDto(reader.GetInt32(0),reader.GetString(1),reader.GetInt32(2),reader.NullableString("CustomerCode"),reader.NullableString("CustomerName"),reader.NullableString("PhoneNumber"),reader.GetString(6),reader.NullableDateTime("DeliveryDate")), null, ct);

    private static PieceDto MapPiece(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5), reader.GetString(6));
    private static TrackingEventDto MapTracking(SqlDataReader reader) => new(reader.GetInt32(0), reader.NullableInt32("OrderItemID"), reader.NullableInt32("OrderID"), reader.NullableString("TrackingCode"), reader.GetString(4), reader.GetString(5), reader.GetDateTime(6), reader.NullableString("EmployeeCode"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDateTime("RevertedAt"), reader.NullableInt32("PieceID"), reader.NullableInt32("ReadyMadeProductionOrderPieceInstanceId"));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? id, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id is not null) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}