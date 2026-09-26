using System.Globalization;
using System.Text.Json;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class OrderRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections, IConsumptionRulesRepository? consumptionRules = null) : IOrderRepository
{
    public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => QueryAsync("SELECT o.OrderID, o.OrderNumber, o.CustomerID, c.CustomerCode, c.CustomerName, c.PhoneNumber, o.OrderDate, o.DeliveryDate, o.TotalAmount, o.PaidAmount, o.RemainingAmount, o.UrgencyStatus, o.OrderStatus, o.SaleCategory FROM dbo.Orders o LEFT JOIN dbo.Customers c ON c.CustomerID = o.CustomerID ORDER BY o.OrderDate DESC, o.OrderID DESC", reader => new OrderListDto(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.GetDateTime(6), reader.NullableDateTime("DeliveryDate"), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11), reader.GetString(12), reader.GetString(13)), null, cancellationToken);

    public async Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT o.OrderID, o.OrderNumber, o.CustomerID, c.CustomerName, c.PhoneNumber, o.OrderDate, o.DeliveryDate, o.TotalAmount, o.DiscountAmount, o.PaidAmount, o.RemainingAmount, o.UrgencyStatus, o.OrderStatus, o.Notes, o.CreatedDate, o.UpdatedDate, o.CancellationReason, o.CancelledAt, o.CancelledBy, o.SaleCategory, o.RevenueRecognized, o.RevenueRecognizedAt, o.RevenueReversalCreated, o.RevenueReversalCreatedAt FROM dbo.Orders o LEFT JOIN dbo.Customers c ON c.CustomerID = o.CustomerID WHERE o.OrderID = @orderId";
        var results = await QueryAsync(sql, reader => new OrderDetailsDto(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.GetDateTime(5), reader.NullableDateTime("DeliveryDate"), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11), reader.GetString(12), reader.NullableString("Notes"), reader.GetDateTime(14), reader.NullableDateTime("UpdatedDate"), reader.NullableString("CancellationReason"), reader.NullableDateTime("CancelledAt"), reader.NullableString("CancelledBy"), reader.GetString(19), reader.GetBoolean(20), reader.NullableDateTime("RevenueRecognizedAt"), reader.GetBoolean(22), reader.NullableDateTime("RevenueReversalCreatedAt")), orderId, cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;
        var orderId = 0;

        try
        {
            if (!await CustomerExistsAsync(order.CustomerId, connection, transaction, cancellationToken)) return null;
            var now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(order.RequestReference))
            {
                const string existingOrderSql = "SELECT TOP(1) OrderID FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE SaleReference=@reference";
                await using var existingOrderCommand = new SqlCommand(existingOrderSql, connection, transaction);
                existingOrderCommand.Parameters.AddWithValue("@reference", order.RequestReference.Trim());
                var existingOrderId = await existingOrderCommand.ExecuteScalarAsync(cancellationToken);
                if (existingOrderId is int existingId)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return await GetByIdAsync(existingId, cancellationToken);
                }
            }
            var fabricLines = await ResolveOrderFabricLinesAsync(order.Items, cancellationToken);
            var fabricRequirements = fabricLines
				.Where(line => line is not null)
				.Select(line => line!)
                .GroupBy(line => line.FabricCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => new OrderFabricRequirement(group.Key, group.Sum(line => line.RequiredInches)))
                .ToList();
            var fabricStocks = new Dictionary<string, OrderFabricStock>(StringComparer.OrdinalIgnoreCase);
            foreach (var requirement in fabricRequirements)
            {
                var stock = await LoadOrderFabricStockAsync(connection, transaction, requirement.FabricCode, cancellationToken);
                if (stock.AvailableInches < requirement.RequiredInches)
                {
                    throw new InvalidOperationException($"الكمية المتوفرة للقماش {requirement.FabricCode} لا تكفي للطلب. المتوفر: {stock.AvailableInches:0.##} بوصة، المطلوب: {requirement.RequiredInches:0.##} بوصة.");
                }

                fabricStocks[requirement.FabricCode] = stock;
            }
            fabricLines = fabricLines
                .Select(line => line is null ? null : line with
                {
                    InventoryItemId = fabricStocks[line.FabricCode].InventoryItemId,
                    InchPrice = fabricStocks[line.FabricCode].InchPrice
                })
                .ToList();
            foreach (var productTypeId in order.Items.Select(item => item.ProductTypeId).Distinct())
            {
                if (!await ProductTypeExistsAsync(productTypeId, connection, transaction, cancellationToken))
                    throw new ArgumentException($"نوع المنتج الرسمي رقم {productTypeId} غير موجود أو غير فعال.");
            }

            var orderPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "OrderCodePrefix", "ORD-", cancellationToken);
            var orderNumber = $"{orderPrefix}{await NextNumberAsync("dbo.Orders", "OrderNumber", orderPrefix, connection, transaction, cancellationToken):D6}";
            var remainingAmount = order.TotalAmount - order.DiscountAmount - order.AdvancePayment;
            const string orderSql = "INSERT INTO dbo.Orders (OrderNumber,CustomerID,OrderDate,DeliveryDate,TotalAmount,DiscountAmount,PaidAmount,RemainingAmount,UrgencyStatus,OrderStatus,Notes,CreatedDate,SaleCategory,SaleReference) OUTPUT INSERTED.OrderID VALUES (@number,@customerId,@orderDate,@deliveryDate,@total,@discount,@paid,@remaining,@urgency,N'New',@notes,@created,@category,@requestReference)";
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
            AddNullable(orderCommand, "@requestReference", order.RequestReference?.Trim());
            orderId = (int)(await orderCommand.ExecuteScalarAsync(cancellationToken))!;

            var trackingPrefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "PieceTrackingPrefix", "TRK-", cancellationToken);
            var nextTracking = await NextNumberAsync("dbo.Pieces", "TrackingCode", trackingPrefix, connection, transaction, cancellationToken);
            for (var itemIndex = 0; itemIndex < order.Items.Count; itemIndex++)
            {
                var item = order.Items[itemIndex];
                var itemTracking = $"{trackingPrefix}{nextTracking++:D6}";
                var orderItemId = await InsertOrderItemAsync(orderId, item, itemTracking, now, connection, transaction, cancellationToken);
                for (var pieceNumber = 1; pieceNumber <= item.Quantity; pieceNumber++)
                    await InsertPieceAsync(orderItemId, $"{trackingPrefix}{nextTracking++:D6}", pieceNumber, now, connection, transaction, cancellationToken);
                if (item.Fabric is not null)
                {
                    var fabricLine = fabricLines[itemIndex];
                    if (fabricLine is null) throw new InvalidOperationException("تعذر تحديد متطلبات القماش الرسمية للبند.");
                    await InsertFabricAsync(orderItemId, item.Fabric, fabricLine, now, connection, transaction, cancellationToken);
                }
            }

            foreach (var requirement in fabricRequirements)
            {
                await DeductOrderFabricAsync(connection, transaction, orderId, orderNumber, requirement, fabricStocks[requirement.FabricCode], now, cancellationToken);
            }

            if (order.AdvancePayment > 0)
            {
                if (order.CashAccountId is not > 0)
                    throw new ArgumentException("الحساب النقدي مطلوب لتسجيل الدفعة المقدمة.");
                await InsertAdvanceAsync(orderId, orderNumber, order.CustomerId, order.AdvancePayment, order.PaymentMethod, order.CashAccountId.Value, now, connection, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }

        return orderId > 0 ? await GetByIdAsync(orderId, cancellationToken) : null;
    }

    public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync(
        "SELECT OrderItemID, OrderID, PieceType, Quantity, FabricCode, FabricType, FabricColor, Request1, Request2, Notes1, Notes2, MeasurementSnapshot, TrackingCode, PieceStatus, CreatedDate, ProductTypeId, ImportedReadyMadeProductId FROM dbo.OrderItems WHERE OrderID = @orderId ORDER BY OrderItemID",
        reader =>
        {
            var measurementSnapshot = reader.NullableString("MeasurementSnapshot");
            var specialRequest = TryExtractSnapshotValue(measurementSnapshot, "specialRequest", "SpecialRequest");
            return new OrderItemDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.NullableString("FabricCode"),
                reader.NullableString("FabricType"),
                reader.NullableString("FabricColor"),
                reader.NullableString("Request1"),
                reader.NullableString("Request2"),
                specialRequest,
                reader.NullableString("Notes1"),
                reader.NullableString("Notes2"),
                measurementSnapshot,
                reader.NullableString("TrackingCode"),
                reader.NullableString("PieceStatus"),
                reader.GetDateTime(14),
                reader.NullableInt32("ProductTypeId"),
                reader.NullableInt32("ImportedReadyMadeProductId"));
        },
        orderId,
        cancellationToken);

    public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT p.PieceID, p.OrderItemID, p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedDate FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID WHERE oi.OrderID = @orderId ORDER BY p.PieceNumber, p.PieceID", reader => new OrderPieceDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5)), orderId, cancellationToken);

    public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT f.OrderItemFabricID, f.OrderItemID, f.InventoryItemID, f.FabricCode, f.FabricType, f.FabricColor, f.Quantity, f.Unit, f.UnitCost, f.TotalCost, f.ConsumedQuantity, i.YardPrice, f.CreatedDate FROM dbo.OrderItemFabrics f INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = f.OrderItemID LEFT JOIN dbo.InventoryItems i ON i.InventoryItemID = f.InventoryItemID WHERE oi.OrderID = @orderId ORDER BY f.OrderItemFabricID", reader => new OrderFabricDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("InventoryItemID"), reader.NullableString("FabricCode"), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.GetDecimal(6), reader.GetString(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.NullableDecimal("YardPrice"), reader.GetDateTime(12)), orderId, cancellationToken);

    public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => QueryAsync("SELECT PaymentID, OrderID, InvoiceID, PaymentDate, Amount, PaymentMethod, ReferenceNo, Notes, CreatedDate, PaymentKind FROM dbo.Payments WHERE OrderID = @orderId ORDER BY PaymentDate DESC, PaymentID DESC", reader => new OrderPaymentDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("InvoiceID"), reader.GetDateTime(3), reader.GetDecimal(4), reader.NullableString("PaymentMethod"), reader.NullableString("ReferenceNo"), reader.NullableString("Notes"), reader.GetDateTime(8), reader.GetString(9)), orderId, cancellationToken);

    public async Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken)
    {
        var results = await QueryAsync("SELECT OrderID, OrderStatus, DeliveryDate FROM dbo.Orders WHERE OrderID = @orderId", reader => new OrderDeliveryDto(reader.GetInt32(0), reader.GetString(1), reader.NullableDateTime("DeliveryDate")), orderId, cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            const string selectSql = "SELECT OrderStatus FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId";
            await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
            selectCommand.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var orderStatus = reader.GetString(0);
            await reader.DisposeAsync();

            if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            if (!string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(orderStatus, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var now = DateTime.UtcNow;
            if (!string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                const string updateSql = "UPDATE dbo.Orders SET OrderStatus = N'Delivered', DeliveryDate = @deliveryDate, UpdatedDate = @updatedDate WHERE OrderID = @orderId AND OrderStatus = N'ReadyForDelivery'";
                await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@deliveryDate", now);
                updateCommand.Parameters.AddWithValue("@updatedDate", now);
                updateCommand.Parameters.AddWithValue("@orderId", orderId);
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetByIdAsync(orderId, cancellationToken);
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public async Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken)
    {
        ThrowIfBalanceWaiverUnavailable();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            const string selectSql = "SELECT OrderNumber, CustomerID, RemainingAmount, OrderStatus, RevenueRecognized FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId";
            await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
            selectCommand.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var orderNumber = reader.GetString(0);
            var customerId = reader.GetInt32(1);
            var remainingAmount = reader.GetDecimal(2);
            var orderStatus = reader.GetString(3);
            var revenueRecognized = reader.GetBoolean(4);
            await reader.DisposeAsync();

            if (!string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase) || !revenueRecognized)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            if (remainingAmount <= 0m)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return await GetByIdAsync(orderId, cancellationToken);
            }

            var now = DateTime.UtcNow;
            var reference = $"{orderNumber}:CustomerBalanceWaiver";
            const string financialSql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) SELECT @reference,@transactionType,@amount,@description,@createdAt WHERE NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @reference AND TransactionType = @transactionType)";
            await using (var financial = new SqlCommand(financialSql, connection, transaction))
            {
                financial.Parameters.AddWithValue("@reference", reference);
                financial.Parameters.AddWithValue("@transactionType", "CustomerBalanceWaiver");
                financial.Parameters.AddWithValue("@amount", remainingAmount);
                financial.Parameters.AddWithValue("@description", $"Customer balance waived as donation for {orderNumber}");
                financial.Parameters.AddWithValue("@createdAt", now);
                await financial.ExecuteNonQueryAsync(cancellationToken);
            }

            const string ledgerSql = "DECLARE @balance decimal(18,2)=ISNULL((SELECT TOP(1) BalanceAfterTransaction FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=@customerId ORDER BY CreatedAt DESC,CustomerLedgerEntryId DESC),0); INSERT INTO dbo.CustomerLedgerEntries (CustomerID,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt) SELECT @customerId,@reference,0,@amount,@balance-@amount,@date WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber=@reference)";
            await using (var ledger = new SqlCommand(ledgerSql, connection, transaction))
            {
                ledger.Parameters.AddWithValue("@customerId", customerId);
                ledger.Parameters.AddWithValue("@reference", reference);
                ledger.Parameters.AddWithValue("@amount", remainingAmount);
                ledger.Parameters.AddWithValue("@date", now);
                await ledger.ExecuteNonQueryAsync(cancellationToken);
            }

            const string orderUpdateSql = "UPDATE dbo.Orders SET RemainingAmount = 0, UpdatedDate = @updatedDate WHERE OrderID = @orderId AND RemainingAmount > 0";
            await using (var update = new SqlCommand(orderUpdateSql, connection, transaction))
            {
                update.Parameters.AddWithValue("@updatedDate", now);
                update.Parameters.AddWithValue("@orderId", orderId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetByIdAsync(orderId, cancellationToken);
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) =>
        SettleCustomerBalanceCoreAsync(orderId, amount, 0m, paymentMethod, cashAccountId, referenceNumber, notes, requireDelivered: false, cancellationToken);

    public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) =>
        SettleCustomerBalanceCoreAsync(orderId, amount, discountAmount, paymentMethod, cashAccountId, referenceNumber, notes, requireDelivered: true, cancellationToken);

    private async Task<OrderDetailsDto?> SettleCustomerBalanceCoreAsync(int orderId, decimal amount, decimal settlementDiscount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, bool requireDelivered, CancellationToken cancellationToken)
    {
        if (settlementDiscount > 0m)
            throw new InvalidOperationException("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.");

        if (amount < 0m || settlementDiscount < 0m || amount + settlementDiscount <= 0m)
        {
            return null;
        }
        if (amount > 0m && cashAccountId is not > 0)
            throw new ArgumentException("الحساب النقدي مطلوب لتسجيل التحصيل.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            const string selectSql = "SELECT OrderNumber, CustomerID, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount, OrderStatus, DeliveryDate, RevenueRecognized FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId";
            await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
            selectCommand.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var orderNumber = reader.GetString(0);
            var customerId = reader.GetInt32(1);
            _ = reader.GetDecimal(2);
            _ = reader.GetDecimal(3);
            var currentPaid = reader.GetDecimal(4);
            var currentRemaining = reader.GetDecimal(5);
            var orderStatus = reader.GetString(6);
            DateTime? deliveryDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7);
            var revenueRecognized = reader.GetBoolean(8);
            await reader.DisposeAsync();

            var isDelivered = string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase);
            if (requireDelivered && (!isDelivered || !revenueRecognized))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            if (currentRemaining <= 0m)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return await GetByIdAsync(orderId, cancellationToken);
            }

            if (amount + settlementDiscount > currentRemaining)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var isReceivableCollection = isDelivered && revenueRecognized;
            var paymentKind = isReceivableCollection ? "DebtCollection" : "Advance";
            var accountingEventType = isReceivableCollection
                ? AccountingEventType.CustomerPayment
                : AccountingEventType.CustomerAdvance;
            var normalizedReference = string.IsNullOrWhiteSpace(referenceNumber) ? $"{orderNumber}:{(requireDelivered ? "Settlement" : paymentKind)}:{DateTime.UtcNow:yyyyMMddHHmmssfff}" : referenceNumber.Trim();
            var normalizedMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod.Trim();
            var discountReference = $"{normalizedReference}:Discount";
            var paymentDuplicateExists = amount > 0m && await PaymentExistsAsync(connection, transaction, orderId, normalizedReference, paymentKind, cancellationToken);
            var discountDuplicateExists = settlementDiscount > 0m && await FinancialTransactionExistsAsync(connection, transaction, discountReference, "CustomerSettlementDiscount", cancellationToken);
            if (paymentDuplicateExists || discountDuplicateExists)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var paymentDate = DateTime.UtcNow;
            int? paymentId = null;
            if (amount > 0m)
            {
                const string paymentInsertSql = "INSERT INTO dbo.Payments (OrderID, PaymentDate, Amount, PaymentMethod, ReferenceNo, Notes, CreatedDate, PaymentKind) OUTPUT INSERTED.PaymentID VALUES (@orderId, @date, @amount, @method, @reference, @notes, @date, @paymentKind)";
                await using (var paymentInsert = new SqlCommand(paymentInsertSql, connection, transaction))
                {
                    paymentInsert.Parameters.AddWithValue("@orderId", orderId);
                    paymentInsert.Parameters.AddWithValue("@date", paymentDate);
                    paymentInsert.Parameters.AddWithValue("@amount", amount);
                    paymentInsert.Parameters.AddWithValue("@method", normalizedMethod);
                    paymentInsert.Parameters.AddWithValue("@reference", normalizedReference);
                    paymentInsert.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
                    paymentInsert.Parameters.AddWithValue("@paymentKind", paymentKind);
                    paymentId = Convert.ToInt32(await paymentInsert.ExecuteScalarAsync(cancellationToken));
                }

                await InsertCustomerLedgerCreditAsync(connection, transaction, customerId, normalizedReference, amount, paymentDate, cancellationToken);
            }

            if (amount > 0m)
            {
                var financialReference = normalizedReference;
                var description = isReceivableCollection ? $"Customer payment collected for {orderNumber}" : $"Customer advance collected for {orderNumber}";
                var paymentPosting = await AccountingEventPostingGateway.PostAsync(
                    connection,
                    transaction,
                    accountingEventType,
                    amount,
                    paymentId,
                    null,
                    null,
                    null,
                    financialReference,
                    description,
                    cancellationToken);
                await CashMovementPostingGateway.PostCashInAsync(connection, transaction, paymentPosting.AccountingEventId, cashAccountId!.Value, amount, cancellationToken);
            }

            if (settlementDiscount > 0m)
            {
                const string discountInsertSql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) VALUES (@reference,@transactionType,@amount,@description,@createdAt)";
                await using (var discount = new SqlCommand(discountInsertSql, connection, transaction))
                {
                    discount.Parameters.AddWithValue("@reference", discountReference);
                    discount.Parameters.AddWithValue("@transactionType", "CustomerSettlementDiscount");
                    discount.Parameters.AddWithValue("@amount", settlementDiscount);
                    discount.Parameters.AddWithValue("@description", $"Settlement discount for {orderNumber}");
                    discount.Parameters.AddWithValue("@createdAt", paymentDate);
                    await discount.ExecuteNonQueryAsync(cancellationToken);
                }

                await InsertCustomerLedgerCreditAsync(connection, transaction, customerId, discountReference, settlementDiscount, paymentDate, cancellationToken);
            }

            var updatedPaid = currentPaid + amount;
            var updatedRemaining = currentRemaining - amount - settlementDiscount;
            const string orderUpdateSql = "UPDATE dbo.Orders SET PaidAmount = @paidAmount, RemainingAmount = @remainingAmount, UpdatedDate = @updatedAt WHERE OrderID = @orderId";
            await using (var update = new SqlCommand(orderUpdateSql, connection, transaction))
            {
                update.Parameters.AddWithValue("@paidAmount", updatedPaid);
                update.Parameters.AddWithValue("@remainingAmount", updatedRemaining);
                update.Parameters.AddWithValue("@updatedAt", paymentDate);
                update.Parameters.AddWithValue("@orderId", orderId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetByIdAsync(orderId, cancellationToken);
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore.
                }
            }
            throw;
        }
    }

    public async Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            const string selectSql = "SELECT OrderNumber, DeliveryDate, OrderStatus, TotalAmount, DiscountAmount, RevenueRecognized FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId";
            await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
            selectCommand.Parameters.AddWithValue("@orderId", orderId);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var orderNumber = reader.GetString(0);
            DateTime? deliveryDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            var orderStatus = reader.GetString(2);
            var totalAmount = reader.GetDecimal(3);
            var discountAmount = reader.GetDecimal(4);
            var revenueRecognized = reader.GetBoolean(5);
            var isDelivered = string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase);

            await reader.DisposeAsync();

            if (!isDelivered)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return await GetByIdAsync(orderId, cancellationToken);
            }

            var now = DateTime.UtcNow;
            if (!revenueRecognized)
            {
                var revenueReference = $"{orderNumber}:RevenueRecognized";
                var revenueAmount = DeliveryRevenueRecognitionResolver.ResolveRevenueAmount(totalAmount, discountAmount);
                await AccountingEventPostingGateway.PostAsync(
                    connection,
                    transaction,
                    AccountingEventType.RevenueRecognizedOrder,
                    revenueAmount,
                    null,
                    orderId,
                    null,
                    null,
                    revenueReference,
                    $"Delivery revenue for {orderNumber}",
                    cancellationToken);

                const string updateSql = "UPDATE dbo.Orders SET RevenueRecognized = 1, RevenueRecognizedAt = @recognizedAt, UpdatedDate = @updatedAt WHERE OrderID = @orderId AND RevenueRecognized = 0";
                await using (var update = new SqlCommand(updateSql, connection, transaction))
                {
                    update.Parameters.AddWithValue("@recognizedAt", now);
                    update.Parameters.AddWithValue("@updatedAt", now);
                    update.Parameters.AddWithValue("@orderId", orderId);
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetByIdAsync(orderId, cancellationToken);
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }
    }

    public async Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var snapshot = await ReadCancellationSnapshotAsync(connection, transaction, orderId, cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var refundRequested = OrderCancellationFinancialMovementResolver.ShouldCreateRefund(snapshot.PaidAmount, !string.IsNullOrWhiteSpace(snapshot.CancellationReason) && string.Equals(snapshot.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase));
            var reversalRequested = OrderCancellationFinancialMovementResolver.ShouldCreateRevenueReversal(snapshot.RevenueRecognized, snapshot.RevenueReversalCreated, snapshot.OrderStatus);
            var now = DateTime.UtcNow;

            if (refundRequested || reversalRequested)
                throw new InvalidOperationException("هذه العملية غير متاحة حتى اكتمال عقد الربط المحاسبي.");

            if (await CanReverseTailoringFabricAsync(connection, transaction, orderId, cancellationToken))
            {
                await ReverseTailoringFabricAsync(connection, transaction, orderId, snapshot.OrderNumber, now, cancellationToken);
            }

            if (refundRequested)
            {
                var refundReference = $"{snapshot.OrderNumber}:OrderCancellationRefund";
                var refundAmount = OrderCancellationFinancialMovementResolver.ResolveRefundAmount(snapshot.PaidAmount);
                const string refundSql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) SELECT @reference,@transactionType,@amount,@description,@createdAt WHERE NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @reference AND TransactionType = N'OrderCancellationRefund')";
                await using (var financial = new SqlCommand(refundSql, connection, transaction))
                {
                    financial.Parameters.AddWithValue("@reference", refundReference);
                    financial.Parameters.AddWithValue("@transactionType", "OrderCancellationRefund");
                    financial.Parameters.AddWithValue("@amount", refundAmount);
                    financial.Parameters.AddWithValue("@description", "Refund issued on order cancellation");
                    financial.Parameters.AddWithValue("@createdAt", now);
                    await financial.ExecuteNonQueryAsync(cancellationToken);
                }

                await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                    connection,
                    transaction,
                    refundReference,
                    "OrderCancellationRefund",
                    refundAmount,
                    "Refund issued on order cancellation",
                    cancellationToken);

                const string paymentInsertSql = "INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,CreatedDate,PaymentKind) SELECT @orderId,@date,@amount,@method,@reference,@date,N'Refund' WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId AND ReferenceNo = @reference AND PaymentKind = N'Refund')";
                await using (var payment = new SqlCommand(paymentInsertSql, connection, transaction))
                {
                    payment.Parameters.AddWithValue("@orderId", orderId);
                    payment.Parameters.AddWithValue("@date", now);
                    payment.Parameters.AddWithValue("@amount", refundAmount);
                    payment.Parameters.AddWithValue("@method", "Refund");
                    payment.Parameters.AddWithValue("@reference", refundReference);
                    await payment.ExecuteNonQueryAsync(cancellationToken);
                }

                const string ledgerSql = "DECLARE @balance decimal(18,2)=ISNULL((SELECT TOP(1) BalanceAfterTransaction FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=(SELECT CustomerID FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID=@orderId) ORDER BY CreatedAt DESC,CustomerLedgerEntryId DESC),0); INSERT INTO dbo.CustomerLedgerEntries (CustomerID,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt) SELECT CustomerID,@reference,@amount,0,@balance+@amount,@date FROM dbo.Orders WHERE OrderID=@orderId AND NOT EXISTS (SELECT 1 FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber=@reference)";
                await using (var ledger = new SqlCommand(ledgerSql, connection, transaction))
                {
                    ledger.Parameters.AddWithValue("@orderId", orderId);
                    ledger.Parameters.AddWithValue("@reference", refundReference);
                    ledger.Parameters.AddWithValue("@amount", refundAmount);
                    ledger.Parameters.AddWithValue("@date", now);
                    await ledger.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (reversalRequested)
            {
                var reversalReference = $"{snapshot.OrderNumber}:RevenueReversal";
                var reversalAmount = Math.Max(0m, snapshot.TotalAmount - 0m);
                const string reversalSql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber,TransactionType,Amount,Description,CreatedAt) SELECT @reference,@transactionType,@amount,@description,@createdAt WHERE NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @reference AND TransactionType = N'RevenueReversal')";
                await using (var financial = new SqlCommand(reversalSql, connection, transaction))
                {
                    financial.Parameters.AddWithValue("@reference", reversalReference);
                    financial.Parameters.AddWithValue("@transactionType", "RevenueReversal");
                    financial.Parameters.AddWithValue("@amount", reversalAmount);
                    financial.Parameters.AddWithValue("@description", "Revenue reversed on order cancellation");
                    financial.Parameters.AddWithValue("@createdAt", now);
                    await financial.ExecuteNonQueryAsync(cancellationToken);
                }

                await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                    connection,
                    transaction,
                    reversalReference,
                    "RevenueReversal",
                    reversalAmount,
                    "Revenue reversed on order cancellation",
                    cancellationToken);
            }

            const string updateSql = "UPDATE dbo.Orders SET OrderStatus = N'Cancelled', CancellationReason = @reason, CancelledAt = @cancelledAt, CancelledBy = @cancelledBy, UpdatedDate = @updatedAt, RevenueReversalCreated = CASE WHEN RevenueReversalCreated = 1 THEN 1 ELSE @reversalFlag END, RevenueReversalCreatedAt = CASE WHEN RevenueReversalCreatedAt IS NOT NULL THEN RevenueReversalCreatedAt ELSE @reversalCreatedAt END WHERE OrderID = @orderId";
            await using (var update = new SqlCommand(updateSql, connection, transaction))
            {
                update.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason.Trim());
                update.Parameters.AddWithValue("@cancelledAt", now);
                update.Parameters.AddWithValue("@cancelledBy", string.IsNullOrWhiteSpace(cancelledBy) ? (object)DBNull.Value : cancelledBy.Trim());
                update.Parameters.AddWithValue("@updatedAt", now);
                update.Parameters.AddWithValue("@reversalFlag", reversalRequested ? 1 : 0);
                update.Parameters.AddWithValue("@reversalCreatedAt", reversalRequested ? now : DBNull.Value);
                update.Parameters.AddWithValue("@orderId", orderId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch
        {
            if (ShouldRollbackAfterFailure(committed, transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open))
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore rollback failures after the original exception is already active.
                }
            }
            throw;
        }

        return await GetByIdAsync(orderId, cancellationToken);
    }

    public static bool ShouldRollbackAfterFailure(bool committed, bool transactionUsable)
        => !committed && transactionUsable;

    private async Task<IReadOnlyList<OrderFabricLine?>> ResolveOrderFabricLinesAsync(
        IReadOnlyList<CreateOrderItemDto> items,
        CancellationToken cancellationToken)
    {
        var result = new List<OrderFabricLine?>(items.Count);
        foreach (var item in items)
        {
            if (item.Fabric is null)
            {
                result.Add(null);
                continue;
            }

            if (consumptionRules is null)
                throw new InvalidOperationException("خدمة قواعد استهلاك القماش غير مهيأة.");

            var fabricCode = item.Fabric.FabricCode?.Trim();
            if (string.IsNullOrWhiteSpace(fabricCode))
                throw new InvalidOperationException("كود القماش مطلوب عند تسجيل استهلاك القماش.");

            var evaluation = await consumptionRules.EvaluateAsync(
                new EvaluateConsumptionRequestDto(item.ProductTypeId, ParseMeasurementSnapshot(item.MeasurementSnapshot)),
                cancellationToken);
            if (evaluation is null || !IsInchUnit(evaluation.Unit))
                throw new InvalidOperationException("تعذر حساب استهلاك القماش الرسمي بالبوصة لهذا البند.");

            var requiredInches = evaluation.Value * item.Quantity;
            if (requiredInches <= 0m)
                throw new InvalidOperationException("كمية استهلاك القماش الرسمية غير صالحة.");

            result.Add(new OrderFabricLine(fabricCode.ToUpperInvariant(), requiredInches, null, 0m));
        }

        return result;
    }

    private static Dictionary<string, decimal> ParseMeasurementSnapshot(string? snapshot)
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(snapshot)) return values;
        try
        {
            using var document = JsonDocument.Parse(snapshot);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return values;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith('_')) continue;
                var raw = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value >= 0m)
                    values[property.Name] = value;
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("بيانات المقاسات المحفوظة غير صالحة.");
        }

        return values;
    }

    private static bool IsInchUnit(string? unit) =>
        string.Equals(unit?.Trim(), "Inch", StringComparison.OrdinalIgnoreCase)
        || string.Equals(unit?.Trim(), "Inches", StringComparison.OrdinalIgnoreCase)
        || string.Equals(unit?.Trim(), "بوصة", StringComparison.OrdinalIgnoreCase);

    private static async Task<OrderFabricStock> LoadOrderFabricStockAsync(SqlConnection connection, SqlTransaction transaction, string fabricCode, CancellationToken cancellationToken)
    {
        int? legacyCode = int.TryParse(fabricCode, out var parsedCode) ? parsedCode : null;
        decimal legacyAvailableInches = decimal.MaxValue;
        decimal legacyFactor = 36m;
        if (legacyCode is not null)
        {
            const string legacySql = "SELECT TOP(1) Unit, AvailableQuantity FROM dbo.Fabrics_Inventory WITH (UPDLOCK,HOLDLOCK) WHERE FabricCode=@fabricCode";
            await using var legacy = new SqlCommand(legacySql, connection, transaction);
            legacy.Parameters.AddWithValue("@fabricCode", legacyCode.Value);
            await using var reader = await legacy.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                legacyFactor = StorageUnitToInches(reader.NullableString("Unit"));
                legacyAvailableInches = (reader.NullableDecimal("AvailableQuantity") ?? 0m) * legacyFactor;
            }
            await reader.CloseAsync();
        }

        const string itemSql = "SELECT TOP(1) InventoryItemID, Unit, AvailableQuantity, InchPrice, YardPrice FROM dbo.InventoryItems WITH (UPDLOCK,HOLDLOCK) WHERE ItemCode=@fabricCode AND IsActive=1";
        await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
        itemCommand.Parameters.AddWithValue("@fabricCode", fabricCode);
        await using var itemReader = await itemCommand.ExecuteReaderAsync(cancellationToken);
        if (!await itemReader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"كود القماش {fabricCode} غير مرتبط بسجل مخزون فعال.");

        var inventoryItemId = itemReader.GetInt32(0);
        var unit = itemReader.NullableString("Unit") ?? "Yard";
        var factor = StorageUnitToInches(unit);
        var available = (itemReader.NullableDecimal("AvailableQuantity") ?? 0m) * factor;
        var inchPrice = itemReader.NullableDecimal("InchPrice") ?? ((itemReader.NullableDecimal("YardPrice") ?? 0m) / 36m);
        await itemReader.CloseAsync();
        return new OrderFabricStock(fabricCode, legacyCode, inventoryItemId, factor, Math.Min(legacyAvailableInches, available), inchPrice, legacyFactor);
    }

    private static decimal StorageUnitToInches(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "inch" or "inches" or "بوصة" => 1m,
        _ => 36m,
    };

    private static async Task DeductOrderFabricAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string orderNumber, OrderFabricRequirement requirement, OrderFabricStock stock, DateTime now, CancellationToken cancellationToken)
    {
        if (stock.AvailableInches < requirement.RequiredInches)
            throw new InvalidOperationException($"الكمية المتوفرة للقماش {requirement.FabricCode} لا تكفي للطلب.");

        if (stock.LegacyFabricCode is not null)
        {
            const string legacySql = "UPDATE dbo.Fabrics_Inventory SET AvailableQuantity=AvailableQuantity-@quantity, UsedQuantity=ISNULL(UsedQuantity,0)+@quantity WHERE FabricCode=@fabricCode AND AvailableQuantity>=@quantity";
            await using var legacy = new SqlCommand(legacySql, connection, transaction);
            legacy.Parameters.AddWithValue("@fabricCode", stock.LegacyFabricCode.Value);
            legacy.Parameters.AddWithValue("@quantity", requirement.RequiredInches / stock.LegacyUnitFactor);
            if (await legacy.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("تعذر خصم القماش من السجل الرسمي.");
        }

        const string inventorySql = "UPDATE dbo.InventoryItems SET CurrentQuantity=CurrentQuantity-@quantity, AvailableQuantity=AvailableQuantity-@quantity, UpdatedAt=@updatedAt WHERE InventoryItemID=@inventoryItemId AND AvailableQuantity>=@quantity";
        await using var inventory = new SqlCommand(inventorySql, connection, transaction);
        inventory.Parameters.AddWithValue("@quantity", requirement.RequiredInches / stock.InventoryUnitFactor);
        inventory.Parameters.AddWithValue("@updatedAt", now);
        inventory.Parameters.AddWithValue("@inventoryItemId", stock.InventoryItemId);
        if (await inventory.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("تعذر خصم القماش من المخزون التشغيلي.");

        const string movementSql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber=@reference AND TransactionType=N'TailoringFabricConsumption')
            INSERT INTO dbo.InventoryTransactions (InventoryItemID,TransactionType,Quantity,ReferenceNumber,Notes,CreatedAt,TotalCostImpact,UnitCost)
            VALUES (@inventoryItemId,N'TailoringFabricConsumption',@quantity,@reference,@notes,@createdAt,@totalCost,@unitCost);";
        await using var movement = new SqlCommand(movementSql, connection, transaction);
        movement.Parameters.AddWithValue("@inventoryItemId", stock.InventoryItemId);
        movement.Parameters.AddWithValue("@quantity", requirement.RequiredInches / stock.InventoryUnitFactor);
        movement.Parameters.AddWithValue("@reference", $"{orderNumber}:Fabric:{requirement.FabricCode}");
        movement.Parameters.AddWithValue("@notes", $"OrderId:{orderId}|خصم قماش تفصيل|الكمية بالبوصة:{requirement.RequiredInches:0.##}");
        movement.Parameters.AddWithValue("@createdAt", now);
        movement.Parameters.AddWithValue("@totalCost", requirement.RequiredInches * stock.InchPrice);
        movement.Parameters.AddWithValue("@unitCost", stock.InchPrice);
        await movement.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? TryExtractSnapshotValue(string? measurementSnapshot, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(measurementSnapshot)) return null;

        try
        {
            using var document = JsonDocument.Parse(measurementSnapshot);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            foreach (var key in keys)
            {
                if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind is not JsonValueKind.Null)
                {
                    return value.ToString();
                }
            }
        }
        catch
        {
            // Ignore malformed measurement snapshots.
        }

        return null;
    }

    private static async Task<bool> FinancialTransactionExistsAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber, string transactionType, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @referenceNumber AND TransactionType = @transactionType) THEN 1 ELSE 0 END";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int intValue ? intValue == 1 : result is bool boolValue && boolValue;
    }

    private static async Task<bool> PaymentExistsAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string referenceNumber, string paymentKind, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId AND ReferenceNo = @referenceNumber AND PaymentKind = @paymentKind) THEN 1 ELSE 0 END";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        command.Parameters.AddWithValue("@paymentKind", paymentKind);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int intValue ? intValue == 1 : result is bool boolValue && boolValue;
    }

    private static async Task InsertCustomerLedgerCreditAsync(SqlConnection connection, SqlTransaction transaction, int customerId, string referenceNumber, decimal amount, DateTime createdAt, CancellationToken cancellationToken)
    {
        const string sql = "DECLARE @balance decimal(18,2)=ISNULL((SELECT TOP(1) BalanceAfterTransaction FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=@customerId ORDER BY CreatedAt DESC,CustomerLedgerEntryId DESC),0); INSERT INTO dbo.CustomerLedgerEntries (CustomerID,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt) VALUES (@customerId,@reference,0,@amount,@balance-@amount,@date)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@reference", referenceNumber);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@date", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OrderCancellationSnapshot?> ReadCancellationSnapshotAsync(SqlConnection connection, SqlTransaction transaction, int orderId, CancellationToken cancellationToken)
    {
        const string selectSql = "SELECT OrderNumber, TotalAmount, PaidAmount, RevenueRecognized, RevenueReversalCreated, OrderStatus, CancellationReason FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @orderId";
        await using var selectCommand = new SqlCommand(selectSql, connection, transaction);
        selectCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OrderCancellationSnapshot(
            reader.GetString(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetBoolean(3),
            reader.GetBoolean(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private sealed record OrderFabricLine(string FabricCode, decimal RequiredInches, int? InventoryItemId, decimal InchPrice);
    private sealed record OrderFabricRequirement(string FabricCode, decimal RequiredInches);
    private sealed record OrderFabricStock(string FabricCode, int? LegacyFabricCode, int InventoryItemId, decimal InventoryUnitFactor, decimal AvailableInches, decimal InchPrice, decimal LegacyUnitFactor);
    private sealed record OrderCancellationSnapshot(string OrderNumber, decimal TotalAmount, decimal PaidAmount, bool RevenueRecognized, bool RevenueReversalCreated, string OrderStatus, string? CancellationReason);

    private static async Task<bool> CanReverseTailoringFabricAsync(SqlConnection connection, SqlTransaction transaction, int orderId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Pieces p
                INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID
                WHERE oi.OrderID=@orderId
                  AND p.PieceStatus NOT IN (N'New',N'Printing')
            ) OR EXISTS (
                SELECT 1
                FROM dbo.TrackingEvents te
                INNER JOIN dbo.Pieces p ON p.PieceID=te.PieceID
                INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID
                WHERE oi.OrderID=@orderId
                  AND te.IsReverted=0
                  AND te.Stage<>N'Printing'
            ) THEN 0 ELSE 1 END;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task ReverseTailoringFabricAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string orderNumber, DateTime now, CancellationToken cancellationToken)
    {
        const string linesSql = @"
            SELECT f.InventoryItemID, f.FabricCode, SUM(f.Quantity), MAX(f.Unit), MAX(f.UnitCost)
            FROM dbo.OrderItemFabrics f
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=f.OrderItemID
            WHERE oi.OrderID=@orderId
            GROUP BY f.InventoryItemID, f.FabricCode;";
        await using var linesCommand = new SqlCommand(linesSql, connection, transaction);
        linesCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await linesCommand.ExecuteReaderAsync(cancellationToken);
        var lines = new List<(int? InventoryItemId, string FabricCode, decimal QuantityInches, string Unit, decimal UnitCost)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add((reader.NullableInt32("InventoryItemID"), reader.NullableString("FabricCode") ?? string.Empty, reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4)));
        }
        await reader.CloseAsync();

        foreach (var line in lines.Where(item => !string.IsNullOrWhiteSpace(item.FabricCode)))
        {
            var reference = $"{orderNumber}:FabricReversal:{line.FabricCode.Trim().ToUpperInvariant()}";
            var consumptionReference = $"{orderNumber}:Fabric:{line.FabricCode.Trim().ToUpperInvariant()}";
            await using var consumptionExistsCommand = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.InventoryTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber=@reference AND TransactionType=N'TailoringFabricConsumption'",
                connection,
                transaction);
            consumptionExistsCommand.Parameters.AddWithValue("@reference", consumptionReference);
            if (Convert.ToInt32(await consumptionExistsCommand.ExecuteScalarAsync(cancellationToken)) == 0) continue;

            await using var existsCommand = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.InventoryTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber=@reference AND TransactionType=N'TailoringFabricReversal'",
                connection,
                transaction);
            existsCommand.Parameters.AddWithValue("@reference", reference);
            if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) > 0) continue;

            var inventoryItemId = line.InventoryItemId;
            if (inventoryItemId is null)
            {
                const string findItemSql = "SELECT TOP(1) InventoryItemID FROM dbo.InventoryItems WITH (UPDLOCK,HOLDLOCK) WHERE ItemCode=@code AND IsActive=1";
                await using var findItem = new SqlCommand(findItemSql, connection, transaction);
                findItem.Parameters.AddWithValue("@code", line.FabricCode.Trim());
                var value = await findItem.ExecuteScalarAsync(cancellationToken);
                inventoryItemId = value is int id ? id : null;
            }

            if (inventoryItemId is null) throw new InvalidOperationException($"لا يوجد سجل مخزون لعكس قماش {line.FabricCode}.");
            var stock = await ReadInventoryUnitAsync(connection, transaction, inventoryItemId.Value, cancellationToken);
            var inventoryQuantity = line.QuantityInches / StorageUnitToInches(stock.Unit);

            const string inventorySql = "UPDATE dbo.InventoryItems SET CurrentQuantity=CurrentQuantity+@quantity, AvailableQuantity=AvailableQuantity+@quantity, UpdatedAt=@updatedAt WHERE InventoryItemID=@id";
            await using var inventory = new SqlCommand(inventorySql, connection, transaction);
            inventory.Parameters.AddWithValue("@quantity", inventoryQuantity);
            inventory.Parameters.AddWithValue("@updatedAt", now);
            inventory.Parameters.AddWithValue("@id", inventoryItemId.Value);
            await inventory.ExecuteNonQueryAsync(cancellationToken);

            if (int.TryParse(line.FabricCode.Trim(), out var legacyCode))
            {
                const string legacySql = "UPDATE dbo.Fabrics_Inventory SET AvailableQuantity=AvailableQuantity+@quantity, UsedQuantity=CASE WHEN ISNULL(UsedQuantity,0)>=@quantity THEN ISNULL(UsedQuantity,0)-@quantity ELSE 0 END WHERE FabricCode=@code";
                await using var legacy = new SqlCommand(legacySql, connection, transaction);
                legacy.Parameters.AddWithValue("@quantity", line.QuantityInches / stock.LegacyUnitFactor);
                legacy.Parameters.AddWithValue("@code", legacyCode);
                await legacy.ExecuteNonQueryAsync(cancellationToken);
            }

            const string movementSql = @"
                INSERT INTO dbo.InventoryTransactions
                    (InventoryItemID,TransactionType,Quantity,ReferenceNumber,Notes,CreatedAt,TotalCostImpact,UnitCost)
                VALUES (@id,N'TailoringFabricReversal',@quantity,@reference,@notes,@createdAt,@cost,@unitCost);";
            await using var movement = new SqlCommand(movementSql, connection, transaction);
            movement.Parameters.AddWithValue("@id", inventoryItemId.Value);
            movement.Parameters.AddWithValue("@quantity", inventoryQuantity);
            movement.Parameters.AddWithValue("@reference", reference);
            movement.Parameters.AddWithValue("@notes", $"OrderId:{orderId}|عكس خصم قماش قبل نقطة اللاعودة");
            movement.Parameters.AddWithValue("@createdAt", now);
            movement.Parameters.AddWithValue("@cost", line.QuantityInches * line.UnitCost);
            movement.Parameters.AddWithValue("@unitCost", stock.InchPrice);
            await movement.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<(string Unit, decimal LegacyUnitFactor, decimal InchPrice)> ReadInventoryUnitAsync(SqlConnection connection, SqlTransaction transaction, int inventoryItemId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT Unit, InchPrice, YardPrice FROM dbo.InventoryItems WITH (UPDLOCK,HOLDLOCK) WHERE InventoryItemID=@id";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", inventoryItemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("سجل المخزون غير موجود.");
        var unit = reader.NullableString("Unit") ?? "Yard";
        var inchPrice = reader.NullableDecimal("InchPrice") ?? ((reader.NullableDecimal("YardPrice") ?? 0m) / 36m);
        await reader.CloseAsync();
        return (unit, StorageUnitToInches(unit), inchPrice);
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
        const string sql = "INSERT INTO dbo.OrderItems (OrderID,PieceType,Quantity,FabricCode,FabricType,FabricColor,Request1,Request2,Notes1,Notes2,MeasurementSnapshot,TrackingCode,PieceStatus,CreatedDate,ProductTypeId,ImportedReadyMadeProductId) OUTPUT INSERTED.OrderItemID VALUES (@orderId,@pieceType,@quantity,@fabricCode,@fabricType,@fabricColor,@request1,@request2,@notes1,@notes2,@measurements,@tracking,N'New',@created,@productTypeId,@importedReadyMadeProductId)";
        var request1 = item.Request1;
        var request2 = item.Request2;
        var mergedMeasurements = MergeMeasurementSnapshot(item.MeasurementSnapshot, item, request1, request2);
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@pieceType", item.PieceType!.Trim());
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        AddNullable(command, "@fabricCode", item.FabricCode);
        AddNullable(command, "@fabricType", item.FabricType);
        AddNullable(command, "@fabricColor", item.FabricColor);
        AddNullable(command, "@request1", request1);
        AddNullable(command, "@request2", request2);
        AddNullable(command, "@notes1", item.Notes1);
        AddNullable(command, "@notes2", item.Notes2);
        command.Parameters.AddWithValue("@productTypeId", item.ProductTypeId);
        AddNullable(command, "@importedReadyMadeProductId", item.ImportedReadyMadeProductId);
        AddNullable(command, "@measurements", mergedMeasurements);
        command.Parameters.AddWithValue("@tracking", trackingCode);
        command.Parameters.AddWithValue("@created", now);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<bool> ProductTypeExistsAsync(int productTypeId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.PricingProductTypes WITH (UPDLOCK, HOLDLOCK) WHERE ProductTypeId=@productTypeId AND IsActive=1", connection, transaction);
        command.Parameters.AddWithValue("@productTypeId", productTypeId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static string? MergeMeasurementSnapshot(string? originalSnapshot, CreateOrderItemDto item, string? request1, string? request2)
    {
        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(originalSnapshot))
        {
            try
            {
                using var document = JsonDocument.Parse(originalSnapshot);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        merged[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => property.Value.TryGetDecimal(out var decimalValue) ? decimalValue : property.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => property.Value.ToString(),
                        };
                    }
                }
            }
            catch
            {
                // Ignore malformed measurement snapshots and replace with a normalized record.
            }
        }

        if (!string.IsNullOrWhiteSpace(item.CatalogNumber)) merged["_catalogNumber"] = item.CatalogNumber.Trim();
        if (item.Consumption is not null) merged["_consumption"] = item.Consumption.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(item.ConsumptionUnit)) merged["_consumptionUnit"] = item.ConsumptionUnit.Trim();
        if (!string.IsNullOrWhiteSpace(item.FabricCode)) merged["fabricCode"] = item.FabricCode.Trim();
        if (!string.IsNullOrWhiteSpace(item.FabricType)) merged["fabricType"] = item.FabricType.Trim();
        if (!string.IsNullOrWhiteSpace(item.FabricColor)) merged["fabricColor"] = item.FabricColor.Trim();
        if (!string.IsNullOrWhiteSpace(request1)) merged["request1"] = request1.Trim();
        if (!string.IsNullOrWhiteSpace(request2)) merged["request2"] = request2.Trim();
        if (!string.IsNullOrWhiteSpace(item.SpecialRequest)) merged["specialRequest"] = item.SpecialRequest.Trim();
        if (!string.IsNullOrWhiteSpace(item.Notes1)) merged["notes1"] = item.Notes1.Trim();
        if (!string.IsNullOrWhiteSpace(item.Notes2)) merged["notes2"] = item.Notes2.Trim();

        return merged.Count == 0 ? originalSnapshot : JsonSerializer.Serialize(merged);
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

    private static async Task InsertFabricAsync(int orderItemId, CreateOrderFabricDto fabric, OrderFabricLine fabricLine, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.OrderItemFabrics (OrderItemID,InventoryItemID,FabricCode,FabricType,FabricColor,Quantity,Unit,UnitCost,TotalCost,ConsumedQuantity,CreatedDate) VALUES (@itemId,@inventoryId,@code,@type,@color,@quantity,@unit,@unitCost,@totalCost,0,@created)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemId", orderItemId);
        AddNullable(command, "@inventoryId", fabricLine.InventoryItemId);
        AddNullable(command, "@code", fabric.FabricCode);
        AddNullable(command, "@type", fabric.FabricType);
        AddNullable(command, "@color", fabric.FabricColor);
        command.Parameters.AddWithValue("@quantity", fabricLine.RequiredInches);
        command.Parameters.AddWithValue("@unit", "Inch");
        command.Parameters.AddWithValue("@unitCost", fabricLine.InchPrice);
        command.Parameters.AddWithValue("@totalCost", fabricLine.RequiredInches * fabricLine.InchPrice);
        command.Parameters.AddWithValue("@created", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAdvanceAsync(int orderId, string orderNumber, int customerId, decimal amount, string? paymentMethod, int cashAccountId, DateTime now, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var reference = $"{orderNumber}:Advance";
        var description = "Customer advance received";

        const string paymentSql = "INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,CreatedDate,PaymentKind) OUTPUT INSERTED.PaymentID VALUES (@orderId,@date,@amount,@method,@reference,@date,N'Advance')";
        int paymentId;
        await using (var payment = new SqlCommand(paymentSql, connection, transaction))
        {
            payment.Parameters.AddWithValue("@orderId", orderId);
            payment.Parameters.AddWithValue("@date", now);
            payment.Parameters.AddWithValue("@amount", amount);
            AddNullable(payment, "@method", paymentMethod);
            payment.Parameters.AddWithValue("@reference", reference);
            paymentId = Convert.ToInt32(await payment.ExecuteScalarAsync(cancellationToken));
        }

        const string ledgerSql = "DECLARE @balance decimal(18,2)=ISNULL((SELECT TOP(1) BalanceAfterTransaction FROM dbo.CustomerLedgerEntries WITH (UPDLOCK,HOLDLOCK) WHERE CustomerID=@customerId ORDER BY CreatedAt DESC,CustomerLedgerEntryId DESC),0); INSERT INTO dbo.CustomerLedgerEntries (CustomerID,ReferenceNumber,DebitAmount,CreditAmount,BalanceAfterTransaction,CreatedAt) VALUES (@customerId,@reference,0,@amount,@balance-@amount,@date)";
        await using (var ledger = new SqlCommand(ledgerSql, connection, transaction))
        {
            ledger.Parameters.AddWithValue("@customerId", customerId);
            ledger.Parameters.AddWithValue("@reference", reference);
            ledger.Parameters.AddWithValue("@amount", amount);
            ledger.Parameters.AddWithValue("@date", now);
            await ledger.ExecuteNonQueryAsync(cancellationToken);
        }

        var posting = await AccountingEventPostingGateway.PostAsync(
            connection,
            transaction,
            AccountingEventType.CustomerAdvance,
            amount,
            paymentId,
            null,
            null,
            null,
            reference,
            description,
            cancellationToken);
        await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, cashAccountId, amount, cancellationToken);
    }

    private static void ThrowIfBalanceWaiverUnavailable() => throw new InvalidOperationException("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.");

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