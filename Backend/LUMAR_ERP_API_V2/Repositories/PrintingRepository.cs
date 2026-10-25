using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Printing;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PrintingRepository(
    ReadOnlySqlConnectionFactory readOnlyConnections,
    OperationalSqlConnectionFactory operationalConnections) : IPrintingRepository
{
    private const string HistorySelect = @"
        SELECT PrintHistoryId, PrintRequestId, PrintStatus, OrderId, OrderItemId, PieceId,
               ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadePieceId,
               TrackingCode, PrintedAtUtc, PrintedByUserId, PrintedByDisplayName, CopyNumber,
               ReprintReasonCode, DamageReason, ResponsibleEmployeeId, Notes, SaleAmount,
               SalePaymentType, SaleCustomerId, FinancialTransactionReference,
               FinancialTransactionId, JournalEntryId, CustomerLedgerEntryId, PaymentId,
               PaymentReferenceNumber, PaymentFinancialTransactionId, PaymentJournalEntryId,
               PaymentCustomerLedgerEntryId, WaiverReferenceNumber, WaiverFinancialTransactionId,
               WaiverJournalEntryId, WaiverCustomerLedgerEntryId, CompletedAtUtc, FailedAtUtc,
               FailureReason, CreatedAt
        FROM dbo.MeasurementCardPrintHistory";

    public async Task<IReadOnlyList<MeasurementCardPrintHistoryDto>> GetPieceHistoryAsync(
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        if (pieceId <= 0) throw new ArgumentException("معرف القطعة غير صالح.", nameof(pieceId));

        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        var identityColumn = isReadyMade ? "ReadyMadePieceId" : "PieceId";
        await using var command = new SqlCommand(
            $"{HistorySelect} WHERE {identityColumn} = @pieceId ORDER BY CopyNumber, PrintHistoryId",
            connection);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MeasurementCardPrintHistoryDto>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(MapHistory(reader));
        return results;
    }

    public async Task<MeasurementCardPrintHistoryDto> PrepareAsync(
        int pieceId,
        bool isReadyMade,
        PrepareMeasurementCardPrintDto request,
        CurrentUserDto user,
        CancellationToken cancellationToken)
    {
        if (pieceId <= 0) throw new ArgumentException("معرف القطعة غير صالح.", nameof(pieceId));
        if (request is null) throw new ArgumentException("بيانات الطباعة مطلوبة.", nameof(request));
        if (user.UserId <= 0) throw new ArgumentException("هوية المستخدم غير صالحة.", nameof(user));

        var printRequestId = request.PrintRequestId ?? Guid.NewGuid();
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var existingRequest = await LoadHistoryByRequestIdAsync(connection, transaction, printRequestId, cancellationToken);
            if (existingRequest is not null)
            {
                if (!IsSamePiece(existingRequest, pieceId, isReadyMade))
                    throw new InvalidOperationException("رقم طلب الطباعة مستخدم لقطعة أخرى.");
                if (existingRequest.PrintedByUserId != user.UserId)
                    throw new InvalidOperationException("عملية الطباعة مرتبطة بمستخدم آخر.");
                if (existingRequest.PrintStatus == "Failed")
                    throw new InvalidOperationException("محاولة الطباعة السابقة فشلت؛ أنشئ طلب طباعة جديدًا.");

                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return existingRequest;
            }

            var context = await LoadPieceContextAsync(connection, transaction, pieceId, isReadyMade, cancellationToken)
                ?? throw new InvalidOperationException("القطعة غير موجودة.");

            var (lastCompletedCopyNumber, completedCount) = await ReadCompletedCopyStateAsync(
                connection,
                transaction,
                pieceId,
                isReadyMade,
                cancellationToken);
            var legacyPrinted = completedCount == 0 && MeasurementCardPrintPolicy.IsLegacyPrintedStatus(context.PieceStatus);
            var hasPrintedBefore = completedCount > 0 || legacyPrinted;
            var reasonCode = NormalizeOptional(request.ReprintReasonCode);

            ValidateReprintRequest(request, reasonCode, hasPrintedBefore, context, isReadyMade);

            var activeReservation = await ReadActiveReservationAsync(
                connection,
                transaction,
                pieceId,
                isReadyMade,
                cancellationToken);
            if (activeReservation is not null)
                throw new InvalidOperationException("توجد عملية طباعة معلقة لهذه القطعة.");

            var responsibleEmployeeId = reasonCode == MeasurementCardPrintPolicy.DamagedPiece
                ? request.ResponsibleEmployeeId
                : null;
            if (responsibleEmployeeId is int employeeId)
                await EnsureActiveEmployeeAsync(connection, transaction, employeeId, cancellationToken);

            var saleCustomerId = reasonCode == MeasurementCardPrintPolicy.PieceSold
                ? request.SaleCustomerId ?? context.CustomerId
                : null;
            if (reasonCode == MeasurementCardPrintPolicy.PieceSold)
            {
                if (saleCustomerId is not int customerId || customerId <= 0)
                    throw new ArgumentException("العميل مطلوب عند تسجيل بيع القطعة.");
                await EnsureActiveCustomerAsync(connection, transaction, customerId, cancellationToken);
            }

            var copyNumber = MeasurementCardPrintPolicy.NextCopyNumber(lastCompletedCopyNumber, legacyPrinted);
            var printedDisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName.Trim();
            var notes = NormalizeOptional(request.Notes);
            var damageReason = reasonCode == MeasurementCardPrintPolicy.DamagedPiece
                ? NormalizeRequired(request.DamageReason, "سبب تلف القطعة مطلوب.")
                : null;
            var salePaymentType = reasonCode == MeasurementCardPrintPolicy.PieceSold
                ? MeasurementCardPrintPolicy.NormalizePaymentType(request.SalePaymentType)
                : null;
            var saleAmount = reasonCode == MeasurementCardPrintPolicy.PieceSold ? request.SaleAmount : null;

            const string insertSql = @"
                INSERT INTO dbo.MeasurementCardPrintHistory
                (
                    PrintRequestId, PrintStatus, OrderId, OrderItemId, PieceId,
                    ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadePieceId,
                    TrackingCode, PrintedByUserId, PrintedByDisplayName, CopyNumber,
                    ReprintReasonCode, DamageReason, ResponsibleEmployeeId, Notes,
                    SaleAmount, SalePaymentType, SaleCustomerId, CreatedAt
                )
                OUTPUT INSERTED.PrintHistoryId
                VALUES
                (
                    @printRequestId, N'Reserved', @orderId, @orderItemId, @pieceId,
                    @readyMadeOrderId, @readyMadeItemId, @readyMadePieceId,
                    @trackingCode, @printedByUserId, @printedByDisplayName, @copyNumber,
                    @reasonCode, @damageReason, @responsibleEmployeeId, @notes,
                    @saleAmount, @salePaymentType, @saleCustomerId, SYSUTCDATETIME()
                );";
            await using var insert = new SqlCommand(insertSql, connection, transaction);
            AddGuid(insert, "@printRequestId", printRequestId);
            AddNullable(insert, "@orderId", context.OrderId);
            AddNullable(insert, "@orderItemId", context.OrderItemId);
            AddNullable(insert, "@pieceId", context.PieceId);
            AddNullable(insert, "@readyMadeOrderId", context.ReadyMadeProductionOrderId);
            AddNullable(insert, "@readyMadeItemId", context.ReadyMadeProductionOrderItemId);
            AddNullable(insert, "@readyMadePieceId", context.ReadyMadePieceId);
            insert.Parameters.AddWithValue("@trackingCode", context.TrackingCode);
            insert.Parameters.AddWithValue("@printedByUserId", user.UserId);
            insert.Parameters.AddWithValue("@printedByDisplayName", printedDisplayName);
            insert.Parameters.AddWithValue("@copyNumber", copyNumber);
            AddNullable(insert, "@reasonCode", reasonCode);
            AddNullable(insert, "@damageReason", damageReason);
            AddNullable(insert, "@responsibleEmployeeId", responsibleEmployeeId);
            AddNullable(insert, "@notes", notes);
            AddNullableDecimal(insert, "@saleAmount", saleAmount);
            AddNullable(insert, "@salePaymentType", salePaymentType);
            AddNullable(insert, "@saleCustomerId", saleCustomerId);
            var printHistoryId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));

            if (reasonCode == MeasurementCardPrintPolicy.PieceSold)
            {
                var financialReference = BuildRevenueReference(printHistoryId);
                await using var referenceCommand = new SqlCommand(
                    "UPDATE dbo.MeasurementCardPrintHistory SET FinancialTransactionReference = @reference WHERE PrintHistoryId = @printHistoryId",
                    connection,
                    transaction);
                referenceCommand.Parameters.AddWithValue("@reference", financialReference);
                referenceCommand.Parameters.AddWithValue("@printHistoryId", printHistoryId);
                await referenceCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetHistoryByIdAsync(printHistoryId, cancellationToken)
                ?? throw new InvalidOperationException("تعذر قراءة سجل الطباعة بعد التجهيز.");
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

    public async Task<MeasurementCardPrintHistoryDto> CompleteAsync(
        int printHistoryId,
        int? cashAccountId,
        CurrentUserDto user,
        CancellationToken cancellationToken)
    {
        if (printHistoryId <= 0) throw new ArgumentException("معرف سجل الطباعة غير صالح.", nameof(printHistoryId));

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var history = await LoadHistoryByIdAsync(connection, transaction, printHistoryId, cancellationToken)
                ?? throw new InvalidOperationException("سجل الطباعة غير موجود.");
            if (history.PrintStatus == "Completed")
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return history;
            }
            if (history.PrintStatus != "Reserved")
                throw new InvalidOperationException("لا يمكن اعتماد سجل طباعة غير محجوز.");
            if (history.PrintedByUserId != user.UserId)
                throw new InvalidOperationException("لا يمكن لمستخدم آخر اعتماد عملية الطباعة.");
            if (history.ReprintReasonCode == MeasurementCardPrintPolicy.PieceSold &&
                history.SalePaymentType == MeasurementCardPrintPolicy.Cash &&
                cashAccountId is not > 0)
                throw new ArgumentException("الحساب النقدي مطلوب لاعتماد بيع القطعة.");

            var revenueFinancialTransactionId = history.FinancialTransactionId;
            var revenueJournalEntryId = history.JournalEntryId;
            var revenueCustomerLedgerEntryId = history.CustomerLedgerEntryId;
            var paymentId = history.PaymentId;
            var paymentReference = history.PaymentReferenceNumber;
            var paymentFinancialTransactionId = history.PaymentFinancialTransactionId;
            var paymentJournalEntryId = history.PaymentJournalEntryId;
            var paymentCustomerLedgerEntryId = history.PaymentCustomerLedgerEntryId;
            var waiverReference = history.WaiverReferenceNumber;
            var waiverFinancialTransactionId = history.WaiverFinancialTransactionId;
            var waiverJournalEntryId = history.WaiverJournalEntryId;
            var waiverCustomerLedgerEntryId = history.WaiverCustomerLedgerEntryId;

            if (history.ReprintReasonCode == MeasurementCardPrintPolicy.PieceSold)
            {
                var context = await LoadPieceContextFromHistoryAsync(connection, transaction, history, cancellationToken)
                    ?? throw new InvalidOperationException("تعذر تحميل سياق القطعة للبيع.");
                var saleCustomerId = history.SaleCustomerId
                    ?? throw new InvalidOperationException("عميل البيع مفقود.");
                var customerName = await LoadCustomerNameAsync(connection, transaction, saleCustomerId, cancellationToken);
                var orderNumber = context.OrderNumber ?? $"#{history.OrderId ?? history.ReadyMadeProductionOrderId}";
                var description = BuildSaleDescription(
                    customerName,
                    orderNumber,
                    history.TrackingCode,
                    history.SaleAmount!.Value,
                    history.CopyNumber);
                var revenueReference = history.FinancialTransactionReference
                    ?? BuildRevenueReference(history.PrintHistoryId);

                revenueCustomerLedgerEntryId = await EnsureCustomerLedgerEntryAsync(
                    connection,
                    transaction,
                    saleCustomerId,
                    revenueReference,
                    history.SaleAmount.Value,
                    0m,
                    cancellationToken);
                var revenuePosting = await AccountingEventPostingGateway.PostAsync(
                    connection,
                    transaction,
                    AccountingEventType.RevenueRecognizedPrintSale,
                    history.SaleAmount.Value,
                    null,
                    null,
                    history.PrintHistoryId,
                    null,
                    revenueReference,
                    description,
                    cancellationToken);
                revenueFinancialTransactionId = revenuePosting.FinancialTransactionId;
                revenueJournalEntryId = revenuePosting.JournalEntryId;

                if (history.SalePaymentType == MeasurementCardPrintPolicy.Cash)
                {
                    paymentReference = $"{revenueReference}:Payment";
                    paymentCustomerLedgerEntryId = await EnsureCustomerLedgerEntryAsync(
                        connection,
                        transaction,
                        saleCustomerId,
                        paymentReference,
                        0m,
                        history.SaleAmount.Value,
                        cancellationToken);
                    paymentId = await EnsurePaymentAsync(
                        connection,
                        transaction,
                        history,
                        paymentReference,
                        description,
                        cancellationToken);
                    var paymentPosting = await AccountingEventPostingGateway.PostAsync(
                        connection,
                        transaction,
                        AccountingEventType.CustomerPayment,
                        history.SaleAmount.Value,
                        paymentId,
                        null,
                        null,
                        null,
                        paymentReference,
                        description,
                        cancellationToken);
                    paymentFinancialTransactionId = paymentPosting.FinancialTransactionId;
                    paymentJournalEntryId = paymentPosting.JournalEntryId;
                    await CashMovementPostingGateway.PostCashInAsync(connection, transaction, paymentPosting.AccountingEventId, cashAccountId!.Value, history.SaleAmount.Value, cancellationToken);
                }
                else if (history.SalePaymentType == MeasurementCardPrintPolicy.Donation)
                {
                    throw new InvalidOperationException("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.");
                }
            }

            const string updateSql = @"
                UPDATE dbo.MeasurementCardPrintHistory
                SET PrintStatus = N'Completed',
                    PrintedAtUtc = SYSUTCDATETIME(),
                    CompletedAtUtc = SYSUTCDATETIME(),
                    FinancialTransactionId = @financialTransactionId,
                    JournalEntryId = @journalEntryId,
                    CustomerLedgerEntryId = @customerLedgerEntryId,
                    PaymentId = @paymentId,
                    PaymentReferenceNumber = @paymentReferenceNumber,
                    PaymentFinancialTransactionId = @paymentFinancialTransactionId,
                    PaymentJournalEntryId = @paymentJournalEntryId,
                    PaymentCustomerLedgerEntryId = @paymentCustomerLedgerEntryId,
                    WaiverReferenceNumber = @waiverReference,
                    WaiverFinancialTransactionId = @waiverFinancialTransactionId,
                    WaiverJournalEntryId = @waiverJournalEntryId,
                    WaiverCustomerLedgerEntryId = @waiverCustomerLedgerEntryId,
                    FailureReason = NULL
                WHERE PrintHistoryId = @printHistoryId AND PrintStatus = N'Reserved';";
            await using var update = new SqlCommand(updateSql, connection, transaction);
            AddNullable(update, "@financialTransactionId", revenueFinancialTransactionId);
            AddNullable(update, "@journalEntryId", revenueJournalEntryId);
            AddNullable(update, "@customerLedgerEntryId", revenueCustomerLedgerEntryId);
            AddNullable(update, "@paymentId", paymentId);
            AddNullable(update, "@paymentReferenceNumber", paymentReference);
            AddNullable(update, "@paymentFinancialTransactionId", paymentFinancialTransactionId);
            AddNullable(update, "@paymentJournalEntryId", paymentJournalEntryId);
            AddNullable(update, "@paymentCustomerLedgerEntryId", paymentCustomerLedgerEntryId);
            AddNullable(update, "@waiverReference", waiverReference);
            AddNullable(update, "@waiverFinancialTransactionId", waiverFinancialTransactionId);
            AddNullable(update, "@waiverJournalEntryId", waiverJournalEntryId);
            AddNullable(update, "@waiverCustomerLedgerEntryId", waiverCustomerLedgerEntryId);
            update.Parameters.AddWithValue("@printHistoryId", printHistoryId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("تعذر اعتماد سجل الطباعة.");

            var pieceId = history.PieceId ?? history.ReadyMadePieceId;
            var shouldAdvanceToPrinting = string.IsNullOrWhiteSpace(history.ReprintReasonCode);

            if (pieceId is int targetPieceId && shouldAdvanceToPrinting)
            {
                await AdvancePieceToPrintingStageAsync(
                    connection,
                    transaction,
                    history,
                    targetPieceId,
                    history.ReadyMadePieceId is not null,
                    cancellationToken);
            }

            if (history.ReprintReasonCode == MeasurementCardPrintPolicy.DamagedPiece)
            {
                await HandleDamagedPieceReworkAsync(connection, transaction, history, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetHistoryByIdAsync(printHistoryId, cancellationToken)
                ?? throw new InvalidOperationException("تعذر قراءة سجل الطباعة بعد الاعتماد.");
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

    private static async Task AdvancePieceToPrintingStageAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(history.ReprintReasonCode))
        {
            return;
        }

        var statusColumn = "PieceStatus";
        var statusTable = isReadyMade ? "dbo.ReadyMadeProductionOrderPieceInstances" : "dbo.Pieces";
        var pieceIdColumn = isReadyMade ? "ReadyMadeProductionOrderPieceInstanceId" : "PieceID";

        await using (var statusCommand = new SqlCommand($@"
            SELECT {statusColumn}
            FROM {statusTable}
            WITH (UPDLOCK, HOLDLOCK)
            WHERE {pieceIdColumn} = @pieceId;", connection, transaction))
        {
            statusCommand.Parameters.AddWithValue("@pieceId", pieceId);
            var currentStatus = await statusCommand.ExecuteScalarAsync(cancellationToken);
            if (currentStatus is not null && string.Equals(currentStatus.ToString(), "Printing", StringComparison.OrdinalIgnoreCase))
                return;
        }

        var hasPrintingEvent = await HasOfficialPrintingEventAsync(connection, transaction, pieceId, isReadyMade, cancellationToken);
        if (hasPrintingEvent)
        {
            await using (var statusSync = new SqlCommand($@"
                UPDATE {statusTable}
                SET {statusColumn} = N'Printing'
                WHERE {pieceIdColumn} = @pieceId AND {statusColumn} <> N'Printing';", connection, transaction))
            {
                statusSync.Parameters.AddWithValue("@pieceId", pieceId);
                await statusSync.ExecuteNonQueryAsync(cancellationToken);
            }
            return;
        }

        var routeSql = isReadyMade
            ? @"
                SELECT i.ProductTypeId
                FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
                WHERE p.ReadyMadeProductionOrderPieceInstanceId = @pieceId;"
            : @"
                SELECT oi.ProductTypeId
                FROM dbo.Pieces p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
                WHERE p.PieceID = @pieceId;";

        int? productTypeId = null;
        await using (var routeCommand = new SqlCommand(routeSql, connection, transaction))
        {
            routeCommand.Parameters.AddWithValue("@pieceId", pieceId);
            var value = await routeCommand.ExecuteScalarAsync(cancellationToken);
            productTypeId = value is null || value is DBNull ? null : Convert.ToInt32(value);
        }

        if (!productTypeId.HasValue || productTypeId.Value <= 0)
            return;

        var route = ProductionTrackingEngine.GetRoute(productTypeId.Value);
        if (route.Count == 0 || !route.Contains("Printing", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("مرحلة الطباعة غير مسموح بها في مسار الإنتاج الرسمي لهذا النوع.");

        var orderItemId = isReadyMade ? (object)DBNull.Value : await ReadOrderItemIdAsync(connection, transaction, pieceId, cancellationToken);
        var orderId = isReadyMade ? (object)DBNull.Value : await ReadOrderIdAsync(connection, transaction, pieceId, cancellationToken);
        var notes = $"MeasurementCardPrintHistoryId={history.PrintHistoryId};Operation=CompletedPrint;OfficialStage=Printing";

        await using (var insert = new SqlCommand(@"
            INSERT INTO dbo.TrackingEvents
                (OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, Notes, IsReverted, RevertedAt, PieceID, ReadyMadeProductionOrderPieceInstanceId)
            VALUES (@orderItemId, @orderId, @trackingCode, N'Printing', N'Printing', SYSUTCDATETIME(), @notes, 0, NULL, @pieceId, @readyMadePieceInstanceId);", connection, transaction))
        {
            insert.Parameters.AddWithValue("@orderItemId", orderItemId);
            insert.Parameters.AddWithValue("@orderId", orderId);
            insert.Parameters.AddWithValue("@trackingCode", history.TrackingCode);
            insert.Parameters.AddWithValue("@notes", notes);
            insert.Parameters.AddWithValue("@pieceId", isReadyMade ? (object)DBNull.Value : pieceId);
            insert.Parameters.AddWithValue("@readyMadePieceInstanceId", isReadyMade ? (object)pieceId : DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = new SqlCommand($@"
            UPDATE {statusTable}
            SET {statusColumn} = N'Printing'
            WHERE {pieceIdColumn} = @pieceId AND {statusColumn} <> N'Printing';", connection, transaction))
        {
            update.Parameters.AddWithValue("@pieceId", pieceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task HandleDamagedPieceReworkAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        CancellationToken cancellationToken)
    {
        var fabricCode = await LoadReplacementFabricCodeAsync(connection, transaction, history, cancellationToken);
        if (string.IsNullOrWhiteSpace(fabricCode))
            return;

        var replacementInches = await LoadReplacementFabricQuantityAsync(connection, transaction, history, cancellationToken);
        if (replacementInches <= 0m)
            return;

        var sourceOperationId = DeriveDamagedPieceSourceOperationId(history);
        await InventoryRepository.ConsumeFabricCodeInventoryAsync(
            connection,
            transaction,
            new ConsumeFabricCodeInventoryDto
            {
                FabricCode = fabricCode,
                OrderItemId = history.OrderItemId,
                PieceId = history.PieceId,
                ReadyMadeProductionOrderItemId = history.ReadyMadeProductionOrderItemId,
                ReadyMadeProductionOrderPieceInstanceId = history.ReadyMadePieceId,
                QuantityInches = replacementInches,
                ConfirmedByUserId = history.PrintedByUserId,
                SourceOperationId = sourceOperationId
            },
            cancellationToken);

        var orderId = history.OrderId is int orderIdValue ? (object)orderIdValue : DBNull.Value;
        var orderItemId = history.OrderItemId is int itemId ? (object)itemId : DBNull.Value;
        var pieceId = history.PieceId is int pieceValue ? (object)pieceValue : DBNull.Value;
        var readyMadeOrderId = history.ReadyMadeProductionOrderId is int readyOrderId ? (object)readyOrderId : DBNull.Value;
        var readyMadePieceId = history.ReadyMadePieceId is int readyPieceId ? (object)readyPieceId : DBNull.Value;
        var notes = $"MeasurementCardPrintHistoryId={history.PrintHistoryId};Operation=DamagedPieceRework;ReplacementFabric={fabricCode};ReplacementInches={replacementInches:0.######};ResponsibleEmployeeId={history.ResponsibleEmployeeId}";

        await using var trackingInsert = new SqlCommand(@"
            INSERT INTO dbo.TrackingEvents
                (OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, Notes, IsReverted, RevertedAt, PieceID, ReadyMadeProductionOrderPieceInstanceId)
            VALUES (@orderItemId, @orderId, @trackingCode, N'Rework', N'Rework', SYSUTCDATETIME(), @notes, 0, NULL, @pieceId, @readyMadePieceId);", connection, transaction);
        trackingInsert.Parameters.AddWithValue("@orderItemId", orderItemId);
        trackingInsert.Parameters.AddWithValue("@orderId", orderId);
        trackingInsert.Parameters.AddWithValue("@trackingCode", history.TrackingCode);
        trackingInsert.Parameters.AddWithValue("@notes", notes);
        trackingInsert.Parameters.AddWithValue("@pieceId", pieceId);
        trackingInsert.Parameters.AddWithValue("@readyMadePieceId", readyMadePieceId);
        await trackingInsert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> LoadReplacementFabricCodeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        CancellationToken cancellationToken)
    {
        if (history.OrderItemId is int orderItemId)
        {
            await using var command = new SqlCommand(
                "SELECT TOP (1) FabricCode FROM dbo.OrderItemFabrics WITH (UPDLOCK, HOLDLOCK) WHERE OrderItemID = @orderItemId ORDER BY OrderItemFabricID;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@orderItemId", orderItemId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value is DBNull ? null : Convert.ToString(value)?.Trim();
        }

        if (history.ReadyMadeProductionOrderItemId is int readyMadeItemId)
        {
            await using var command = new SqlCommand(
                "SELECT TOP (1) FabricCode FROM dbo.ReadyMadeProductionOrderItems WITH (UPDLOCK, HOLDLOCK) WHERE ReadyMadeProductionOrderItemId = @readyMadeItemId;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@readyMadeItemId", readyMadeItemId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value is DBNull ? null : Convert.ToString(value)?.Trim();
        }

        return null;
    }

    private static async Task<decimal> LoadReplacementFabricQuantityAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        CancellationToken cancellationToken)
    {
        if (history.OrderItemId is int orderItemId)
        {
            await using var command = new SqlCommand(
                "SELECT TOP (1) Quantity, Unit FROM dbo.OrderItemFabrics WITH (UPDLOCK, HOLDLOCK) WHERE OrderItemID = @orderItemId ORDER BY OrderItemFabricID;",
                connection,
                transaction);
            command.Parameters.AddWithValue("@orderItemId", orderItemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var quantity = reader.GetDecimal(0);
                var unit = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (quantity <= 0m)
                    return 0m;

                if (string.Equals(unit, "Yard", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "Yards", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "ياردة", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "يارد", StringComparison.OrdinalIgnoreCase))
                {
                    return quantity * 36m;
                }

                if (string.Equals(unit, "Inch", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "Inches", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "بوصة", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(unit, "بوصات", StringComparison.OrdinalIgnoreCase))
                {
                    return quantity;
                }

                return quantity;
            }
        }

        return 0m;
    }

    private static Guid DeriveDamagedPieceSourceOperationId(MeasurementCardPrintHistoryDto history)
    {
        var identity = history.PrintHistoryId > 0
            ? history.PrintHistoryId
            : history.PieceId ?? history.ReadyMadePieceId ?? 0;
        var seed = $"DamagedPiece:{history.OrderItemId ?? history.ReadyMadeProductionOrderItemId ?? 0}:{identity}:{history.TrackingCode}:{history.PieceId ?? history.ReadyMadePieceId ?? 0}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash[..16]);
    }

    private static async Task<bool> HasOfficialPrintingEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        var sql = isReadyMade
            ? "SELECT TOP (1) 1 FROM dbo.TrackingEvents WITH (UPDLOCK, HOLDLOCK) WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId AND Stage = N'Printing' AND Status = N'Printing';"
            : "SELECT TOP (1) 1 FROM dbo.TrackingEvents WITH (UPDLOCK, HOLDLOCK) WHERE PieceID = @pieceId AND Stage = N'Printing' AND Status = N'Printing';";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private static async Task<int?> ReadOrderItemIdAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT OrderItemID FROM dbo.Pieces WITH (UPDLOCK, HOLDLOCK) WHERE PieceID = @pieceId;", connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<int?> ReadOrderIdAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(@"
            SELECT oi.OrderID
            FROM dbo.Pieces p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE p.PieceID = @pieceId;", connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? null : Convert.ToInt32(result);
    }

    public async Task<MeasurementCardPrintHistoryDto?> FailAsync(
        int printHistoryId,
        string? failureReason,
        CurrentUserDto user,
        CancellationToken cancellationToken)
    {
        if (printHistoryId <= 0) throw new ArgumentException("معرف سجل الطباعة غير صالح.", nameof(printHistoryId));

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var committed = false;

        try
        {
            var history = await LoadHistoryByIdAsync(connection, transaction, printHistoryId, cancellationToken);
            if (history is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }
            if (history.PrintStatus == "Completed" || history.PrintStatus == "Failed")
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
                return history;
            }
            if (history.PrintedByUserId != user.UserId)
                throw new InvalidOperationException("لا يمكن لمستخدم آخر إغلاق محاولة الطباعة.");

            await using var update = new SqlCommand(@"
                UPDATE dbo.MeasurementCardPrintHistory
                SET PrintStatus = N'Failed', FailedAtUtc = SYSUTCDATETIME(), FailureReason = @failureReason
                WHERE PrintHistoryId = @printHistoryId AND PrintStatus = N'Reserved';", connection, transaction);
            AddNullable(update, "@failureReason", NormalizeOptional(failureReason) ?? "لم تكتمل عملية الطباعة.");
            update.Parameters.AddWithValue("@printHistoryId", printHistoryId);
            await update.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return await GetHistoryByIdAsync(printHistoryId, cancellationToken);
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

    private static void ValidateReprintRequest(
        PrepareMeasurementCardPrintDto request,
        string? reasonCode,
        bool hasPrintedBefore,
        PieceContext context,
        bool isReadyMade)
    {
        if (hasPrintedBefore && !MeasurementCardPrintPolicy.IsReprintReason(reasonCode))
            throw new ArgumentException("يجب اختيار سبب إعادة الطباعة.");
        if (!hasPrintedBefore && reasonCode is not null)
            throw new ArgumentException("لا يمكن تحديد سبب إعادة الطباعة للنسخة الأولى.");

        if (reasonCode == MeasurementCardPrintPolicy.DamagedPiece)
        {
            if (string.IsNullOrWhiteSpace(request.DamageReason))
                throw new ArgumentException("سبب تلف القطعة مطلوب.");
            if (request.ResponsibleEmployeeId is not > 0)
                throw new ArgumentException("الموظف المسؤول عن التلف مطلوب.");
        }
        else if (!string.IsNullOrWhiteSpace(request.DamageReason) || request.ResponsibleEmployeeId is not null)
        {
            throw new ArgumentException("بيانات التلف لا تستخدم إلا لسبب القطعة التالفة.");
        }

        if (reasonCode == MeasurementCardPrintPolicy.PieceSold)
        {
            if (request.SaleAmount is not > 0m)
                throw new ArgumentException("قيمة البيع يجب أن تكون أكبر من صفر.");
            var paymentType = MeasurementCardPrintPolicy.NormalizePaymentType(request.SalePaymentType);
            if (paymentType == MeasurementCardPrintPolicy.Donation)
                throw new InvalidOperationException("هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.");
            if (isReadyMade && request.SaleCustomerId is not > 0)
                throw new ArgumentException("يجب اختيار عميل بيع القطعة الجاهزة.");
        }
        else if (request.SaleAmount is not null || !string.IsNullOrWhiteSpace(request.SalePaymentType) || request.SaleCustomerId is not null)
        {
            throw new ArgumentException("بيانات البيع لا تستخدم إلا لسبب تم بيع القطعة.");
        }

        if (context.TrackingCode.Trim().Length == 0)
            throw new InvalidOperationException("رمز تتبع القطعة مفقود.");
    }

    private static bool IsSamePiece(MeasurementCardPrintHistoryDto history, int pieceId, bool isReadyMade) =>
        isReadyMade ? history.ReadyMadePieceId == pieceId : history.PieceId == pieceId;

    private static string BuildRevenueReference(int printHistoryId) => $"MC-PIECE-SALE-{printHistoryId:D8}";

    private static string BuildSaleDescription(string customerName, string orderNumber, string trackingCode, decimal amount, int copyNumber) =>
        $"بيع القطعة ذات رمز التتبع {trackingCode} العائدة للطلب {orderNumber} والعميل {customerName} بقيمة {amount:0.00}، مع إصدار بطاقة بديلة: {MeasurementCardPrintPolicy.CopyLabel(copyNumber)}.";

    private static async Task<int> EnsureCustomerLedgerEntryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int customerId,
        string reference,
        decimal debitAmount,
        decimal creditAmount,
        CancellationToken cancellationToken)
    {
        await using (var existing = new SqlCommand(@"
            SELECT TOP (1) CustomerLedgerEntryId, DebitAmount, CreditAmount
            FROM dbo.CustomerLedgerEntries WITH (UPDLOCK, HOLDLOCK)
            WHERE ReferenceNumber = @reference;", connection, transaction))
        {
            existing.Parameters.AddWithValue("@reference", reference);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetDecimal(1) != debitAmount || reader.GetDecimal(2) != creditAmount)
                    throw new InvalidOperationException("مرجع دفتر العميل مستخدم بقيم مختلفة.");
                return reader.GetInt32(0);
            }
        }

        decimal balanceAfter;
        await using (var balance = new SqlCommand(@"
            SELECT TOP (1) BalanceAfterTransaction
            FROM dbo.CustomerLedgerEntries WITH (UPDLOCK, HOLDLOCK)
            WHERE CustomerID = @customerId
            ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC;", connection, transaction))
        {
            balance.Parameters.AddWithValue("@customerId", customerId);
            var value = await balance.ExecuteScalarAsync(cancellationToken);
            balanceAfter = value is null ? 0m : Convert.ToDecimal(value);
        }

        await using var insert = new SqlCommand(@"
            INSERT INTO dbo.CustomerLedgerEntries
                (CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt)
            OUTPUT INSERTED.CustomerLedgerEntryId
            VALUES (@customerId, @reference, @debitAmount, @creditAmount, @balanceAfter, SYSUTCDATETIME());", connection, transaction);
        insert.Parameters.AddWithValue("@customerId", customerId);
        insert.Parameters.AddWithValue("@reference", reference);
        insert.Parameters.AddWithValue("@debitAmount", debitAmount);
        insert.Parameters.AddWithValue("@creditAmount", creditAmount);
        insert.Parameters.AddWithValue("@balanceAfter", balanceAfter + debitAmount - creditAmount);
        return Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> EnsurePaymentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        string reference,
        string description,
        CancellationToken cancellationToken)
    {
        await using (var existing = new SqlCommand(@"
            SELECT TOP (1) PaymentID
            FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
            WHERE PrintHistoryId = @printHistoryId OR ReferenceNo = @reference;", connection, transaction))
        {
            existing.Parameters.AddWithValue("@printHistoryId", history.PrintHistoryId);
            existing.Parameters.AddWithValue("@reference", reference);
            var value = await existing.ExecuteScalarAsync(cancellationToken);
            if (value is not null) return Convert.ToInt32(value);
        }

        await using var insert = new SqlCommand(@"
            INSERT INTO dbo.Payments
                (OrderID, PrintHistoryId, PaymentDate, Amount, PaymentMethod, ReferenceNo, Notes, CreatedDate, PaymentKind)
            OUTPUT INSERTED.PaymentID
            VALUES (@orderId, @printHistoryId, SYSUTCDATETIME(), @amount, N'Cash', @reference, @notes, SYSUTCDATETIME(), N'MeasurementCardPieceSale');", connection, transaction);
        AddNullable(insert, "@orderId", history.OrderId);
        insert.Parameters.AddWithValue("@printHistoryId", history.PrintHistoryId);
        insert.Parameters.AddWithValue("@amount", history.SaleAmount!.Value);
        insert.Parameters.AddWithValue("@reference", reference);
        insert.Parameters.AddWithValue("@notes", description);
        return Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task EnsureActiveEmployeeAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) 1 FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeID = @employeeId AND (IsActive = 1 OR Status = N'Active')",
            connection,
            transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new ArgumentException("الموظف المسؤول غير موجود أو غير فعال.");
    }

    private static async Task EnsureActiveCustomerAsync(SqlConnection connection, SqlTransaction transaction, int customerId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK) WHERE CustomerID = @customerId AND (IsActive = 1 OR IsActive IS NULL)",
            connection,
            transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new ArgumentException("العميل غير موجود أو غير فعال.");
    }

    private static async Task<string> LoadCustomerNameAsync(SqlConnection connection, SqlTransaction transaction, int customerId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) CustomerName FROM dbo.Customers WHERE CustomerID = @customerId", connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value?.ToString()?.Trim() is { Length: > 0 } name ? name : "غير محدد";
    }

    private static async Task<(int? LastCompletedCopyNumber, int CompletedCount)> ReadCompletedCopyStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        var column = isReadyMade ? "ReadyMadePieceId" : "PieceId";
        await using var command = new SqlCommand($@"
            SELECT MAX(CopyNumber), COUNT(*)
            FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
            WHERE {column} = @pieceId AND PrintStatus = N'Completed';", connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<int?> ReadActiveReservationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        var column = isReadyMade ? "ReadyMadePieceId" : "PieceId";
        await using var command = new SqlCommand($@"
            SELECT TOP (1) PrintHistoryId
            FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
            WHERE {column} = @pieceId AND PrintStatus = N'Reserved';", connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static async Task<PieceContext?> LoadPieceContextAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int pieceId,
        bool isReadyMade,
        CancellationToken cancellationToken)
    {
        if (isReadyMade)
        {
            const string readySql = @"
                SELECT p.ReadyMadeProductionOrderPieceInstanceId, p.ReadyMadeProductionOrderItemId,
                       i.ReadyMadeProductionOrderId, o.ProductionOrderNumber, p.TrackingCode,
                       p.PieceStatus
                FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
                INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
                WHERE p.ReadyMadeProductionOrderPieceInstanceId = @pieceId;";
            await using var command = new SqlCommand(readySql, connection, transaction);
            command.Parameters.AddWithValue("@pieceId", pieceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new PieceContext(
                true,
                null,
                null,
                null,
                reader.GetInt32(2),
                reader.GetInt32(1),
                reader.GetInt32(0),
                reader.GetString(3),
                null,
                null,
                reader.GetString(4),
                reader.GetString(5));
        }

        const string tailoringSql = @"
            SELECT p.PieceID, p.OrderItemID, oi.OrderID, o.OrderNumber,
                   p.TrackingCode, p.PieceStatus, o.CustomerID, c.CustomerName
            FROM dbo.Pieces p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            LEFT JOIN dbo.Customers c ON c.CustomerID = o.CustomerID
            WHERE p.PieceID = @pieceId;";
        await using var tailoringCommand = new SqlCommand(tailoringSql, connection, transaction);
        tailoringCommand.Parameters.AddWithValue("@pieceId", pieceId);
        await using var tailoringReader = await tailoringCommand.ExecuteReaderAsync(cancellationToken);
        if (!await tailoringReader.ReadAsync(cancellationToken)) return null;
        return new PieceContext(
            false,
            tailoringReader.GetInt32(0),
            tailoringReader.GetInt32(1),
            tailoringReader.GetInt32(2),
            null,
            null,
            null,
            tailoringReader.GetString(3),
            tailoringReader.GetInt32(6),
            tailoringReader.NullableString("CustomerName"),
            tailoringReader.GetString(4),
            tailoringReader.GetString(5));
    }

    private static async Task<PieceContext?> LoadPieceContextFromHistoryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        MeasurementCardPrintHistoryDto history,
        CancellationToken cancellationToken)
    {
        var pieceId = history.PieceId ?? history.ReadyMadePieceId;
        if (pieceId is not int id) return null;
        return await LoadPieceContextAsync(connection, transaction, id, history.ReadyMadePieceId is not null, cancellationToken);
    }

    private async Task<MeasurementCardPrintHistoryDto?> GetHistoryByIdAsync(int printHistoryId, CancellationToken cancellationToken)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"{HistorySelect} WHERE PrintHistoryId = @printHistoryId", connection);
        command.Parameters.AddWithValue("@printHistoryId", printHistoryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapHistory(reader) : null;
    }

    private static async Task<MeasurementCardPrintHistoryDto?> LoadHistoryByIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int printHistoryId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"{HistorySelect} WITH (UPDLOCK, HOLDLOCK) WHERE PrintHistoryId = @printHistoryId", connection, transaction);
        command.Parameters.AddWithValue("@printHistoryId", printHistoryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapHistory(reader) : null;
    }

    private static async Task<MeasurementCardPrintHistoryDto?> LoadHistoryByRequestIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid printRequestId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"{HistorySelect} WITH (UPDLOCK, HOLDLOCK) WHERE PrintRequestId = @printRequestId", connection, transaction);
        AddGuid(command, "@printRequestId", printRequestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapHistory(reader) : null;
    }

    private static MeasurementCardPrintHistoryDto MapHistory(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetGuid(1),
        reader.GetString(2),
        reader.NullableInt32("OrderId"),
        reader.NullableInt32("OrderItemId"),
        reader.NullableInt32("PieceId"),
        reader.NullableInt32("ReadyMadeProductionOrderId"),
        reader.NullableInt32("ReadyMadeProductionOrderItemId"),
        reader.NullableInt32("ReadyMadePieceId"),
        reader.GetString(9),
        reader.NullableDateTime("PrintedAtUtc"),
        reader.GetInt32(11),
        reader.GetString(12),
        reader.GetInt32(13),
        reader.NullableString("ReprintReasonCode"),
        reader.NullableString("DamageReason"),
        reader.NullableInt32("ResponsibleEmployeeId"),
        reader.NullableString("Notes"),
        reader.NullableDecimal("SaleAmount"),
        reader.NullableString("SalePaymentType"),
        reader.NullableInt32("SaleCustomerId"),
        reader.NullableString("FinancialTransactionReference"),
        reader.NullableInt32("FinancialTransactionId"),
        reader.NullableInt32("JournalEntryId"),
        reader.NullableInt32("CustomerLedgerEntryId"),
        reader.NullableInt32("PaymentId"),
        reader.NullableString("PaymentReferenceNumber"),
        reader.NullableInt32("PaymentFinancialTransactionId"),
        reader.NullableInt32("PaymentJournalEntryId"),
        reader.NullableInt32("PaymentCustomerLedgerEntryId"),
        reader.NullableString("WaiverReferenceNumber"),
        reader.NullableInt32("WaiverFinancialTransactionId"),
        reader.NullableInt32("WaiverJournalEntryId"),
        reader.NullableInt32("WaiverCustomerLedgerEntryId"),
        reader.NullableDateTime("CompletedAtUtc"),
        reader.NullableDateTime("FailedAtUtc"),
        reader.NullableString("FailureReason"),
        reader.GetDateTime(37));

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequired(string? value, string message) =>
        NormalizeOptional(value) ?? throw new ArgumentException(message);

    private static void AddGuid(SqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, System.Data.SqlDbType.UniqueIdentifier).Value = value;

    private static void AddNullable(SqlCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static void AddNullableDecimal(SqlCommand command, string name, decimal? value)
    {
        var parameter = command.Parameters.Add(name, System.Data.SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private sealed record PieceContext(
        bool IsReadyMade,
        int? PieceId,
        int? OrderItemId,
        int? OrderId,
        int? ReadyMadeProductionOrderId,
        int? ReadyMadeProductionOrderItemId,
        int? ReadyMadePieceId,
        string OrderNumber,
        int? CustomerId,
        string? CustomerName,
        string TrackingCode,
        string PieceStatus);
}
