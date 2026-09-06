using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class OrderRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IOrderRepository
{
    public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => QueryAsync("SELECT OrderID, OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, SaleCategory FROM dbo.Orders ORDER BY OrderDate DESC, OrderID DESC", reader => new OrderListDto(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTime(3), reader.NullableDateTime("DeliveryDate"), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetString(8), reader.GetString(9), reader.GetString(10)), null, cancellationToken);

    public async Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT OrderID, OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, UrgencyStatus, OrderStatus, Notes, CreatedDate, UpdatedDate, CancellationReason, CancelledAt, CancelledBy, SaleCategory, RevenueRecognized, RevenueRecognizedAt, RevenueReversalCreated, RevenueReversalCreatedAt FROM dbo.Orders WHERE OrderID = @orderId";
        var results = await QueryAsync(sql, reader => new OrderDetailsDto(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTime(3), reader.NullableDateTime("DeliveryDate"), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9), reader.GetString(10), reader.NullableString("Notes"), reader.GetDateTime(12), reader.NullableDateTime("UpdatedDate"), reader.NullableString("CancellationReason"), reader.NullableDateTime("CancelledAt"), reader.NullableString("CancelledBy"), reader.GetString(17), reader.GetBoolean(18), reader.NullableDateTime("RevenueRecognizedAt"), reader.GetBoolean(20), reader.NullableDateTime("RevenueReversalCreatedAt")), orderId, cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (!await CustomerExistsAsync(order.CustomerId, connection, transaction, cancellationToken)) return null;
            var now = DateTime.UtcNow;
            var orderPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "OrderCodePrefix", "ORD-", cancellationToken);
            var orderNumber = $"{orderPrefix}{await NextNumberAsync("dbo.Orders", "OrderNumber", orderPrefix, connection, transaction, cancellationToken):D6}";
            var remainingAmount = order.TotalAmount - order.DiscountAmount - order.AdvancePayment;
            const string orderSql = "INSERT INTO dbo.Orders (OrderNumber,CustomerID,OrderDate,DeliveryDate,TotalAmount,DiscountAmount,PaidAmount,RemainingAmount,UrgencyStatus,OrderStatus,Notes,CreatedDate,SaleCategory) OUTPUT INSERTED.OrderID VALUES (@number,@customerId,@orderDate,@deliveryDate,@total,@discount,@paid,@remaining,@urgency,N'New',@notes,@created,@category)";
            await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
            orderCommand.Parameters.AddWithValue("@number", orderNumber);
            orderCommand.Parameters.AddWithValue("@customerId", order.CustomerId);
            orderCommand.Parameters.AddWithValue("@orderDate", order.OrderDate ?? now);
            AddNullable(orderCommand, "@deliveryDate", order.DeliveryDate);
            orderCommand.Parameters.AddWithValue("@total", order.TotalAmount);
            orderCommand.Parameters.AddWithValue("@discount", order.DiscountAmount);
            orderCommand.Parameters.AddWithValue("@paid", order.AdvancePayment);
            orderCommand.Parameters.AddWithValue("@remaining", remainingAmount);
            orderCommand.Parameters.AddWithValue("@urgency", order.UrgencyStatus.Trim());
            AddNullable(orderCommand, "@notes", order.Notes);
            orderCommand.Parameters.AddWithValue("@created", now);
            orderCommand.Parameters.AddWithValue("@category", order.SaleCategory.Trim());
            var orderId = (int)(await orderCommand.ExecuteScalarAsync(cancellationToken))!;

            var trackingPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "PieceTrackingPrefix", "TRK-", cancellationToken);
            var nextTracking = await NextNumberAsync("dbo.Pieces", "TrackingCode", trackingPrefix, connection, transaction, cancellationToken);
            foreach (var item in order.Items)
            {
                var itemTracking = $"{trackingPrefix}{nextTracking++:D6}";
                var orderItemId = await InsertOrderItemAsync(orderId, item, itemTracking, now, connection, transaction, cancellationToken);
                for (var pieceNumber = 1; pieceNumber <= item.Quantity; pieceNumber++)
                    await InsertPieceAsync(orderItemId, $"{trackingPrefix}{nextTracking++:D6}", pieceNumber, now, connection, transaction, cancellationToken);
                if (item.Fabric is not null) await InsertFabricAsync(orderItemId, item.Fabric, now, connection, transaction, cancellationToken);
            }

            if (order.AdvancePayment > 0)
                await InsertAdvanceAsync(orderId, orderNumber, order.CustomerId, order.AdvancePayment, order.PaymentMethod, now, connection, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return await GetByIdAsync(orderId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT OrderItemID, OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, Request1, Request2, Notes1, Notes2, MeasurementSnapshot, TrackingCode, PieceStatus, CreatedDate FROM dbo.OrderItems WHERE OrderID = @orderId ORDER BY OrderItemID", reader => new OrderItemDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.NullableString("FabricCode"), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.NullableString("Request1"), reader.NullableString("Request2"), reader.NullableString("Notes1"), reader.NullableString("Notes2"), reader.NullableString("MeasurementSnapshot"), reader.NullableString("TrackingCode"), reader.NullableString("PieceStatus"), reader.GetDateTime(14)), orderId, cancellationToken);

    public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT p.PieceID, p.OrderItemID, p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedDate FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID WHERE oi.OrderID = @orderId ORDER BY p.PieceNumber, p.PieceID", reader => new OrderPieceDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5)), orderId, cancellationToken);

    public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT f.OrderItemFabricID, f.OrderItemID, f.InventoryItemID, f.FabricCode, f.FabricType, f.FabricColor, f.Quantity, f.Unit, f.UnitCost, f.TotalCost, f.ConsumedQuantity, i.YardPrice, f.CreatedDate FROM dbo.OrderItemFabrics f INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = f.OrderItemID LEFT JOIN dbo.InventoryItems i ON i.InventoryItemID = f.InventoryItemID WHERE oi.OrderID = @orderId ORDER BY f.OrderItemFabricID", reader => new OrderFabricDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("InventoryItemID"), reader.NullableString("FabricCode"), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.GetDecimal(6), reader.GetString(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.NullableDecimal("YardPrice"), reader.GetDateTime(12)), orderId, cancellationToken);

    public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT PaymentID, OrderID, InvoiceID, PaymentDate, Amount, PaymentMethod, ReferenceNo, Notes, CreatedDate, PaymentKind FROM dbo.Payments WHERE OrderID = @orderId ORDER BY PaymentDate DESC, PaymentID DESC", reader => new OrderPaymentDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("InvoiceID"), reader.GetDateTime(3), reader.GetDecimal(4), reader.NullableString("PaymentMethod"), reader.NullableString("ReferenceNo"), reader.NullableString("Notes"), reader.GetDateTime(8), reader.GetString(9)), orderId, cancellationToken);

    public async Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken)
    {
        var results = await QueryAsync("SELECT OrderID, OrderStatus, DeliveryDate FROM dbo.Orders WHERE OrderID = @orderId", reader => new OrderDeliveryDto(reader.GetInt32(0), reader.GetString(1), reader.NullableDateTime("DeliveryDate")), orderId, cancellationToken);
        return results.SingleOrDefault();
    }

    private static async Task<bool> CustomerExistsAsync(int customerId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT 1 FROM dbo.Customers WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=@customerId", connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<int> NextNumberAsync(string table, string column, string prefix, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "" : prefix.Trim();
        var sql = $"SELECT ISNULL(MAX(TRY_CONVERT(int,SUBSTRING({column},{normalizedPrefix.Length + 1},20))),0)+1 FROM {table} WITH (TABLOCKX,HOLDLOCK) WHERE {column} LIKE @prefix";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", $"{normalizedPrefix}%");
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<int> InsertOrderItemAsync(int orderId, CreateOrderItemDto item, string trackingCode, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,FabricCode,FabricType,FabricColor,Notes1,Notes2,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate) OUTPUT INSERTED.OrderItemID VALUES (@orderId,@pieceType,@quantity,@fabricCode,@fabricType,@fabricColor,@notes1,@notes2,@measurements,@tracking,N'New',@created)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@pieceType", item.PieceType!.Trim());
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        AddNullable(command, "@fabricCode", item.FabricCode);
        AddNullable(command, "@fabricType", item.FabricType);
        AddNullable(command, "@fabricColor", item.FabricColor);
        AddNullable(command, "@notes1", item.Notes1);
        AddNullable(command, "@notes2", item.Notes2);
        AddNullable(command, "@measurements", item.MeasurementSnapshot);
        command.Parameters.AddWithValue("@tracking", trackingCode);
        command.Parameters.AddWithValue("@created", now);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task InsertPieceAsync(int orderItemId, string trackingCode, int pieceNumber, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("INSERT INTO dbo.Pieces (OrderItemID,TrackingCode,PieceStatus,PieceNumber,CreatedDate) VALUES (@itemId,@tracking,N'New',@number,@created)", connection, transaction);
        command.Parameters.AddWithValue("@itemId", orderItemId);
        command.Parameters.AddWithValue("@tracking", trackingCode);
        command.Parameters.AddWithValue("@number", pieceNumber);
        command.Parameters.AddWithValue("@created", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertFabricAsync(int orderItemId, CreateOrderFabricDto fabric, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.OrderItemFabrics (OrderItemID,InventoryItemID,FabricCode,FabricType,FabricColor,Quantity,Unit,UnitCost,TotalCost,ConsumedQuantity,CreatedDate) VALUES (@itemId,@inventoryId,@code,@type,@color,@quantity,@unit,@unitCost,@totalCost,0,@created)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemId", orderItemId);
        AddNullable(command, "@inventoryId", fabric.InventoryItemId);
        AddNullable(command, "@code", fabric.FabricCode);
        AddNullable(command, "@type", fabric.FabricType);
        AddNullable(command, "@color", fabric.FabricColor);
        command.Parameters.AddWithValue("@quantity", fabric.Quantity);
        command.Parameters.AddWithValue("@unit", fabric.Unit.Trim());
        command.Parameters.AddWithValue("@unitCost", fabric.UnitCost);
        command.Parameters.AddWithValue("@totalCost", fabric.Quantity * fabric.UnitCost);
        command.Parameters.AddWithValue("@created", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAdvanceAsync(int orderId, string orderNumber, int customerId, decimal amount, string? paymentMethod, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var reference = $"{orderNumber}:Advance";
        const string paymentSql = "INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,CreatedDate,PaymentKind) VALUES (@orderId,@date,@amount,@method,@reference,@date,N'Advance')";
        await using (var payment = new SqlCommand(paymentSql, connection, transaction))
        {
            payment.Parameters.AddWithValue("@orderId", orderId);
            payment.Parameters.AddWithValue("@date", now);
            payment.Parameters.AddWithValue("@amount", amount);
            AddNullable(payment, "@method", paymentMethod);
            payment.Parameters.AddWithValue("@reference", reference);
            await payment.ExecuteNonQueryAsync(cancellationToken);
        }
        const string ledgerSql = "DECLARE @balance decimal(18,2)=ISNULL((SELECT TOP(1) BalanceAfterTransaction FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=@customerId ORDER BY CreatedAt DESC,CustomerLedgerEntryId DESC),0); INSERT INTO dbo.CustomerLedgerEntries (CustomerID,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt) VALUES (@customerId,@reference,0,@amount,@balance-@amount,@date)";
        await using var ledger = new SqlCommand(ledgerSql, connection, transaction);
        ledger.Parameters.AddWithValue("@customerId", customerId);
        ledger.Parameters.AddWithValue("@reference", reference);
        ledger.Parameters.AddWithValue("@amount", amount);
        ledger.Parameters.AddWithValue("@date", now);
        await ledger.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value is string text ? (string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim()) : value ?? DBNull.Value);

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, int? orderId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (orderId is not null) command.Parameters.AddWithValue("@orderId", orderId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }
}