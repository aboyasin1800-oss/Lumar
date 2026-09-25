using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ReadyMadeSalesRepository(
    ReadOnlySqlConnectionFactory readOnlyConnections,
    OperationalSqlConnectionFactory operationalConnections) : IReadyMadeSalesRepository
{
    public async Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
    {
        ValidateRequest(sale);
        var paymentType = NormalizePaymentType(sale.PaymentType);
        var saleReference = string.IsNullOrWhiteSpace(sale.SaleReference)
            ? $"RMS-{Guid.NewGuid():N}"
            : sale.SaleReference.Trim();
        var totalAmount = sale.Items.Sum(item => item.UnitPrice * item.Quantity);

        if (totalAmount <= 0m) throw new ArgumentException("إجمالي البيع يجب أن يكون أكبر من صفر.");
        if (sale.DiscountAmount < 0m || sale.DiscountAmount > totalAmount)
            throw new ArgumentException("الخصم غير صالح.");

        var netAmount = totalAmount - sale.DiscountAmount;
        ValidatePayment(paymentType, sale.PaidAmount, netAmount);
        if (paymentType == "Donation")
            throw new InvalidOperationException("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var existingOrderId = await FindOrderIdBySaleReferenceAsync(connection, transaction, saleReference, cancellationToken);
            if (existingOrderId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return await GetResultAsync(existingOrderId.Value, cancellationToken)
                    ?? throw new InvalidOperationException("تعذر قراءة عملية البيع الموجودة.");
            }

            if (!await CustomerExistsAsync(connection, transaction, sale.CustomerId, cancellationToken))
                throw new ArgumentException("العميل المحدد غير موجود.");

            var employeeCode = string.IsNullOrWhiteSpace(sale.EmployeeCode) ? null : sale.EmployeeCode.Trim();
            if (employeeCode is not null && !await ActiveEmployeeExistsAsync(connection, transaction, employeeCode, cancellationToken))
                throw new ArgumentException("الموظف المحدد غير موجود أو غير فعال.");

            var lines = new List<SaleLineDraft>(sale.Items.Count);
            foreach (var item in sale.Items)
            {
                if ((item.ReadyMadeInventoryProductId is null) == (item.ImportedReadyMadeProductId is null))
                    throw new ArgumentException("يجب تحديد مصدر واحد فقط لكل منتج: منتج مصنع أو منتج مستورد.");
                if (item.Quantity <= 0) throw new ArgumentException("كمية المنتج يجب أن تكون أكبر من صفر.");
                if (item.UnitPrice < 0m) throw new ArgumentException("سعر المنتج غير صالح.");

                if (item.ReadyMadeInventoryProductId is int readyMadeId)
                {
                    if (item.Quantity != 1) throw new ArgumentException("منتج المصنع الجاهز يباع كقطعة واحدة لكل سجل مخزون.");
                    if (item.ProductTypeId is not int productTypeId || productTypeId <= 0)
                        throw new ArgumentException("ProductTypeId الرسمي مطلوب لمنتج المصنع الجاهز.");

                    var local = await GetLocalProductForUpdateAsync(connection, transaction, readyMadeId, cancellationToken)
                        ?? throw new InvalidOperationException("منتج المصنع الجاهز غير موجود.");
                    if (!local.IsActive || !string.Equals(local.Status, "AvailableForSale", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("منتج المصنع الجاهز غير متاح للبيع أو سبق بيعه.");
                    if (local.ProductTypeId is null || local.ProductTypeId.Value != productTypeId)
                        throw new InvalidOperationException("ProductTypeId لا يطابق الربط الرسمي المحفوظ للمنتج.");
                    if (!await ProductTypeExistsAsync(productTypeId, connection, transaction, cancellationToken))
                        throw new InvalidOperationException("ProductTypeId غير موجود أو غير فعال.");

                    lines.Add(new SaleLineDraft(
                        readyMadeId,
                        null,
                        productTypeId,
                        local.ProductionName,
                        item.Quantity,
                        item.UnitPrice,
                        local.ActualCost ?? 0m,
                        local.TrackingCode));
                }
                else
                {
                    if (item.ProductTypeId is > 0)
                        throw new ArgumentException("لا يستخدم المنتج المستورد ProductTypeId لتحديد النقاط.");

                    var imported = await GetImportedProductForUpdateAsync(connection, transaction, item.ImportedReadyMadeProductId!.Value, cancellationToken)
                        ?? throw new InvalidOperationException("المنتج المستورد غير موجود.");
                    if (!imported.IsActive || imported.Quantity < item.Quantity)
                        throw new InvalidOperationException("الكمية المستوردة غير كافية أو المنتج غير فعال.");

                    lines.Add(new SaleLineDraft(
                        null,
                        imported.ImportedReadyMadeProductId,
                        null,
                        imported.ProductName,
                        item.Quantity,
                        item.UnitPrice,
                        imported.PurchasePrice,
                        imported.ProductCode));
                }
            }

            if (lines.Any(line => line.UnitCost > 0m))
                throw new InvalidOperationException("هذه العملية غير متاحة حتى اكتمال عقد الربط المحاسبي.");

            var now = DateTime.UtcNow;
            var orderNumber = await GetNextOrderNumberAsync(connection, transaction, cancellationToken);
            var paidAmount = paymentType == "Donation" ? 0m : sale.PaidAmount;
            var remainingAmount = paymentType == "Credit" ? netAmount - paidAmount : 0m;
            var orderId = await InsertOrderAsync(
                connection,
                transaction,
                orderNumber,
                sale.CustomerId,
                totalAmount,
                sale.DiscountAmount,
                paidAmount,
                remainingAmount,
                saleReference,
                sale.Notes,
                now,
                cancellationToken);

            var invoiceId = await InsertInvoiceHeaderAsync(
                connection,
                transaction,
                orderId,
                orderNumber,
                sale.CustomerId,
                employeeCode,
                totalAmount,
                sale.DiscountAmount,
                netAmount,
                paidAmount,
                paymentType,
                now,
                cancellationToken);

            foreach (var line in lines)
            {
                var orderItemId = await InsertOrderItemAsync(connection, transaction, orderId, line, now, cancellationToken);
                var trackingCode = await InsertProductionTrackingAsync(connection, transaction, invoiceId, sale.CustomerId, line.ItemName, now, cancellationToken);
                await InsertInvoiceDetailAsync(connection, transaction, invoiceId, sale.CustomerId, orderItemId, trackingCode, line, cancellationToken);

                if (line.ReadyMadeInventoryProductId is int readyMadeId)
                {
                    await MarkLocalProductSoldAsync(connection, transaction, readyMadeId, line.ProductTypeId!.Value, cancellationToken);
                }
                else
                {
                    await DecrementImportedProductAsync(connection, transaction, line.ImportedReadyMadeProductId!.Value, line.Quantity, now, cancellationToken);
                    await InsertImportedSaleMovementAsync(connection, transaction, line, orderNumber, now, cancellationToken);
                }
            }

            await InsertCustomerLedgerEntryAsync(connection, transaction, sale.CustomerId, $"{orderNumber}:Sale", netAmount, 0m, now, cancellationToken);
            await InsertFinancialTransactionAsync(connection, transaction, $"{orderNumber}:RevenueRecognized", "RevenueRecognized", netAmount, $"Ready-made sale revenue for {orderNumber}", now, cancellationToken);
            var revenuePosting = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(connection, transaction, $"{orderNumber}:RevenueRecognized", "RevenueRecognized", netAmount, $"Ready-made sale revenue for {orderNumber}", cancellationToken);
            revenuePosting.ThrowIfFailure();

            if (paymentType is "Cash" or "Credit" && paidAmount > 0m)
            {
                var paymentReference = $"{orderNumber}:Payment";
                await InsertPaymentAsync(connection, transaction, orderId, paidAmount, paymentReference, now, cancellationToken);
                await InsertCustomerLedgerEntryAsync(connection, transaction, sale.CustomerId, paymentReference, 0m, paidAmount, now, cancellationToken);
                await InsertFinancialTransactionAsync(connection, transaction, paymentReference, "CustomerPayment", paidAmount, $"Cash payment for {orderNumber}", now, cancellationToken);
                var paymentPosting = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(connection, transaction, paymentReference, "CustomerPayment", paidAmount, $"Cash payment for {orderNumber}", cancellationToken);
                paymentPosting.ThrowIfFailure();
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetResultAsync(orderId, cancellationToken)
                ?? throw new InvalidOperationException("تعذر قراءة نتيجة البيع بعد الحفظ.");
        }
        catch
        {
            if (!committed)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            }
            throw;
        }
    }

    private async Task<ReadyMadeSaleResultDto?> GetResultAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        const string headerSql = @"
            SELECT TOP (1) o.OrderID, o.OrderNumber, ih.InvoiceID, ih.InvoiceNumber,
                   o.CustomerID, c.CustomerName, o.SaleReference, ih.PaymentType,
                   o.TotalAmount, o.DiscountAmount, ih.NetAmount, o.PaidAmount, o.RemainingAmount
            FROM dbo.Orders o
            INNER JOIN dbo.Invoice_Header ih ON ih.OrderID = o.OrderID
            LEFT JOIN dbo.Customers c ON c.CustomerID = o.CustomerID
            WHERE o.OrderID = @orderId
            ORDER BY ih.InvoiceID DESC;";
        await using var headerCommand = new SqlCommand(headerSql, connection);
        headerCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var headerReader = await headerCommand.ExecuteReaderAsync(cancellationToken);
        if (!await headerReader.ReadAsync(cancellationToken)) return null;
        var result = new ReadyMadeSaleResultDto(
            headerReader.GetInt32(0),
            headerReader.GetString(1),
            headerReader.GetInt32(2),
            headerReader.NullableString("InvoiceNumber") ?? $"Invoice-{headerReader.GetInt32(2)}",
            headerReader.GetInt32(4),
            headerReader.NullableString("CustomerName"),
            headerReader.GetString(6),
            headerReader.NullableString("PaymentType") ?? "Cash",
            headerReader.GetDecimal(8),
            headerReader.GetDecimal(9),
            headerReader.GetDecimal(10),
            headerReader.GetDecimal(11),
            headerReader.GetDecimal(12),
            []);
        await headerReader.CloseAsync();

        const string linesSql = @"
            SELECT d.ReadyMadeInventoryProductId, d.ImportedReadyMadeProductId,
                   oi.ProductTypeId, d.ItemType, d.Quantity, d.UnitPrice, d.TotalPrice,
                   COALESCE(r.ActualCost, ip.PurchasePrice, 0)
            FROM dbo.Invoice_Details d
            LEFT JOIN dbo.OrderItems oi ON oi.OrderItemID = d.OrderItemID
            LEFT JOIN dbo.ReadyMadeInventoryProducts r ON r.ReadyMadeInventoryProductId = d.ReadyMadeInventoryProductId
            LEFT JOIN dbo.ImportedReadyMadeProducts ip ON ip.ImportedReadyMadeProductId = d.ImportedReadyMadeProductId
            WHERE d.InvoiceID = @invoiceId
            ORDER BY d.DetailID;";
        await using var linesCommand = new SqlCommand(linesSql, connection);
        linesCommand.Parameters.AddWithValue("@invoiceId", result.InvoiceId);
        await using var linesReader = await linesCommand.ExecuteReaderAsync(cancellationToken);
        var lines = new List<ReadyMadeSaleLineDto>();
        while (await linesReader.ReadAsync(cancellationToken))
        {
            lines.Add(new ReadyMadeSaleLineDto(
                linesReader.NullableInt32("ReadyMadeInventoryProductId"),
                linesReader.NullableInt32("ImportedReadyMadeProductId"),
                linesReader.NullableInt32("ProductTypeId"),
                linesReader.NullableString("ItemType") ?? "منتج جاهز",
                linesReader.GetInt32(4),
                linesReader.GetDecimal(5),
                linesReader.GetDecimal(6),
                linesReader.GetDecimal(7)));
        }

        return result with { Items = lines };
    }

    private static void ValidateRequest(CreateReadyMadeSaleDto sale)
    {
        if (sale.CustomerId <= 0) throw new ArgumentException("العميل مطلوب.");
        if (sale.Items is null || sale.Items.Count == 0) throw new ArgumentException("يجب إضافة منتج واحد على الأقل.");
    }

    private static string NormalizePaymentType(string? paymentType)
    {
        var normalized = paymentType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "cash" or "نقداً" or "نقدا" => "Cash",
            "credit" or "onaccount" or "آجل" => "Credit",
            "donation" or "تبرعاً" or "تبرعا" => "Donation",
            _ => throw new ArgumentException("طريقة الدفع يجب أن تكون نقداً أو آجلاً أو تبرعاً.")
        };
    }

    private static void ValidatePayment(string paymentType, decimal paidAmount, decimal netAmount)
    {
        if (paidAmount < 0m || paidAmount > netAmount) throw new ArgumentException("المبلغ المدفوع غير صالح.");
        if (paymentType == "Cash" && paidAmount != netAmount) throw new ArgumentException("البيع النقدي يتطلب سداد صافي الفاتورة كاملاً.");
        if (paymentType == "Donation" && paidAmount != 0m) throw new ArgumentException("لا يقبل التبرع دفعة نقدية ضمن عملية البيع.");
    }

    private static async Task<int?> FindOrderIdBySaleReferenceAsync(SqlConnection connection, SqlTransaction transaction, string saleReference, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) OrderID FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE SaleReference = @saleReference", connection, transaction);
        command.Parameters.AddWithValue("@saleReference", saleReference);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is int orderId ? orderId : null;
    }

    private static async Task<bool> CustomerExistsAsync(SqlConnection connection, SqlTransaction transaction, int customerId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID = @customerId", connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> ActiveEmployeeExistsAsync(SqlConnection connection, SqlTransaction transaction, string employeeCode, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP (1) 1 FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeCode = @employeeCode AND (IsActive = 1 OR Status = N'Active')";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeCode", employeeCode);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> ProductTypeExistsAsync(int productTypeId, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.PricingProductTypes WITH (UPDLOCK, HOLDLOCK) WHERE ProductTypeId = @productTypeId AND IsActive = 1", connection, transaction);
        command.Parameters.AddWithValue("@productTypeId", productTypeId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<LocalProduct?> GetLocalProductForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int productId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT ReadyMadeInventoryProductId, ProductTypeId, ProductionName, ActualCost, Status, IsActive, TrackingCode
            FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
            WHERE ReadyMadeInventoryProductId = @productId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@productId", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LocalProduct(
            reader.GetInt32(0),
            reader.NullableInt32("ProductTypeId"),
            reader.GetString(2),
            reader.NullableDecimal("ActualCost"),
            reader.GetString(4),
            reader.GetBoolean(5),
            reader.GetString(6));
    }

    private static async Task<ImportedProduct?> GetImportedProductForUpdateAsync(SqlConnection connection, SqlTransaction transaction, int productId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT ImportedReadyMadeProductId, ProductName, ProductCode, Quantity, PurchasePrice, IsActive
            FROM dbo.ImportedReadyMadeProducts WITH (UPDLOCK, HOLDLOCK)
            WHERE ImportedReadyMadeProductId = @productId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@productId", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ImportedProduct(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetBoolean(5));
    }

    private static async Task<string> GetNextOrderNumberAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "OrderCodePrefix", "ORD-", cancellationToken);
        const string sql = "SELECT ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(OrderNumber, LEN(@prefix) + 1, 20))), 0) + 1 FROM dbo.Orders WITH (TABLOCKX, HOLDLOCK) WHERE OrderNumber LIKE @pattern";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", prefix);
        command.Parameters.AddWithValue("@pattern", $"{prefix}%");
        var next = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return $"{prefix}{next:D6}";
    }

    private static async Task<int> InsertOrderAsync(SqlConnection connection, SqlTransaction transaction, string orderNumber, int customerId, decimal totalAmount, decimal discountAmount, decimal paidAmount, decimal remainingAmount, string saleReference, string? notes, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO dbo.Orders
                (OrderNumber, CustomerID, OrderDate, DeliveryDate, TotalAmount, DiscountAmount, PaidAmount, RemainingAmount,
                 UrgencyStatus, OrderStatus, Notes, CreatedDate, SaleCategory, RevenueRecognized, RevenueRecognizedAt, SaleReference)
            OUTPUT INSERTED.OrderID
            VALUES
                (@orderNumber, @customerId, @orderDate, @deliveryDate, @totalAmount, @discountAmount, @paidAmount, @remainingAmount,
                 N'Normal', N'Delivered', @notes, @createdDate, N'ReadyMadeSale', 1, @revenueRecognizedAt, @saleReference);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderNumber", orderNumber);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@orderDate", now);
        command.Parameters.AddWithValue("@deliveryDate", now);
        command.Parameters.AddWithValue("@totalAmount", totalAmount);
        command.Parameters.AddWithValue("@discountAmount", discountAmount);
        command.Parameters.AddWithValue("@paidAmount", paidAmount);
        command.Parameters.AddWithValue("@remainingAmount", remainingAmount);
        AddNullable(command, "@notes", notes);
        command.Parameters.AddWithValue("@createdDate", now);
        command.Parameters.AddWithValue("@revenueRecognizedAt", now);
        command.Parameters.AddWithValue("@saleReference", saleReference);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertOrderItemAsync(SqlConnection connection, SqlTransaction transaction, int orderId, SaleLineDraft line, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO dbo.OrderItems
                (OrderID, PieceType, Quantity, TrackingCode, PieceStatus, CreatedDate, ProductTypeId, ImportedReadyMadeProductId, ReadyMadeInventoryProductId)
            OUTPUT INSERTED.OrderItemID
            VALUES (@orderId, @pieceType, @quantity, @trackingCode, N'Delivered', @createdDate, @productTypeId, @importedProductId, @readyMadeProductId);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@pieceType", line.ItemName);
        command.Parameters.AddWithValue("@quantity", line.Quantity);
        AddNullable(command, "@trackingCode", line.TrackingCode);
        command.Parameters.AddWithValue("@createdDate", now);
        AddNullable(command, "@productTypeId", line.ProductTypeId);
        AddNullable(command, "@importedProductId", line.ImportedReadyMadeProductId);
        AddNullable(command, "@readyMadeProductId", line.ReadyMadeInventoryProductId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> InsertProductionTrackingAsync(SqlConnection connection, SqlTransaction transaction, int invoiceId, int customerId, string itemType, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = @"
            DECLARE @trackingCode int;
            SELECT @trackingCode = ISNULL(MAX(TrackingCode), 0) + 1
            FROM dbo.Production_Tracking WITH (TABLOCKX, HOLDLOCK);
            INSERT INTO dbo.Production_Tracking
                (TrackingCode, InvoiceID, CustomerID, ItemType, Status, CreatedDate, IsCompleted, IsDelivered, DeliveryDate)
            VALUES (@trackingCode, @invoiceId, @customerId, @itemType, N'ReadyMadeSale', @createdDate, 1, 1, @createdDate);
            SELECT @trackingCode;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@itemType", itemType);
        command.Parameters.AddWithValue("@createdDate", now);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertInvoiceDetailAsync(SqlConnection connection, SqlTransaction transaction, int invoiceId, int customerId, int orderItemId, int trackingCode, SaleLineDraft line, CancellationToken cancellationToken)
    {
        const string sql = @"
            DECLARE @detailId int;
            SELECT @detailId = ISNULL(MAX(DetailID), 0) + 1
            FROM dbo.Invoice_Details WITH (TABLOCKX, HOLDLOCK);
            INSERT INTO dbo.Invoice_Details
                (DetailID, InvoiceID, CustomerID, ItemType, Quantity, UnitPrice, TrackingCode, OrderItemID, ReadyMadeInventoryProductId, ImportedReadyMadeProductId)
            SELECT @detailId, @invoiceId, @customerId, @itemType, @quantity, @unitPrice, @trackingCode, @orderItemId, @readyMadeProductId, @importedProductId
            WHERE NOT EXISTS
                (SELECT 1 FROM dbo.Invoice_Details d WHERE d.OrderItemID = @orderItemId);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@itemType", line.ItemName);
        command.Parameters.AddWithValue("@quantity", line.Quantity);
        command.Parameters.AddWithValue("@unitPrice", line.UnitPrice);
        command.Parameters.AddWithValue("@trackingCode", trackingCode);
        command.Parameters.AddWithValue("@orderItemId", orderItemId);
        AddNullable(command, "@readyMadeProductId", line.ReadyMadeInventoryProductId);
        AddNullable(command, "@importedProductId", line.ImportedReadyMadeProductId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertInvoiceHeaderAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string orderNumber, int customerId, string? employeeCode, decimal totalAmount, decimal discountAmount, decimal netAmount, decimal paidAmount, string paymentType, DateTime now, CancellationToken cancellationToken)
    {
        var invoiceNumber = $"INV-RMS-{orderNumber}";
        const string sql = @"
            INSERT INTO dbo.Invoice_Header
                (CustomerID, EmployeeCode, InvoiceDate, TotalAmount, Discount, NetAmount, PaidAmount, PaymentType, IsDelivered, CreatedAt, OrderID, InvoiceNumber)
            OUTPUT INSERTED.InvoiceID
            VALUES (@customerId, @employeeCode, @invoiceDate, @totalAmount, @discountAmount, @netAmount, @paidAmount, @paymentType, 1, @createdAt, @orderId, @invoiceNumber);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        AddNullable(command, "@employeeCode", employeeCode);
        command.Parameters.AddWithValue("@invoiceDate", now);
        command.Parameters.AddWithValue("@totalAmount", totalAmount);
        command.Parameters.AddWithValue("@discountAmount", discountAmount);
        command.Parameters.AddWithValue("@netAmount", netAmount);
        command.Parameters.AddWithValue("@paidAmount", paidAmount);
        command.Parameters.AddWithValue("@paymentType", paymentType);
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task MarkLocalProductSoldAsync(SqlConnection connection, SqlTransaction transaction, int productId, int productTypeId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.ReadyMadeInventoryProducts SET Status = N'Sold', IsActive = 0 WHERE ReadyMadeInventoryProductId = @productId AND ProductTypeId = @productTypeId AND Status = N'AvailableForSale' AND IsActive = 1";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@productId", productId);
        command.Parameters.AddWithValue("@productTypeId", productTypeId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("تعذر تحويل منتج المصنع إلى Sold؛ قد يكون بيع سابق قد سبقه.");
    }

    private static async Task DecrementImportedProductAsync(SqlConnection connection, SqlTransaction transaction, int productId, int quantity, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.ImportedReadyMadeProducts SET Quantity = Quantity - @quantity, IsActive = CASE WHEN Quantity - @quantity <= 0 THEN 0 ELSE IsActive END, UpdatedAt = @updatedAt WHERE ImportedReadyMadeProductId = @productId AND IsActive = 1 AND Quantity >= @quantity";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@updatedAt", now);
        command.Parameters.AddWithValue("@productId", productId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("الكمية المستوردة غير كافية أو تم بيعها في عملية متزامنة.");
    }

    private static async Task InsertImportedSaleMovementAsync(SqlConnection connection, SqlTransaction transaction, SaleLineDraft line, string orderNumber, DateTime now, CancellationToken cancellationToken)
    {
        const string findSql = "SELECT TOP (1) InventoryItemID FROM dbo.InventoryItems WHERE ItemCode = @itemCode";
        await using var find = new SqlCommand(findSql, connection, transaction);
        find.Parameters.AddWithValue("@itemCode", line.TrackingCode);
        var value = await find.ExecuteScalarAsync(cancellationToken);
        if (value is not int inventoryItemId) return;

        const string insertSql = @"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
            VALUES (@inventoryItemId, N'Sale', @quantity, @reference, @notes, @createdAt, @cost, @unitCost);";
        await using var insert = new SqlCommand(insertSql, connection, transaction);
        insert.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
        insert.Parameters.AddWithValue("@quantity", line.Quantity);
        insert.Parameters.AddWithValue("@reference", $"{orderNumber}:Imported:{line.ImportedReadyMadeProductId}");
        insert.Parameters.AddWithValue("@notes", "Ready-made imported product sale");
        insert.Parameters.AddWithValue("@createdAt", now);
        insert.Parameters.AddWithValue("@cost", line.UnitCost * line.Quantity);
        insert.Parameters.AddWithValue("@unitCost", line.UnitCost);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPaymentAsync(SqlConnection connection, SqlTransaction transaction, int orderId, decimal amount, string reference, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.Payments (OrderID, PaymentDate, Amount, PaymentMethod, ReferenceNo, CreatedDate, PaymentKind) VALUES (@orderId, @paymentDate, @amount, N'Cash', @reference, @createdDate, N'SaleCash')";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@paymentDate", now);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@createdDate", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCustomerLedgerEntryAsync(SqlConnection connection, SqlTransaction transaction, int customerId, string reference, decimal debitAmount, decimal creditAmount, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = @"
            DECLARE @balance decimal(18,2) = ISNULL((
                SELECT TOP (1) BalanceAfterTransaction
                FROM dbo.CustomerLedgerEntries WITH (UPDLOCK, HOLDLOCK)
                WHERE CustomerID = @customerId
                ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC), 0);
            INSERT INTO dbo.CustomerLedgerEntries
                (CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt)
            SELECT @customerId, @reference, @debitAmount, @creditAmount, @balance + @debitAmount - @creditAmount, @createdAt
            WHERE NOT EXISTS (SELECT 1 FROM dbo.CustomerLedgerEntries WITH (UPDLOCK, HOLDLOCK) WHERE ReferenceNumber = @reference);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@debitAmount", debitAmount);
        command.Parameters.AddWithValue("@creditAmount", creditAmount);
        command.Parameters.AddWithValue("@createdAt", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertFinancialTransactionAsync(SqlConnection connection, SqlTransaction transaction, string reference, string transactionType, decimal amount, string description, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt)
            SELECT @reference, @transactionType, @amount, @description, @createdAt
            WHERE NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WITH (UPDLOCK, HOLDLOCK) WHERE ReferenceNumber = @reference AND TransactionType = @transactionType);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@description", description);
        command.Parameters.AddWithValue("@createdAt", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable(SqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private sealed record LocalProduct(int ProductId, int? ProductTypeId, string ProductionName, decimal? ActualCost, string Status, bool IsActive, string TrackingCode);
    private sealed record ImportedProduct(int ImportedReadyMadeProductId, string ProductName, string ProductCode, decimal Quantity, decimal PurchasePrice, bool IsActive);
    private sealed record SaleLineDraft(int? ReadyMadeInventoryProductId, int? ImportedReadyMadeProductId, int? ProductTypeId, string ItemName, int Quantity, decimal UnitPrice, decimal UnitCost, string TrackingCode);
}