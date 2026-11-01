using System.Globalization;
using System.Text.Json;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed partial class OrderRepository
{
    public async Task<TailoringOrderEditSnapshot?> GetEditSnapshotAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var snapshot = await ReadEditSnapshotAsync(connection, transaction, orderId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    }

    private static async Task<TailoringOrderEditSnapshot?> ReadEditSnapshotAsync(
        SqlConnection connection, SqlTransaction transaction, int orderId, CancellationToken cancellationToken)
    {
        const string orderSql = """
            SELECT orders.OrderID,orders.OrderNumber,orders.CustomerID,customer.CustomerName,customer.PhoneNumber,
                   orders.OrderDate,orders.DeliveryDate,orders.TotalAmount,orders.DiscountAmount,orders.PaidAmount,
                   orders.RemainingAmount,orders.UrgencyStatus,orders.OrderStatus,orders.Notes,orders.CreatedDate,
                   orders.UpdatedDate,orders.CancellationReason,orders.CancelledAt,orders.CancelledBy,
                   orders.SaleCategory,orders.RevenueRecognized,orders.RevenueRecognizedAt,
                   orders.RevenueReversalCreated,orders.RevenueReversalCreatedAt,orders.EditVersion
            FROM dbo.Orders orders WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.Customers customer ON customer.CustomerID=orders.CustomerID WHERE orders.OrderID=@orderId
            """;
        await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
        orderCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await orderCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var order = new OrderDetailsDto(reader.GetInt32(0),reader.GetString(1),reader.GetInt32(2),
            reader.NullableString("CustomerName"),reader.NullableString("PhoneNumber"),reader.GetDateTime(5),
            reader.NullableDateTime("DeliveryDate"),reader.GetDecimal(7),reader.GetDecimal(8),reader.GetDecimal(9),
            reader.GetDecimal(10),reader.GetString(11),reader.GetString(12),reader.NullableString("Notes"),
            reader.GetDateTime(14),reader.NullableDateTime("UpdatedDate"),reader.NullableString("CancellationReason"),
            reader.NullableDateTime("CancelledAt"),reader.NullableString("CancelledBy"),reader.GetString(19),
            reader.GetBoolean(20),reader.NullableDateTime("RevenueRecognizedAt"),reader.GetBoolean(22),
            reader.NullableDateTime("RevenueReversalCreatedAt"));
        var version = Convert.ToBase64String((byte[])reader[24]);
        await reader.DisposeAsync();
        var beforeReady = OrderAddPieceGuard.CanAddPiece(order.OrderStatus, false);
        var canEdit = order.SaleCategory == "TailoringOrder" && !order.RevenueRecognized &&
            (beforeReady || order.OrderStatus == "ReadyForDelivery");
        const string pieceSql = """
            SELECT piece.PieceID,piece.OrderItemID,piece.TrackingCode,piece.PieceStatus,item.ProductTypeId,
                   item.PieceType,item.Quantity,item.FabricCode,item.FabricType,item.FabricColor,
                   item.MeasurementSnapshot,item.Request1,item.Request2,item.Notes1,item.Notes2,
                   price.OriginalSalePrice,price.ApprovedSalePrice,price.DiscountShare,
                   CASE WHEN EXISTS(SELECT 1 FROM dbo.TrackingEvents event WITH(HOLDLOCK) WHERE event.PieceID=piece.PieceID) THEN 1 ELSE 0 END AS HasTracking,
                   CASE WHEN EXISTS(SELECT 1 FROM dbo.PieceWageRecords wage WITH(HOLDLOCK) WHERE wage.PieceID=piece.PieceID) THEN 1 ELSE 0 END AS HasWage
            FROM dbo.Pieces piece WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.OrderItems item WITH(UPDLOCK,HOLDLOCK) ON item.OrderItemID=piece.OrderItemID
            LEFT JOIN dbo.TailoringPieceSaleValues price ON price.PieceID=piece.PieceID
            WHERE item.OrderID=@orderId ORDER BY piece.PieceID
            """;
        await using var pieceCommand = new SqlCommand(pieceSql, connection, transaction);
        pieceCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var pieceReader = await pieceCommand.ExecuteReaderAsync(cancellationToken);
        var pieces = new List<TailoringEditablePiece>();
        while (await pieceReader.ReadAsync(cancellationToken))
        {
            var measurements = ParseMeasurementSnapshot(pieceReader.NullableString("MeasurementSnapshot"))
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(CultureInfo.InvariantCulture));
            var metadata = ReadPieceMetadata(pieceReader.NullableString("MeasurementSnapshot"));
            var active = !string.Equals(pieceReader.GetString(3), "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pieceReader.GetString(3), "Canceled", StringComparison.OrdinalIgnoreCase);
            var hasPrice = !pieceReader.IsDBNull(15);
            var single = pieceReader.GetInt32(6) == 1;
            var hasTracking = pieceReader.GetInt32(18) == 1;
            var hasWage = pieceReader.GetInt32(19) == 1;
            pieces.Add(new TailoringEditablePiece
            {
                PieceId = pieceReader.GetInt32(0), OrderItemId = pieceReader.GetInt32(1),
                TrackingCode = pieceReader.GetString(2), PieceStatus = pieceReader.GetString(3),
                ProductTypeId = pieceReader.IsDBNull(4) ? 0 : pieceReader.GetInt32(4), PieceType = pieceReader.GetString(5),
                FabricCode = pieceReader.NullableString("FabricCode") ?? string.Empty,
                FabricType = pieceReader.NullableString("FabricType"), FabricColor = pieceReader.NullableString("FabricColor"),
                CatalogNumber = metadata.GetValueOrDefault("_catalogNumber"),
                Request1 = pieceReader.NullableString("Request1"), Request2 = pieceReader.NullableString("Request2"),
                Notes1 = pieceReader.NullableString("Notes1"), Notes2 = pieceReader.NullableString("Notes2"),
                SpecialRequest = metadata.GetValueOrDefault("specialRequest"), Measurements = measurements,
                OriginalSalePrice = pieceReader.NullableDecimal("OriginalSalePrice"),
                ApprovedSalePrice = pieceReader.NullableDecimal("ApprovedSalePrice"),
                DiscountShare = pieceReader.NullableDecimal("DiscountShare"), HasTracking = hasTracking, HasWage = hasWage,
                CanEditDetails = canEdit && active && single,
                CanEditMeasurements = canEdit && beforeReady && active && single && hasPrice && !hasTracking && !hasWage,
                CanCancel = canEdit && beforeReady && active && hasPrice,
                LockReason = !active ? "القطعة ملغاة." : !hasPrice ? "لا يوجد سعر أصلي موثق لهذه القطعة."
                    : hasTracking || hasWage ? "المقاسات ونوع القطعة مقفلة بعد بدء العمل."
                    : !single ? "بيانات هذه القطعة مشتركة مع قطع أخرى في البند." : null
            });
        }
        var addedPieceIds = await ReadAddedPieceIdsAsync(connection, transaction, orderId, cancellationToken);
        return new TailoringOrderEditSnapshot(order,version,canEdit,canEdit && beforeReady,pieces,
            addedPieceIds,canEdit ? null : "حالة الطلب لا تسمح بالتعديل.");
    }

    private static async Task<Dictionary<Guid, int>> ReadAddedPieceIdsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT AddOperationId, PieceId
            FROM dbo.TailoringPieceAddOperations WITH (UPDLOCK,HOLDLOCK)
            WHERE OrderId=@orderId
            ORDER BY AddOperationId;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<Guid, int>();
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetInt32(1);
        return result;
    }

    private static Dictionary<string,string> ReadPieceMetadata(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot)) return new(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(snapshot);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
    }
}