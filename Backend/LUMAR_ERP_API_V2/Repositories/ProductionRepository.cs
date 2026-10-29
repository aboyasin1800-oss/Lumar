using System.Globalization;
using System.Text.Json;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ProductionRepository(
    ReadOnlySqlConnectionFactory connections,
    OperationalSqlConnectionFactory operationalConnections,
    IProductionProductTypeIdentityResolver identities,
    IConsumptionRulesRepository? consumptionRules = null,
    IPricingEngineService? pricingEngine = null) : IProductionRepository
{
    private const string EffectivePieceStatusSql = @"
        SELECT p.PieceID,
               p.OrderItemID,
               p.TrackingCode,
               p.PieceStatus AS EffectivePieceStatus,
               p.PieceNumber,
               p.CreatedDate,
               oi.PieceType,
               o.OrderID,
               o.OrderNumber,
               productType.ProductTypeId
        FROM dbo.Pieces p WITH (NOLOCK)
        INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
        INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
        OUTER APPLY (
            SELECT TOP 1 pt.ProductTypeId
            FROM dbo.PricingProductTypes pt WITH (NOLOCK)
            WHERE pt.IsActive = 1 AND (pt.Code = oi.PieceType OR pt.NameAr = oi.PieceType)
            ORDER BY CASE WHEN pt.Code = oi.PieceType THEN 0 ELSE 1 END, pt.ProductTypeId
        ) productType";

    public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken ct) => QueryAsync($"{EffectivePieceStatusSql} ORDER BY p.CreatedDate DESC, p.PieceID DESC", MapPiece, null, ct);
    public async Task<PieceDto?> GetPieceByIdAsync(int id, CancellationToken ct) => (await QueryAsync($"{EffectivePieceStatusSql} WHERE p.PieceID = @id", MapPiece, id, ct)).SingleOrDefault();

    public Task<IReadOnlyList<PieceDto>> GetReadyMadePiecesAsync(CancellationToken ct) => QueryAsync(@"
        SELECT p.ReadyMadeProductionOrderPieceInstanceId, p.ReadyMadeProductionOrderItemId,
               p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedAt,
               i.PieceType, o.ReadyMadeProductionOrderId, o.ProductionOrderNumber,
               i.ProductTypeId
        FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (NOLOCK)
        INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
        INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
        ORDER BY p.CreatedAt DESC, p.ReadyMadeProductionOrderPieceInstanceId DESC", MapReadyMadePiece, null, ct);

    public async Task<PieceDto?> GetReadyMadePieceByIdAsync(int id, CancellationToken ct) =>
        (await QueryAsync(@"
            SELECT p.ReadyMadeProductionOrderPieceInstanceId, p.ReadyMadeProductionOrderItemId,
                   p.TrackingCode, p.PieceStatus, p.PieceNumber, p.CreatedAt,
                   i.PieceType, o.ReadyMadeProductionOrderId, o.ProductionOrderNumber,
                   i.ProductTypeId
            FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (NOLOCK)
            INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
            INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
            WHERE p.ReadyMadeProductionOrderPieceInstanceId = @id", MapReadyMadePiece, id, ct)).SingleOrDefault();
    public async Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        const string sql = @"
            SELECT p.PieceID,
                   COALESCE(
                       (SELECT TOP 1 te.Stage
                        FROM dbo.TrackingEvents te WITH (NOLOCK)
                        WHERE te.PieceID = p.PieceID
                        ORDER BY te.EventTime DESC, te.TrackingEventID DESC),
                       p.PieceStatus
                   ) AS CurrentStatus,
                   oi.PieceType
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE p.PieceID = @pieceId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        string pieceType;
        string current;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            pieceType = reader.GetString(2);
            current = reader.GetString(1);
        }

        var identity = await identities.ResolveAsync(connection, null, null, pieceType, pieceType, null, ct);
        if (identity is null) return null;
        var route = ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
        if (route.Count == 0) return null;
        return new ProductionTrackingRouteDto(identity.ProductTypeId, identity.Code, identity.NameAr, pieceType, route, current, ProductionTrackingEngine.GetNextStage(identity.ProductTypeId, current));
    }

    public async Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trackingCode)) return null;
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        const string sql = @"
            SELECT p.PieceID,
                   COALESCE(
                       (SELECT TOP 1 te.Stage
                        FROM dbo.TrackingEvents te WITH (NOLOCK)
                        WHERE te.PieceID = p.PieceID
                        ORDER BY te.EventTime DESC, te.TrackingEventID DESC),
                       p.PieceStatus
                   ) AS CurrentStatus,
                   oi.PieceType
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE p.TrackingCode = @trackingCode";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@trackingCode", trackingCode.Trim());
        string pieceType;
        string current;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            pieceType = reader.GetString(2);
            current = reader.GetString(1);
        }

        var identity = await identities.ResolveAsync(connection, null, null, pieceType, pieceType, null, ct);
        if (identity is null) return null;
        var route = ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
        if (route.Count == 0) return null;
        return new ProductionTrackingRouteDto(identity.ProductTypeId, identity.Code, identity.NameAr, pieceType, route, current, ProductionTrackingEngine.GetNextStage(identity.ProductTypeId, current));
    }

    public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteAsync(int pieceId, CancellationToken ct) =>
        GetReadyMadePieceRouteCoreAsync("p.ReadyMadeProductionOrderPieceInstanceId = @id", pieceId, null, ct);

    public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken ct) =>
        GetReadyMadePieceRouteCoreAsync("p.TrackingCode = @trackingCode", null, trackingCode, ct);

    private async Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteCoreAsync(string predicate, int? pieceId, string? trackingCode, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        var sql = $@"
            SELECT p.PieceStatus, i.PieceType, i.ProductTypeId, p.TrackingCode
            FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (NOLOCK)
            INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
            WHERE {predicate};";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", pieceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@trackingCode", string.IsNullOrWhiteSpace(trackingCode) ? (object)DBNull.Value : trackingCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var current = reader.GetString(0);
        var pieceType = reader.GetString(1);
        var productTypeId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
        await reader.CloseAsync();
        var identity = await identities.ResolveAsync(connection, null, productTypeId, pieceType, pieceType, null, ct);
        if (identity is null) return null;
        var route = ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
        if (route.Count == 0) return null;
        return new ProductionTrackingRouteDto(identity.ProductTypeId, identity.Code, identity.NameAr, pieceType, route, current, ProductionTrackingEngine.GetNextStage(identity.ProductTypeId, current), true);
    }

    public async Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken ct)
    {
        if (request is null) return null;

        if (string.IsNullOrWhiteSpace(request.RequestedStage))
        {
            return new ProductionTrackingAdvanceResultDto(request.PieceId, request.TrackingCode, "New", "New", null, "Requested stage is required.", false);
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            var pieceContext = request.IsReadyMade
                ? await LoadReadyMadePieceContextAsync(connection, transaction, request.PieceId, request.TrackingCode, ct)
                : await LoadPieceContextAsync(connection, transaction, request.PieceId, request.TrackingCode, ct);
            if (pieceContext is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(request.PieceId, request.TrackingCode, "New", "New", null, "Piece not found.", false);
            }

            if (request.ProductTypeId <= 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "ProductTypeId is required.", false);
            }

            var identity = await identities.ResolveAsync(
                connection,
                transaction,
                request.ProductTypeId,
                null,
                null,
                null,
                ct);
            if (identity is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "لا يوجد نوع منتج رسمي مطابق لهذه القطعة.", false);
            }

            if (pieceContext.IsReadyMade
                ? pieceContext.ProductTypeId != identity.ProductTypeId
                : await IsTailoringPieceIdentityMismatchAsync(connection, transaction, pieceContext, identity.ProductTypeId, ct))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "هوية نوع المنتج لا تطابق نوع القطعة الرسمي.", false);
            }

            var route = ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
            if (route.Count == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "No approved production route exists for this piece type.", false);
            }

            var finalStage = NormalizeStageForStatus(request.RequestedStage);
            var isFinalReadyMadeStage = pieceContext.IsReadyMade
                && string.Equals(finalStage, route[^1], StringComparison.OrdinalIgnoreCase);
            var sameStageAlreadyApplied = string.Equals(pieceContext.PieceStatus, finalStage, StringComparison.OrdinalIgnoreCase);
            var executionSource = ResolveExecutionSource(request);
            var supervisorAuthorized = request.IsSupervisorReexecution;

            if (executionSource == "Scanner" && RequiresPieceStageAssignment(finalStage))
            {
                var authenticatedEmployeeCode = request.EmployeeCode?.Trim();
                var authenticatedEmployeeId = ResolveEmployeeIdentifier(authenticatedEmployeeCode)
                    ?? (string.IsNullOrWhiteSpace(authenticatedEmployeeCode)
                        ? null
                        : await ResolveEmployeeIdAsync(connection, transaction, authenticatedEmployeeCode, ct));

                if (!authenticatedEmployeeId.HasValue)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "Authenticated employee id is required for the requested piece-stage action.", false);
                }

                var scannerEmployeeCode = await ResolveEmployeeCodeAsync(connection, transaction, request.ScannerCode, request.EmployeeCode, ct);
                var scannerEmployeeId = string.IsNullOrWhiteSpace(scannerEmployeeCode)
                    ? null
                    : await ResolveEmployeeIdAsync(connection, transaction, scannerEmployeeCode, ct);

                if (!scannerEmployeeId.HasValue || scannerEmployeeId.Value != authenticatedEmployeeId.Value)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "Employee account is not the same employee linked to the scanner.", false);
                }

                var hasAssignment = await IsEmployeeAuthorizedForPieceStageAsync(connection, transaction, authenticatedEmployeeId.Value, pieceContext.PieceType, finalStage, ct);
                var guardResult = EmployeeScannerRequestGuard.ValidatePieceStageAssignment(authenticatedEmployeeId, pieceContext.PieceType, finalStage, hasAssignment);
                if (guardResult is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "Employee is not authorized for the requested piece type and stage.", false);
                }
            }

            if (sameStageAlreadyApplied && isFinalReadyMadeStage)
            {
                var recovery = await EnsureReadyMadeInventoryProductAsync(
                    connection, transaction, pieceContext, identity.ProductTypeId, ct);
                if (!recovery.IsAvailableForSale)
                {
                    throw new InvalidOperationException("The ready-made inventory product could not be ensured for the completed piece.");
                }

                await SynchronizeReadyMadeProductionStatusesAsync(connection, transaction, pieceContext.OrderId, ct);
                await transaction.CommitAsync(ct);
                return new ProductionTrackingAdvanceResultDto(
                    pieceContext.PieceId,
                    pieceContext.TrackingCode,
                    pieceContext.PieceStatus,
                    pieceContext.PieceStatus,
                    null,
                    recovery.Created
                        ? "Final stage was already recorded; ready-made inventory was created successfully."
                        : "Final stage and ready-made inventory are already recorded.",
                    true,
                    true);
            }

            var routeContainsRequestedStage = route.Contains(finalStage, StringComparer.OrdinalIgnoreCase);
            if (!routeContainsRequestedStage)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "Requested stage is not present in the official production route for this piece type.", false);
            }

            var supervisorDecision = EvaluateSupervisorReexecutionDecision(
                pieceContext.PieceStatus,
                request.RequestedStage,
                request.IsSupervisorReexecution,
                request is not null && request.IsSupervisorReexecution,
                route.Count > 0,
                routeContainsRequestedStage);
            if (!supervisorDecision.IsAllowed && sameStageAlreadyApplied)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, ProductionTrackingEngine.GetNextStage(identity.ProductTypeId, pieceContext.PieceStatus), "This stage has already been applied to the piece and cannot be repeated without an authorized supervisor re-execution.", false);
            }

            ProductionTrackingTransitionValidation validation;
            if (sameStageAlreadyApplied && supervisorDecision.IsAllowed)
            {
                validation = new ProductionTrackingTransitionValidation(
                    true,
                    pieceContext.PieceStatus,
                    request.RequestedStage,
                    ProductionTrackingEngine.GetNextStage(identity.ProductTypeId, pieceContext.PieceStatus),
                    "Supervisor re-execution allowed for the already-applied stage.");
            }
            else
            {
                validation = ProductionTrackingEngine.ValidateTransition(identity.ProductTypeId, pieceContext.PieceStatus, request.RequestedStage);
            }

            if (!validation.IsAllowed)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, validation.NextStage, validation.Message, false);
            }

            var disposition = pieceContext.IsReadyMade
                ? null
                : await GetDispositionAsync(connection, transaction, pieceContext.PieceId, ct);
            if (string.Equals(disposition?.Decision, "StopAndHold", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "This piece is stopped and held by a cancellation decision.", false);
            }

            if (string.Equals(pieceContext.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) && disposition is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, null, "Order is cancelled and requires an approved disposition decision before progress.", false);
            }

            if (executionSource == "Scanner" && NeedsScanner(request.RequestedStage) && !string.IsNullOrWhiteSpace(request.ScannerCode))
            {
                var scannerValidation = await ValidateScannerAsync(connection, transaction, request.ScannerCode, request.EmployeeCode, request.ProductTypeId, finalStage, ct);
                if (!scannerValidation.IsValid)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, validation.NextStage, scannerValidation.Message, false);
                }
            }
            else if (executionSource == "Scanner" && NeedsScanner(request.RequestedStage) && string.IsNullOrWhiteSpace(request.ScannerCode))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, validation.NextStage, "ScannerCode is required for the requested production stage.", false);
            }

            var nextStage = validation.NextStage;
            if (sameStageAlreadyApplied && !supervisorAuthorized)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, nextStage, "This stage has already been applied to the piece and cannot be repeated without an authorized supervisor re-execution.", false);
            }

            var employeeCode = executionSource == "ManualTest" ? null : await ResolveEmployeeCodeAsync(connection, transaction, request.ScannerCode, request.EmployeeCode, ct);
            var notes = executionSource == "ManualTest"
                ? "ExecutionSource=ManualTest;Operation=ManualProductionAdvance;RequestedStage=" + request.RequestedStage + ";PerformedBySystemUser=admin"
                : request.OperationReference;

            var trackingEventId = await InsertTrackingEventAsync(
                connection,
                transaction,
                pieceContext,
                finalStage,
                request.RequestedStage,
                employeeCode,
                notes,
                request.TrackingCode,
                ct);

            var shouldCreateWage = !pieceContext.IsReadyMade && executionSource == "Scanner" && RequiresPieceWage(finalStage);
            if (shouldCreateWage)
            {
                var wageInsert = await CreatePieceWageRecordAsync(
                    connection,
                    transaction,
                    pieceContext,
                    trackingEventId,
                    finalStage,
                    pieceContext.PieceType,
                    employeeCode,
                    ct);
                if (!wageInsert.IsCreated)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, validation.NextStage, wageInsert.Message, false);
                }
            }

            var updatedRows = await UpdatePieceStatusAsync(connection, transaction, pieceContext.PieceId, finalStage, pieceContext.IsReadyMade, ct);
            if (updatedRows == 0)
            {
                throw new InvalidOperationException("The piece update did not affect the expected row.");
            }

            var hasOrderStartedProduction = !pieceContext.IsReadyMade && pieceContext.OrderId > 0
                && await MaybeUpdateOrderProductionStatusAsync(connection, transaction, pieceContext.OrderId, pieceContext.OrderStatus, ct);

            var readyMadeInventory = pieceContext.IsReadyMade && isFinalReadyMadeStage
                ? await EnsureReadyMadeInventoryProductAsync(connection, transaction, pieceContext, identity.ProductTypeId, ct)
                : ReadyMadeInventoryTransferOutcome.NotRequired;
            if (!readyMadeInventory.IsAvailableForSale)
            {
                throw new InvalidOperationException("The completed ready-made piece was not transferred to ready-made inventory.");
            }

            if (pieceContext.IsReadyMade)
            {
                await SynchronizeReadyMadeProductionStatusesAsync(connection, transaction, pieceContext.OrderId, ct);
            }

            var isOrderReadyForDelivery = !pieceContext.IsReadyMade && pieceContext.OrderId > 0
                && await MaybeUpdateOrderReadyForDeliveryAsync(connection, transaction, pieceContext.OrderId, pieceContext.OrderStatus, ct);
            await transaction.CommitAsync(ct);

            return new ProductionTrackingAdvanceResultDto(
                pieceContext.PieceId,
                pieceContext.TrackingCode,
                pieceContext.PieceStatus,
                finalStage,
                nextStage,
                readyMadeInventory.Created
                    ? "Production stage recorded and piece transferred to ready-made inventory."
                    : isOrderReadyForDelivery
                        ? "Production stage completed and order is ready for delivery."
                        : "Production stage recorded successfully.",
                true,
                pieceContext.IsReadyMade,
                isOrderReadyForDelivery);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<PieceContext?> LoadPieceContextAsync(SqlConnection connection, SqlTransaction transaction, int? pieceId, string? trackingCode, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.PieceID, p.OrderItemID, p.TrackingCode, p.PieceStatus, oi.PieceType, oi.OrderID, o.OrderStatus, o.OrderNumber
            FROM dbo.Pieces p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            WHERE (@pieceId IS NOT NULL AND p.PieceID = @pieceId)
               OR (@trackingCode IS NOT NULL AND p.TrackingCode = @trackingCode);";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@trackingCode", string.IsNullOrWhiteSpace(trackingCode) ? (object)DBNull.Value : trackingCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var pieceIdValue = reader.GetInt32(0);
        var orderItemId = reader.GetInt32(1);
        var trackingCodeValue = reader.GetString(2);
        var pieceStatus = reader.GetString(3);
        var pieceType = reader.GetString(4);
        var orderId = reader.GetInt32(5);
        var orderStatus = reader.GetString(6);
        var orderNumber = reader.GetString(7);
        return new PieceContext(pieceIdValue, orderItemId, trackingCodeValue, pieceStatus, pieceType, orderId, orderStatus, orderNumber);
    }

    private static async Task<PieceContext?> LoadReadyMadePieceContextAsync(SqlConnection connection, SqlTransaction transaction, int? pieceId, string? trackingCode, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.ReadyMadeProductionOrderPieceInstanceId, p.ReadyMadeProductionOrderItemId,
                   p.TrackingCode, p.PieceStatus, i.PieceType, o.ReadyMadeProductionOrderId,
                   o.Status, o.ProductionOrderNumber, i.ProductTypeId
            FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
            INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
            WHERE (@pieceId IS NOT NULL AND p.ReadyMadeProductionOrderPieceInstanceId = @pieceId)
               OR (@trackingCode IS NOT NULL AND p.TrackingCode = @trackingCode);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@trackingCode", string.IsNullOrWhiteSpace(trackingCode) ? (object)DBNull.Value : trackingCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new PieceContext(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetString(7),
            true,
            reader.IsDBNull(8) ? null : reader.GetInt32(8));
    }

    private async Task<bool> IsTailoringPieceIdentityMismatchAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        PieceContext pieceContext,
        int productTypeId,
        CancellationToken ct)
    {
        var identity = await identities.ResolveAsync(
            connection,
            transaction,
            null,
            pieceContext.PieceType,
            pieceContext.PieceType,
            null,
            ct);
        return identity is null || identity.ProductTypeId != productTypeId;
    }

    private static async Task<CancelledPieceDispositionRow?> GetDispositionAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, CancellationToken ct)
    {
        const string sql = "SELECT PieceId, Decision, TransferStatus FROM dbo.CancelledPieceDisposition WITH (NOLOCK) WHERE PieceId = @pieceId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new CancelledPieceDispositionRow(reader.GetInt32(0), reader.GetString(1), reader.NullableString("TransferStatus"));
    }

    private static async Task<ScannerValidationResult> ValidateScannerAsync(SqlConnection connection, SqlTransaction transaction, string? scannerCode, string? employeeCode, int productTypeId, string? stage, CancellationToken ct)
    {
        var effectiveScannerCode = string.IsNullOrWhiteSpace(scannerCode) && !string.IsNullOrWhiteSpace(employeeCode)
            ? EmployeeScannerRequestGuard.MobileEmployeeScannerCode
            : scannerCode;

        if (string.IsNullOrWhiteSpace(effectiveScannerCode))
        {
            return ScannerValidationResult.Invalid("ScannerCode is required for this production stage.");
        }

        if (productTypeId <= 0)
        {
            return ScannerValidationResult.Invalid("ProductTypeId is required for scanner validation.");
        }

        if (string.IsNullOrWhiteSpace(stage))
        {
            return ScannerValidationResult.Invalid("Requested production stage is required for scanner validation.");
        }

        if (EmployeeScannerRequestGuard.IsMobileEmployeeScanner(effectiveScannerCode))
        {
            var mobileEmployeeCode = await ResolveEmployeeCodeAsync(connection, transaction, effectiveScannerCode, employeeCode, ct);
            var mobileRequiresPieceWageEmployee = RequiresPieceWage(stage.Trim());
            if (string.IsNullOrWhiteSpace(mobileEmployeeCode) && mobileRequiresPieceWageEmployee)
            {
                return ScannerValidationResult.Invalid("Piece-wage employee is required for Cutting/Sewing execution.");
            }

            return ScannerValidationResult.Valid(mobileEmployeeCode);
        }

        const string scannerSql = "SELECT ScannerId, ScannerCode, IsActive FROM dbo.Scanners WITH (NOLOCK) WHERE ScannerCode = @scannerCode";
        await using var scannerCommand = new SqlCommand(scannerSql, connection, transaction);
        scannerCommand.Parameters.AddWithValue("@scannerCode", effectiveScannerCode.Trim());
        await using var scannerReader = await scannerCommand.ExecuteReaderAsync(ct);
        if (!await scannerReader.ReadAsync(ct)) return ScannerValidationResult.Invalid("Scanner does not exist.");
        var scannerId = scannerReader.GetInt32(0);
        var isActive = scannerReader.GetBoolean(2);
        if (!isActive) return ScannerValidationResult.Invalid("Scanner is inactive.");

        const string permissionSql = @"
            SELECT TOP (1) 1
            FROM dbo.ScannerStageProductPermissions WITH (NOLOCK)
            WHERE ScannerId = @scannerId
              AND ProductTypeId = @productTypeId
              AND Stage = @stage
              AND IsActive = 1;";
        await using var permissionCommand = new SqlCommand(permissionSql, connection, transaction);
        permissionCommand.Parameters.AddWithValue("@scannerId", scannerId);
        permissionCommand.Parameters.AddWithValue("@productTypeId", productTypeId);
        permissionCommand.Parameters.AddWithValue("@stage", stage.Trim());
        await using var permissionReader = await permissionCommand.ExecuteReaderAsync(ct);
        if (!await permissionReader.ReadAsync(ct))
        {
            return ScannerValidationResult.Invalid("Scanner is not authorized for this product type and stage.");
        }

        var employeeCodeResult = await ResolveEmployeeCodeAsync(connection, transaction, effectiveScannerCode, employeeCode, ct);
        var requiresPieceWageEmployee = RequiresPieceWage(stage.Trim());
        if (string.IsNullOrWhiteSpace(employeeCodeResult) && requiresPieceWageEmployee)
        {
            return ScannerValidationResult.Invalid("Piece-wage employee is required for Cutting/Sewing execution.");
        }

        return ScannerValidationResult.Valid(employeeCodeResult);
    }

    private static async Task<string?> ResolveEmployeeCodeAsync(SqlConnection connection, SqlTransaction transaction, string? scannerCode, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scannerCode) && string.IsNullOrWhiteSpace(employeeCode)) return null;

        var effectiveScannerCode = string.IsNullOrWhiteSpace(scannerCode) && !string.IsNullOrWhiteSpace(employeeCode)
            ? EmployeeScannerRequestGuard.MobileEmployeeScannerCode
            : scannerCode;

        if (EmployeeScannerRequestGuard.IsMobileEmployeeScanner(effectiveScannerCode))
        {
            if (ResolveEmployeeIdentifier(employeeCode) is var employeeId && employeeId.HasValue)
            {
                const string employeeIdSql = "SELECT EmployeeCode FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeID = @employeeId AND IsActive = 1 ORDER BY EmployeeID";
                await using var command = new SqlCommand(employeeIdSql, connection, transaction);
                command.Parameters.AddWithValue("@employeeId", employeeId.Value);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    return reader.IsDBNull(0) ? null : reader.GetString(0);
                }
            }

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                const string employeeCodeSql = "SELECT EmployeeCode FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeCode = @employeeCode AND IsActive = 1 ORDER BY EmployeeID";
                await using var command = new SqlCommand(employeeCodeSql, connection, transaction);
                command.Parameters.AddWithValue("@employeeCode", employeeCode.Trim());
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    return reader.IsDBNull(0) ? null : reader.GetString(0);
                }
            }

            return null;
        }

        const string sql = "SELECT EmployeeID, EmployeeCode, ScannerCode, IsActive FROM dbo.Employees WITH (NOLOCK) WHERE ScannerCode = @scannerCode ORDER BY EmployeeID";
        await using var command2 = new SqlCommand(sql, connection, transaction);
        command2.Parameters.AddWithValue("@scannerCode", scannerCode.Trim());
        await using var reader2 = await command2.ExecuteReaderAsync(ct);
        var employees = new List<string>();
        while (await reader2.ReadAsync(ct))
        {
            if (reader2.GetBoolean(3)) employees.Add(reader2.GetString(1));
        }

        if (employees.Count == 0) return null;
        if (employees.Count > 1) throw new InvalidOperationException("More than one active employee is associated with the same scanner code. This requires review before progress can continue.");
        if (!string.IsNullOrWhiteSpace(employeeCode) && !string.Equals(employeeCode.Trim(), employees[0], StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The supplied employee code does not match the active employee linked to the scanner.");
        }

        return employees[0];
    }

    private static async Task<PieceWageInsertOutcome> CreatePieceWageRecordAsync(SqlConnection connection, SqlTransaction transaction, PieceContext pieceContext, int trackingEventId, string stage, string pieceTypeCode, string? employeeCode, CancellationToken ct)
    {
        var rateList = await LoadPieceWageRatesAsync(connection, transaction, ct);

        var employeeId = await ResolveEmployeeIdAsync(connection, transaction, employeeCode, ct);
        if (!employeeId.HasValue)
        {
            return new PieceWageInsertOutcome(false, "The employee linked to the tracking event could not be resolved for piece wage creation.");
        }

        var employeeExtra = await LoadEmployeePieceRateExtraAsync(connection, transaction, employeeId.Value, pieceTypeCode, stage, ct);
        var rateResolution = PieceWageEngine.ResolveRate(pieceTypeCode, stage, rateList, employeeExtra);
        if (!rateResolution.IsValid)
        {
            return new PieceWageInsertOutcome(false, rateResolution.Message);
        }

        var existingWages = await CountPieceWageRecordsForTrackingEventAsync(connection, transaction, trackingEventId, ct);
        if (existingWages > 0)
        {
            return new PieceWageInsertOutcome(false, "A PieceWageRecord already exists for this TrackingEvent.");
        }

        var quantity = 1m;
        var finalRate = rateResolution.WageRate!.Value;
        var totalWage = PieceWageEngine.CalculateTotalWage(quantity, finalRate);
        var validation = PieceWageEngine.ValidateRecord(pieceTypeCode, stage, finalRate, employeeCode, quantity, existingWages);
        if (!validation.IsValid)
        {
            return new PieceWageInsertOutcome(false, validation.Message);
        }

        const string sql = @"
            INSERT INTO dbo.PieceWageRecords
                (OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt)
            VALUES (@orderId, @orderItemId, @pieceId, @trackingEventId, @employeeId, @employeeCode, @pieceType, @stage, @quantity, @wageRate, @totalWage, NULL, NULL, @status, @notes, @createdAt);";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderId", pieceContext.OrderId);
        command.Parameters.AddWithValue("@orderItemId", pieceContext.OrderItemId);
        command.Parameters.AddWithValue("@pieceId", pieceContext.PieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        command.Parameters.AddWithValue("@employeeId", employeeId.Value);
        command.Parameters.AddWithValue("@employeeCode", employeeCode?.Trim() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@pieceType", pieceTypeCode);
        command.Parameters.AddWithValue("@stage", stage);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@wageRate", finalRate);
        command.Parameters.AddWithValue("@totalWage", validation.TotalWage);
        command.Parameters.AddWithValue("@status", "PendingPayroll");
        command.Parameters.AddWithValue("@notes", $"Auto-created from TrackingEvent {trackingEventId}");
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(ct);
        return new PieceWageInsertOutcome(true, "Piece wage created successfully.");
    }

    private static async Task<IReadOnlyList<LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto>> LoadPieceWageRatesAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        const string sql = "SELECT PieceWageRateID, PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt FROM dbo.PieceWageRates WITH (NOLOCK) WHERE IsActive = 1 ORDER BY PieceType, Stage, PieceWageRateID";
        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetBoolean(4),
                reader.NullableString("Notes"),
                reader.GetDateTime(6),
                reader.NullableDateTime("UpdatedAt")));
        }

        return results;
    }

    private static async Task<decimal> LoadEmployeePieceRateExtraAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string pieceType, string stage, CancellationToken ct)
    {
        const string sql = @"SELECT TOP 1 Rate
            FROM dbo.EmployeePieceRateAssignments WITH (NOLOCK)
            WHERE EmployeeId = @employeeId
              AND PieceType = @pieceType
              AND Stage = @stage
              AND IsActive = 1
              AND (EffectiveFrom IS NULL OR EffectiveFrom <= @now)
              AND (EffectiveTo IS NULL OR EffectiveTo >= @now)
            ORDER BY EffectiveFrom DESC, EmployeePieceRateAssignmentId DESC;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@pieceType", pieceType.Trim());
        command.Parameters.AddWithValue("@stage", stage.Trim());
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0m;
        return reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
    }

    private static async Task<bool> IsEmployeeAuthorizedForPieceStageAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string pieceType, string stage, CancellationToken ct)
    {
        if (employeeId <= 0 || string.IsNullOrWhiteSpace(pieceType) || string.IsNullOrWhiteSpace(stage))
            return false;

        const string sql = @"SELECT TOP 1 1
            FROM dbo.EmployeePieceRateAssignments WITH (NOLOCK)
            WHERE EmployeeId = @employeeId
              AND PieceType = @pieceType
              AND Stage = @stage
              AND IsActive = 1
              AND (EffectiveFrom IS NULL OR EffectiveFrom <= @now)
              AND (EffectiveTo IS NULL OR EffectiveTo >= @now);";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@pieceType", pieceType.Trim());
        command.Parameters.AddWithValue("@stage", stage.Trim());
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    private static async Task<int> CountPieceWageRecordsForTrackingEventAsync(SqlConnection connection, SqlTransaction transaction, int trackingEventId, CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.PieceWageRecords WITH (UPDLOCK, HOLDLOCK) WHERE TrackingEventID = @trackingEventId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    private static async Task<int?> ResolveEmployeeIdAsync(SqlConnection connection, SqlTransaction transaction, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return null;

        var trimmed = employeeCode.Trim();
        if (ResolveEmployeeIdentifier(trimmed) is var employeeId && employeeId.HasValue)
        {
            const string sql = "SELECT EmployeeID FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeID = @employeeId AND IsActive = 1 ORDER BY EmployeeID";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@employeeId", employeeId.Value);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return reader.GetInt32(0);
            }
        }

        const string codeSql = "SELECT EmployeeID FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeCode = @employeeCode AND IsActive = 1 ORDER BY EmployeeID";
        await using var codeCommand = new SqlCommand(codeSql, connection, transaction);
        codeCommand.Parameters.AddWithValue("@employeeCode", trimmed);
        await using var codeReader = await codeCommand.ExecuteReaderAsync(ct);
        if (!await codeReader.ReadAsync(ct)) return null;
        return codeReader.GetInt32(0);
    }

    private static async Task<string?> ResolveEmployeeJobTitleAsync(SqlConnection connection, SqlTransaction transaction, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return null;

        const string sql = "SELECT JobTitle FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeCode = @employeeCode AND IsActive = 1 ORDER BY EmployeeID";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeCode", employeeCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.IsDBNull(0) ? null : reader.GetString(0);
    }

    private static async Task<int> InsertTrackingEventAsync(SqlConnection connection, SqlTransaction transaction, PieceContext pieceContext, string stage, string requestedStage, string? employeeCode, string? operationReference, string? trackingCode, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.TrackingEvents
                (OrderItemID, OrderID, TrackingCode, Stage, Status, EventTime, EmployeeCode, Notes, IsReverted, RevertedAt, PieceID, ReadyMadeProductionOrderPieceInstanceId)
            OUTPUT INSERTED.TrackingEventID
            VALUES (@orderItemId, @orderId, @trackingCode, @stage, @status, @eventTime, @employeeCode, @notes, 0, NULL, @pieceId, @readyMadePieceInstanceId);";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@orderItemId", pieceContext.IsReadyMade ? (object)DBNull.Value : pieceContext.OrderItemId);
        command.Parameters.AddWithValue("@orderId", pieceContext.IsReadyMade ? (object)DBNull.Value : pieceContext.OrderId);
        command.Parameters.AddWithValue("@trackingCode", pieceContext.TrackingCode);
        command.Parameters.AddWithValue("@stage", stage);
        command.Parameters.AddWithValue("@status", NormalizeStageForStatus(requestedStage));
        command.Parameters.AddWithValue("@eventTime", DateTime.UtcNow);
        command.Parameters.AddWithValue("@employeeCode", string.IsNullOrWhiteSpace(employeeCode) ? (object)DBNull.Value : employeeCode.Trim());
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(operationReference) ? (object)DBNull.Value : operationReference.Trim());
        command.Parameters.AddWithValue("@pieceId", pieceContext.IsReadyMade ? (object)DBNull.Value : pieceContext.PieceId);
        command.Parameters.AddWithValue("@readyMadePieceInstanceId", pieceContext.IsReadyMade ? (object)pieceContext.PieceId : DBNull.Value);
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    private static async Task<int> UpdatePieceStatusAsync(SqlConnection connection, SqlTransaction transaction, int pieceId, string stage, bool isReadyMade, CancellationToken ct)
    {
        var sql = isReadyMade
            ? "UPDATE dbo.ReadyMadeProductionOrderPieceInstances SET PieceStatus = @pieceStatus WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId"
            : "UPDATE dbo.Pieces SET PieceStatus = @pieceStatus WHERE PieceID = @pieceId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceStatus", stage);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<ReadyMadeInventoryTransferOutcome> EnsureReadyMadeInventoryProductAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        PieceContext pieceContext,
        int productTypeId,
        CancellationToken ct)
    {
        const string existingSql = @"
            SELECT ReadyMadeInventoryProductId
            FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
            WHERE ReadyMadeProductionOrderPieceInstanceId = @pieceId;";
        await using var existingCommand = new SqlCommand(existingSql, connection, transaction);
        existingCommand.Parameters.AddWithValue("@pieceId", pieceContext.PieceId);
        var existingProductId = await existingCommand.ExecuteScalarAsync(ct);
        if (existingProductId is not null)
        {
            return new ReadyMadeInventoryTransferOutcome(Convert.ToInt32(existingProductId), false, true);
        }

        const string contextSql = @"
            SELECT o.ProductionOrderNumber, o.ProductionName, o.SuggestedSellingPrice,
                   i.ReadyMadeProductionOrderItemId, i.ProductTypeId, i.PieceType,
                   i.FabricCode, i.FabricType, i.FabricColor, i.CatalogNumber,
                   i.PieceCost, i.MeasurementSnapshot,
                   p.PieceNumber, p.TrackingCode
            FROM dbo.ReadyMadeProductionOrderPieceInstances p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
            INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
            WHERE p.ReadyMadeProductionOrderPieceInstanceId = @pieceId;";
        await using var contextCommand = new SqlCommand(contextSql, connection, transaction);
        contextCommand.Parameters.AddWithValue("@pieceId", pieceContext.PieceId);
        await using var reader = await contextCommand.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return ReadyMadeInventoryTransferOutcome.Failed;
        }

        var productionOrderNumber = reader.GetString(0);
        var productionName = reader.GetString(1);
        _ = reader.GetDecimal(2);
        var itemId = reader.GetInt32(3);
        var officialProductTypeId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
        var pieceType = reader.GetString(5);
        var fabricCode = reader.NullableString("FabricCode");
        var fabricType = reader.NullableString("FabricType");
        var fabricColor = reader.NullableString("FabricColor");
        var catalogNumber = reader.NullableString("CatalogNumber");
        var pricingSnapshot = ReadPricingSnapshot(reader.NullableString("MeasurementSnapshot"))
            ?? throw new InvalidOperationException("لقطة التسعير الرسمية مفقودة للقطعة المكتملة.");
        var actualCost = pricingSnapshot.FullCostPerPiece;
        var suggestedSellingPrice = pricingSnapshot.FinalPricePerPiece;
        if (actualCost <= 0m || suggestedSellingPrice <= 0m)
        {
            throw new InvalidOperationException("التكلفة أو سعر البيع الرسمي للقطعة غير صالح.");
        }
        var measurementSnapshot = reader.NullableString("MeasurementSnapshot");
        var pieceNumber = reader.GetInt32(12);
        var trackingCode = reader.GetString(13);
        await reader.CloseAsync();

        if (officialProductTypeId != productTypeId)
        {
            throw new InvalidOperationException("The ready-made piece ProductTypeId does not match its official item ProductTypeId.");
        }

        const string insertSql = @"
            INSERT INTO dbo.ReadyMadeInventoryProducts
                (ReadyMadeProductionOrderId, ReadyMadeProductionOrderItemId, ReadyMadeProductionOrderPieceInstanceId,
                 ProductTypeId, ProductionOrderNumber, ProductionName, PieceType, PieceNumber, TrackingCode,
                 FabricCode, FabricType, FabricColor, CatalogNumber, FabricUnit, FabricWidth, FabricWidthUnit,
                 ActualCost, SuggestedSellingPrice, MeasurementSnapshot, ReadyForSaleAt, Status, Source, Notes, IsActive, CreatedAt)
            OUTPUT INSERTED.ReadyMadeInventoryProductId
            VALUES
                (@orderId, @itemId, @pieceId,
                 @productTypeId, @productionOrderNumber, @productionName, @pieceType, @pieceNumber, @trackingCode,
                 @fabricCode, @fabricType, @fabricColor, @catalogNumber, N'Piece', NULL, NULL,
                 @actualCost, @suggestedSellingPrice, @measurementSnapshot, @now, N'AvailableForSale', N'OurProduction', N'Automatically transferred after the final production stage.', 1, @now);";
        await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("@orderId", pieceContext.OrderId);
        insertCommand.Parameters.AddWithValue("@itemId", itemId);
        insertCommand.Parameters.AddWithValue("@pieceId", pieceContext.PieceId);
        insertCommand.Parameters.AddWithValue("@productTypeId", officialProductTypeId);
        insertCommand.Parameters.AddWithValue("@productionOrderNumber", productionOrderNumber);
        insertCommand.Parameters.AddWithValue("@productionName", productionName);
        insertCommand.Parameters.AddWithValue("@pieceType", pieceType);
        insertCommand.Parameters.AddWithValue("@pieceNumber", pieceNumber);
        insertCommand.Parameters.AddWithValue("@trackingCode", trackingCode);
        AddNullable(insertCommand, "@fabricCode", fabricCode);
        AddNullable(insertCommand, "@fabricType", fabricType);
        AddNullable(insertCommand, "@fabricColor", fabricColor);
        AddNullable(insertCommand, "@catalogNumber", catalogNumber);
        insertCommand.Parameters.AddWithValue("@actualCost", actualCost);
        insertCommand.Parameters.AddWithValue("@suggestedSellingPrice", suggestedSellingPrice);
        AddNullable(insertCommand, "@measurementSnapshot", measurementSnapshot);
        insertCommand.Parameters.AddWithValue("@now", DateTime.UtcNow);
        var productId = await insertCommand.ExecuteScalarAsync(ct);
        if (productId is null)
        {
            return ReadyMadeInventoryTransferOutcome.Failed;
        }

        var readyMadeInventoryProductId = Convert.ToInt32(productId);
        var postingAmount = decimal.Round(actualCost, 2, MidpointRounding.AwayFromZero);
        var accountingPosting = await AccountingEventPostingGateway.PostAsync(
            connection,
            transaction,
            AccountingEventType.WipToFinishedGoods,
            postingAmount,
            null,
            null,
            null,
            readyMadeInventoryProductId,
            $"{trackingCode}:WipToFinishedGoods",
            $"Ready-made WIP transfer for {trackingCode}",
            ct);
        var inventoryItemId = await InsertReadyMadeInventoryItemAsync(
            connection,
            transaction,
            readyMadeInventoryProductId,
            productionName,
            trackingCode,
            DateTime.UtcNow,
            ct);
        await InsertReadyMadeInventoryReceiptTransactionAsync(
            connection,
            transaction,
            inventoryItemId,
            readyMadeInventoryProductId,
            actualCost,
            accountingPosting.AccountingEventId,
            trackingCode,
            DateTime.UtcNow,
            ct);

        return new ReadyMadeInventoryTransferOutcome(readyMadeInventoryProductId, true, true);
    }

    private static async Task<int> InsertReadyMadeInventoryItemAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int readyMadeInventoryProductId,
        string productionName,
        string trackingCode,
        DateTime now,
        CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.InventoryItems
                (ItemCode, ItemName, Category, Unit, CurrentQuantity, AvailableQuantity, ReservedQuantity,
                 IsActive, CreatedAt, Barcode, FabricCategory, FabricColor, FabricWidth, FabricWidthUnit, InchPrice, YardPrice)
            OUTPUT INSERTED.InventoryItemID
            VALUES
                (@itemCode, @itemName, N'ReadyMadeProduct', N'Piece', 1, 1, 0,
                 1, @createdAt, @barcode, N'ReadyMadeProduct', N'غير محدد', 0, N'Piece', 0, 0);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@itemCode", $"RMP-{readyMadeInventoryProductId}");
        command.Parameters.AddWithValue("@itemName", productionName);
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@barcode", trackingCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task InsertReadyMadeInventoryReceiptTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int inventoryItemId,
        int readyMadeInventoryProductId,
        decimal actualCost,
        long accountingEventId,
        string trackingCode,
        DateTime now,
        CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt,
                 TotalCostImpact, UnitCost, AccountingEventId, ReadyMadeInventoryProductId, OperationalCostImpact)
            VALUES
                (@inventoryItemId, N'Receive', 1, @referenceNumber, N'Ready-made product WIP transfer', @createdAt,
                 @operationalAmount, @unitCost, @accountingEventId, @readyMadeInventoryProductId, @operationalAmount);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@inventoryItemId", inventoryItemId);
        command.Parameters.AddWithValue("@referenceNumber", $"{trackingCode}:WipToFinishedGoods");
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@operationalAmount", actualCost);
        command.Parameters.AddWithValue("@unitCost", actualCost);
        command.Parameters.AddWithValue("@accountingEventId", accountingEventId);
        command.Parameters.AddWithValue("@readyMadeInventoryProductId", readyMadeInventoryProductId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task SynchronizeReadyMadeProductionStatusesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        CancellationToken ct)
    {
        const string piecesSql = @"
            SELECT i.ReadyMadeProductionOrderItemId, i.ProductTypeId, p.PieceStatus
            FROM dbo.ReadyMadeProductionOrderItems i WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ReadyMadeProductionOrderPieceInstances p ON p.ReadyMadeProductionOrderItemId = i.ReadyMadeProductionOrderItemId
            WHERE i.ReadyMadeProductionOrderId = @orderId;";
        await using var piecesCommand = new SqlCommand(piecesSql, connection, transaction);
        piecesCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await piecesCommand.ExecuteReaderAsync(ct);
        var statusesByItem = new Dictionary<int, List<(int ProductTypeId, string Status)>>();
        while (await reader.ReadAsync(ct))
        {
            var itemId = reader.GetInt32(0);
            var productTypeId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            var status = reader.GetString(2);
            if (!statusesByItem.TryGetValue(itemId, out var statuses))
            {
                statuses = [];
                statusesByItem[itemId] = statuses;
            }
            statuses.Add((productTypeId, status));
        }
        await reader.CloseAsync();

        var allCompleted = statusesByItem.Count > 0;
        var anyProgress = false;
        foreach (var (itemId, statuses) in statusesByItem)
        {
            var itemCompleted = statuses.Count > 0 && statuses.All(status =>
            {
                var route = ProductionTrackingEngine.GetRoute(status.ProductTypeId);
                return route.Count > 0 && string.Equals(status.Status, route[^1], StringComparison.OrdinalIgnoreCase);
            });
            var itemHasProgress = statuses.Any(status => !string.Equals(status.Status, "New", StringComparison.OrdinalIgnoreCase));
            anyProgress |= itemHasProgress;
            allCompleted &= itemCompleted;

            var itemStatus = itemCompleted ? "Completed" : itemHasProgress ? "InProduction" : "New";
            await using var itemCommand = new SqlCommand(
                "UPDATE dbo.ReadyMadeProductionOrderItems SET PieceStatus = @status WHERE ReadyMadeProductionOrderItemId = @itemId",
                connection,
                transaction);
            itemCommand.Parameters.AddWithValue("@status", itemStatus);
            itemCommand.Parameters.AddWithValue("@itemId", itemId);
            await itemCommand.ExecuteNonQueryAsync(ct);
        }

        var orderStatus = allCompleted ? "Completed" : anyProgress ? "InProduction" : "New";
        await using var orderCommand = new SqlCommand(
            "UPDATE dbo.ReadyMadeProductionOrders SET Status = @status WHERE ReadyMadeProductionOrderId = @orderId",
            connection,
            transaction);
        orderCommand.Parameters.AddWithValue("@status", orderStatus);
        orderCommand.Parameters.AddWithValue("@orderId", orderId);
        await orderCommand.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> MaybeUpdateOrderProductionStatusAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string currentOrderStatus, CancellationToken ct)
    {
        if (string.Equals(currentOrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentOrderStatus, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentOrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string sql = @"
            SELECT p.PieceStatus, oi.PieceType
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE oi.OrderID = @orderId;";
        await using var piecesCommand = new SqlCommand(sql, connection, transaction);
        piecesCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await piecesCommand.ExecuteReaderAsync(ct);
        var totalPieces = 0;
        var completedPieces = 0;
        var cancelledPieces = 0;
        var hasBlockedPieces = false;
        var hasStartedPiece = false;
        while (await reader.ReadAsync(ct))
        {
            totalPieces++;
            var pieceStatus = reader.GetString(0);
            var pieceType = reader.GetString(1);
            var identity = await identities.ResolveAsync(connection, transaction, null, pieceType, pieceType, null, ct);
            var route = identity is null ? Array.Empty<string>() : ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
            var lastStage = route.LastOrDefault();
            var completed = string.Equals(pieceStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pieceStatus, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pieceStatus, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(lastStage) && string.Equals(pieceStatus, lastStage, StringComparison.OrdinalIgnoreCase));
            if (completed) completedPieces++;
            if (!string.Equals(pieceStatus, "New", StringComparison.OrdinalIgnoreCase))
            {
                hasStartedPiece = true;
            }
            if (string.Equals(pieceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)) cancelledPieces++;
            if (string.Equals(pieceStatus, "Blocked", StringComparison.OrdinalIgnoreCase) || string.Equals(pieceStatus, "Hold", StringComparison.OrdinalIgnoreCase)) hasBlockedPieces = true;
        }

        if (totalPieces <= 0)
        {
            return false;
        }

        var nextStatus = hasStartedPiece
            ? ProductionTrackingEngine.DetermineOrderStatus(completedPieces, totalPieces, cancelledPieces, hasBlockedPieces, currentOrderStatus)
            : currentOrderStatus;
        if (string.Equals(nextStatus, currentOrderStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string updateSql = "UPDATE dbo.Orders SET OrderStatus = @status WHERE OrderID = @orderId AND OrderStatus <> N'Cancelled'";
        await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@status", nextStatus);
        updateCommand.Parameters.AddWithValue("@orderId", orderId);
        return await updateCommand.ExecuteNonQueryAsync(ct) > 0;
    }

    private async Task<bool> MaybeUpdateOrderReadyForDeliveryAsync(SqlConnection connection, SqlTransaction transaction, int orderId, string currentOrderStatus, CancellationToken ct)
    {
        if (string.Equals(currentOrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string sql = @"
            SELECT p.PieceStatus, oi.PieceType
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE oi.OrderID = @orderId;";
        await using var piecesCommand = new SqlCommand(sql, connection, transaction);
        piecesCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await piecesCommand.ExecuteReaderAsync(ct);
        var totalPieces = 0;
        var completedPieces = 0;
        while (await reader.ReadAsync(ct))
        {
            totalPieces++;
            var pieceStatus = reader.GetString(0);
            var pieceType = reader.GetString(1);
            var identity = await identities.ResolveAsync(connection, transaction, null, pieceType, pieceType, null, ct);
            var route = identity is null ? Array.Empty<string>() : ProductionTrackingEngine.GetRoute(identity.ProductTypeId);
            var lastStage = route.LastOrDefault();
            var completed = string.Equals(pieceStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pieceStatus, "Ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pieceStatus, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(lastStage) && string.Equals(pieceStatus, lastStage, StringComparison.OrdinalIgnoreCase));
            if (completed) completedPieces++;
        }

        if (totalPieces <= 0 || completedPieces != totalPieces)
        {
            return false;
        }

        await EnsureTailoringCostPostingAsync(connection, transaction, orderId, ct);

        const string updateSql = "UPDATE dbo.Orders SET OrderStatus = N'ReadyForDelivery' WHERE OrderID = @orderId AND OrderStatus <> N'Cancelled'";
        await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@orderId", orderId);
        return await updateCommand.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task EnsureTailoringCostPostingAsync(SqlConnection connection, SqlTransaction transaction, int orderId, CancellationToken ct)
    {
        const string sourceSql = @"
            SELECT COALESCE(SUM(s.PostingAmount), 0), COALESCE(SUM(s.OperationalAmount), 0), COUNT_BIG(*)
            FROM dbo.FabricConsumptionSources s WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.OrderItems oi WITH (UPDLOCK, HOLDLOCK) ON oi.OrderItemID=s.OrderItemId
            WHERE oi.OrderID=@orderId AND s.OrderItemId IS NOT NULL AND s.AccountingEventId IS NOT NULL;";
        await using var sourceCommand = new SqlCommand(sourceSql, connection, transaction);
        sourceCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var sourceReader = await sourceCommand.ExecuteReaderAsync(ct);
        if (!await sourceReader.ReadAsync(ct))
            throw new InvalidOperationException("تعذر قراءة تكلفة قماش الطلب قبل الجاهزية.");
        var postingAmount = sourceReader.GetDecimal(0);
        var sourceCount = sourceReader.GetInt64(2);
        await sourceReader.DisposeAsync();
        if (sourceCount <= 0 || postingAmount <= 0m)
            throw new InvalidOperationException("لا توجد تكلفة قماش مصروفة ومثبتة لهذا الطلب.");

        var sourceOperationId = CreateTailoringCostOperationId(orderId);
        const string existingSql = @"
            SELECT TOP (1) TailoringCostPostingId, PostingAmount, AccountingEventId
            FROM dbo.TailoringCostPostings WITH (UPDLOCK, HOLDLOCK)
            WHERE OrderId=@orderId
            ORDER BY TailoringCostPostingId DESC;";
        await using var existingCommand = new SqlCommand(existingSql, connection, transaction);
        existingCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var existingReader = await existingCommand.ExecuteReaderAsync(ct);

        long candidatePostingId = 0;
        decimal candidatePostingAmount = 0m;
        long? candidateAccountingEventId = null;
        if (await existingReader.ReadAsync(ct))
        {
            candidatePostingId = existingReader.GetInt64(0);
            candidatePostingAmount = existingReader.GetDecimal(1);
            candidateAccountingEventId = existingReader.IsDBNull(2) ? null : existingReader.GetInt64(2);
        }
        await existingReader.DisposeAsync();

        if (candidatePostingId > 0)
        {
            if (Math.Abs(candidatePostingAmount - postingAmount) < 0.01m)
            {
                if (candidateAccountingEventId.HasValue)
                    return;

                var repairPostingId = candidatePostingId;
                var repairReference = $"Order:{orderId}:TailoringCost";
                const string repairFinancialSql = @"
                    INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt)
                    VALUES(@reference,N'TailoringCost',@amount,@description,SYSUTCDATETIME());";
                await using (var repairFinancialCommand = new SqlCommand(repairFinancialSql, connection, transaction))
                {
                    repairFinancialCommand.Parameters.AddWithValue("@reference", repairReference);
                    AddDecimal(repairFinancialCommand, "@amount", postingAmount, 2);
                    repairFinancialCommand.Parameters.AddWithValue("@description", $"Tailoring cost of sales for order {orderId}");
                    await repairFinancialCommand.ExecuteNonQueryAsync(ct);
                }

                var repairJournal = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                    connection,
                    transaction,
                    repairReference,
                    "TailoringCost",
                    postingAmount,
                    $"Tailoring cost of sales for order {orderId}",
                    ct,
                    "TailoringCostPosting",
                    repairPostingId,
                    sourceOperationId);
                repairJournal.ThrowIfFailure();

                const string repairLinkSql = @"
                    UPDATE p SET AccountingEventId=j.AccountingEventId
                    FROM dbo.TailoringCostPostings p
                    INNER JOIN dbo.JournalEntries j ON j.JournalEntryId=@journalEntryId
                    WHERE p.TailoringCostPostingId=@postingId;";
                await using var repairLinkCommand = new SqlCommand(repairLinkSql, connection, transaction);
                repairLinkCommand.Parameters.AddWithValue("@journalEntryId", repairJournal.JournalEntryId!.Value);
                repairLinkCommand.Parameters.AddWithValue("@postingId", repairPostingId);
                if (await repairLinkCommand.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("تعذر ربط قيد تكلفة التفصيل بمصدره.");
                return;
            }

            const string repairPostingSql = @"
                UPDATE p
                SET FabricOperationalAmount=@operationalAmount,
                    PostingAmount=@postingAmount
                FROM dbo.TailoringCostPostings p
                WHERE p.TailoringCostPostingId=@postingId;";
            await using (var repairPostingCommand = new SqlCommand(repairPostingSql, connection, transaction))
            {
                repairPostingCommand.Parameters.AddWithValue("@postingId", candidatePostingId);
                AddDecimal(repairPostingCommand, "@operationalAmount", postingAmount);
                AddDecimal(repairPostingCommand, "@postingAmount", postingAmount, 2);
                if (await repairPostingCommand.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("تعذر تحديث قيمة تكلفة التفصيل لتطابق إجمالي القماش الرسمي.");
            }

            if (candidateAccountingEventId.HasValue)
            {
                const string repairAccountingSql = @"
                    UPDATE ae
                    SET PostingAmount=@postingAmount
                    FROM dbo.AccountingEvents ae
                    WHERE ae.AccountingEventId=@accountingEventId;

                    UPDATE ft
                    SET Amount=@postingAmount
                    FROM dbo.FinancialTransactions ft
                    WHERE ft.AccountingEventId=@accountingEventId
                      AND ft.TransactionType = N'TailoringCost';

                    UPDATE jel
                    SET DebitAmount = CASE WHEN la.AccountCode = N'5200' THEN @postingAmount ELSE 0 END,
                        CreditAmount = CASE WHEN la.AccountCode = N'1130' THEN @postingAmount ELSE 0 END
                    FROM dbo.JournalEntryLines jel
                    INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
                    INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
                    WHERE je.AccountingEventId = @accountingEventId
                      AND la.AccountCode IN (N'5200', N'1130');";
                await using (var repairAccountingCommand = new SqlCommand(repairAccountingSql, connection, transaction))
                {
                    repairAccountingCommand.Parameters.AddWithValue("@accountingEventId", candidateAccountingEventId.Value);
                    AddDecimal(repairAccountingCommand, "@postingAmount", postingAmount, 2);
                    await repairAccountingCommand.ExecuteNonQueryAsync(ct);
                }

                const string repairLinkSql = @"
                    UPDATE p SET AccountingEventId=@accountingEventId
                    FROM dbo.TailoringCostPostings p
                    WHERE p.TailoringCostPostingId=@postingId;";
                await using var repairLinkCommand = new SqlCommand(repairLinkSql, connection, transaction);
                repairLinkCommand.Parameters.AddWithValue("@accountingEventId", candidateAccountingEventId.Value);
                repairLinkCommand.Parameters.AddWithValue("@postingId", candidatePostingId);
                if (await repairLinkCommand.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("تعذر ربط قيد تكلفة التفصيل بمصدره بعد الإصلاح.");
                return;
            }

            var repairReference2 = $"Order:{orderId}:TailoringCost";
            const string repairFinancialSql2 = @"
                INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt)
                VALUES(@reference,N'TailoringCost',@amount,@description,SYSUTCDATETIME());";
            await using (var repairFinancialCommand = new SqlCommand(repairFinancialSql2, connection, transaction))
            {
                repairFinancialCommand.Parameters.AddWithValue("@reference", repairReference2);
                AddDecimal(repairFinancialCommand, "@amount", postingAmount, 2);
                repairFinancialCommand.Parameters.AddWithValue("@description", $"Tailoring cost of sales for order {orderId}");
                await repairFinancialCommand.ExecuteNonQueryAsync(ct);
            }

            var repairPosting2 = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection,
                transaction,
                repairReference2,
                "TailoringCost",
                postingAmount,
                $"Tailoring cost of sales for order {orderId}",
                ct,
                "TailoringCostPosting",
                candidatePostingId,
                sourceOperationId);
            repairPosting2.ThrowIfFailure();

            const string repairFinallyLinkSql = @"
                UPDATE p SET AccountingEventId=j.AccountingEventId
                FROM dbo.TailoringCostPostings p
                INNER JOIN dbo.JournalEntries j ON j.JournalEntryId=@journalEntryId
                WHERE p.TailoringCostPostingId=@postingId;";
            await using var repairFinallyLinkCommand = new SqlCommand(repairFinallyLinkSql, connection, transaction);
            repairFinallyLinkCommand.Parameters.AddWithValue("@journalEntryId", repairPosting2.JournalEntryId!.Value);
            repairFinallyLinkCommand.Parameters.AddWithValue("@postingId", candidatePostingId);
            if (await repairFinallyLinkCommand.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("تعذر ربط قيد تكلفة التفصيل بمصدره بعد الإصلاح.");
            return;
        }

        const string insertSql = @"
            INSERT dbo.TailoringCostPostings
                (OrderId,FabricOperationalAmount,PostingAmount,SourceOperationId)
            OUTPUT INSERTED.TailoringCostPostingId
            VALUES (@orderId,@operationalAmount,@postingAmount,@sourceOperationId);";
        await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("@orderId", orderId);
        AddDecimal(insertCommand, "@operationalAmount", postingAmount);
        AddDecimal(insertCommand, "@postingAmount", postingAmount, 2);
        insertCommand.Parameters.AddWithValue("@sourceOperationId", sourceOperationId);
        var postingId = Convert.ToInt64(await insertCommand.ExecuteScalarAsync(ct));
        var orderReference = $"Order:{orderId}:TailoringCost";
        const string financialSql = @"
            INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt)
            VALUES(@reference,N'TailoringCost',@amount,@description,SYSUTCDATETIME());";
        await using (var financialCommand = new SqlCommand(financialSql, connection, transaction))
        {
            financialCommand.Parameters.AddWithValue("@reference", orderReference);
            AddDecimal(financialCommand, "@amount", postingAmount, 2);
            financialCommand.Parameters.AddWithValue("@description", $"Tailoring cost of sales for order {orderId}");
            await financialCommand.ExecuteNonQueryAsync(ct);
        }

        var posting = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
            connection,
            transaction,
            orderReference,
            "TailoringCost",
            postingAmount,
            $"Tailoring cost of sales for order {orderId}",
            ct,
            "TailoringCostPosting",
            postingId,
            sourceOperationId);
        posting.ThrowIfFailure();

        const string linkSql = @"
            UPDATE p SET AccountingEventId=j.AccountingEventId
            FROM dbo.TailoringCostPostings p
            INNER JOIN dbo.JournalEntries j ON j.JournalEntryId=@journalEntryId
            WHERE p.TailoringCostPostingId=@postingId;";
        await using var linkCommand = new SqlCommand(linkSql, connection, transaction);
        linkCommand.Parameters.AddWithValue("@journalEntryId", posting.JournalEntryId!.Value);
        linkCommand.Parameters.AddWithValue("@postingId", postingId);
        if (await linkCommand.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("تعذر ربط قيد تكلفة التفصيل بمصدره.");
    }

    private static Guid CreateTailoringCostOperationId(int orderId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"tailoring-cost:{orderId}"));
        return new Guid(bytes[..16]);
    }

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte scale = 6)
    {
        var parameter = command.Parameters.Add(name, System.Data.SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    internal static (bool IsAllowed, string Mode, string? EmployeeCode, bool AllowsExecutionWithoutEmployee) EvaluateGeneralScannerDecision(
        bool isScannerActive,
        bool hasPermission,
        bool routeExists,
        bool routeContainsRequestedStage,
        bool isEmployeeLinked,
        bool requiresPieceWageEmployee,
        bool isAdministrativeSupervisor)
    {
        var allowsWithoutEmployee = isScannerActive && hasPermission && routeExists && routeContainsRequestedStage && !isEmployeeLinked && !requiresPieceWageEmployee && !isAdministrativeSupervisor;
        var isAllowed = isScannerActive && hasPermission && routeExists && routeContainsRequestedStage && (!requiresPieceWageEmployee || isEmployeeLinked);
        return (isAllowed, isAllowed ? "GeneralScanner" : "Rejected", isEmployeeLinked ? "LinkedEmployee" : null, allowsWithoutEmployee);
    }

    internal static (bool IsAllowed, string Mode, bool AllowsExecutionWithoutEmployee) EvaluateSupervisorReexecutionDecision(
        string? currentStage,
        string? requestedStage,
        bool isSupervisorReexecution,
        bool isAdministrativeUser,
        bool routeExists,
        bool routeContainsRequestedStage)
    {
        if (!isSupervisorReexecution || !isAdministrativeUser)
        {
            return (false, "DuplicateGuard", false);
        }

        if (!routeExists || !routeContainsRequestedStage)
        {
            return (false, "Rejected", false);
        }

        var sameStage = string.Equals(currentStage?.Trim(), requestedStage?.Trim(), StringComparison.OrdinalIgnoreCase);
        return sameStage
            ? (true, "SupervisorReexecution", true)
            : (true, "SupervisorReexecution", false);
    }

    internal static (bool IsAllowed, string Mode, bool RequiresWageCorrection, bool AllowsExecutionWithoutEmployee) EvaluateStageAttributionCorrectionDecision(
        string? currentEmployeeCode,
        string? targetEmployeeCode,
        bool isAdministrativeUser,
        bool currentEmployeeEligible,
        bool targetEmployeeEligible,
        bool sameEmployee,
        bool hasPendingPayrollWage,
        bool hasPaidPayrollWage,
        bool isIdempotentDuplicate,
        bool isDamageLostReprint)
    {
        if (isDamageLostReprint)
        {
            return (false, "SeparateReprintFlow", false, false);
        }

        if (!isAdministrativeUser)
        {
            return (false, "Forbidden", false, false);
        }

        if (sameEmployee)
        {
            return (false, "NoChange", false, false);
        }

        if (isIdempotentDuplicate)
        {
            return (false, "IdempotentDuplicate", false, false);
        }

        if (!currentEmployeeEligible || !targetEmployeeEligible)
        {
            return (false, "IneligibleEmployee", false, false);
        }

        if (hasPaidPayrollWage)
        {
            return (false, "PayrollLocked", false, false);
        }

        if (string.IsNullOrWhiteSpace(currentEmployeeCode) || string.IsNullOrWhiteSpace(targetEmployeeCode))
        {
            return (false, "MissingEmployeeIdentity", false, false);
        }

        return (true, "SupervisorStageAttributionCorrection", hasPendingPayrollWage, false);
    }

    private static bool NeedsScanner(string? requestedStage)
    {
        if (string.IsNullOrWhiteSpace(requestedStage)) return false;
        return !string.Equals(requestedStage, "Printing", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExecutionSource(ProductionTrackingAdvanceRequestDto request)
    {
        if (request is null) return "ManualTest";
        if (!string.IsNullOrWhiteSpace(request.OperationReference) &&
            (request.OperationReference.Contains("ManualTest", StringComparison.OrdinalIgnoreCase)
             || request.OperationReference.Contains("ManualProductionAdvance", StringComparison.OrdinalIgnoreCase)))
        {
            return "ManualTest";
        }

        if (string.IsNullOrWhiteSpace(request.ScannerCode) && string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            return "ManualTest";
        }

        return "Scanner";
    }

    private static bool RequiresPieceStageAssignment(string stage) =>
        string.Equals(stage, "Cutting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, "Sewing", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresPieceWage(string stage) =>
        string.Equals(stage, "Cutting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, "Sewing", StringComparison.OrdinalIgnoreCase);

    public static int? ResolveEmployeeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value.Trim(), out var employeeId) && employeeId > 0 ? employeeId : null;
    }

    private static int? TryParseEmployeeId(string? value) => ResolveEmployeeIdentifier(value);

    private static string NormalizeStageForStatus(string stage) => stage.Trim() switch
    {
        "Printing" => "Printing",
        "FabricPrep" => "FabricPrep",
        "Cutting" => "Cutting",
        "Sewing" => "Sewing",
        "Buttons" => "Buttons",
        "Ironing" => "Ironing",
        "Quality" => "Quality",
        "Assembly" => "Assembly",
        _ => stage.Trim(),
    };

    private sealed record PieceContext(int PieceId, int OrderItemId, string TrackingCode, string PieceStatus, string PieceType, int OrderId, string OrderStatus, string OrderNumber, bool IsReadyMade = false, int? ProductTypeId = null);
    private sealed record ReadyMadeInventoryTransferOutcome(int? ProductId, bool Created, bool IsAvailableForSale)
    {
        public static ReadyMadeInventoryTransferOutcome NotRequired { get; } = new(null, false, true);
        public static ReadyMadeInventoryTransferOutcome Failed { get; } = new(null, false, false);
    }
    private sealed record CancelledPieceDispositionRow(int PieceId, string Decision, string? TransferStatus);
    private sealed record PieceWageInsertOutcome(bool IsCreated, string Message);
    private sealed record ScannerValidationResult(bool IsValid, string? Message)
    {
        public static ScannerValidationResult Valid(string? employeeCode) => new(true, employeeCode);
        public static ScannerValidationResult Invalid(string message) => new(false, message);
    }
    public async Task<WorkCardDto?> GetReadyMadeWorkCardAsync(int id, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.ReadyMadeProductionOrderPieceInstanceId, p.ReadyMadeProductionOrderItemId,
                   o.ReadyMadeProductionOrderId, o.ProductionOrderNumber, p.PieceNumber, p.TrackingCode,
                   i.PieceType, i.Quantity, i.FabricType, i.FabricColor, i.FabricCode, i.CatalogNumber,
                   i.MeasurementSnapshot, p.PieceStatus
            FROM dbo.ReadyMadeProductionOrderPieceInstances p
            INNER JOIN dbo.ReadyMadeProductionOrderItems i ON i.ReadyMadeProductionOrderItemId = p.ReadyMadeProductionOrderItemId
            INNER JOIN dbo.ReadyMadeProductionOrders o ON o.ReadyMadeProductionOrderId = i.ReadyMadeProductionOrderId
            WHERE p.ReadyMadeProductionOrderPieceInstanceId = @id;";
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var itemId = reader.GetInt32(1);
        var orderId = reader.GetInt32(2);
        var orderNumber = reader.GetString(3);
        var pieceNumber = reader.GetInt32(4);
        var trackingCode = reader.GetString(5);
        var pieceType = reader.GetString(6);
        var quantity = reader.GetInt32(7);
        var fabricType = reader.NullableString("FabricType");
        var fabricColor = reader.NullableString("FabricColor");
        var fabricCode = reader.NullableString("FabricCode");
        var catalogNumber = reader.NullableString("CatalogNumber");
        var measurementSnapshot = reader.NullableString("MeasurementSnapshot");
        var pieceStatus = reader.GetString(13);
        await reader.CloseAsync();

        var history = await GetReadyMadePieceTrackingAsync(id, ct);
        return new WorkCardDto(
            id,
            itemId,
            orderId,
            orderNumber,
            pieceNumber,
            trackingCode,
            null,
            null,
            null,
            pieceType,
            quantity,
            fabricType,
            fabricColor,
            fabricCode,
            catalogNumber,
            null,
            null,
            null,
            null,
            null,
            measurementSnapshot,
            null,
            pieceStatus,
            history);
    }

    public async Task<StageAttributionCorrectionPreviewDto?> PreviewStageAttributionCorrectionAsync(StageAttributionCorrectionPreviewRequestDto request, CancellationToken ct)
    {
        if (request is null) return null;
        if (request.PieceId is null && string.IsNullOrWhiteSpace(request.TrackingCode)) return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        var piece = await ResolvePieceForStageAttributionCorrectionAsync(connection, null, request.PieceId, request.TrackingCode, ct);
        if (piece is null) return null;

        var eventId = request.OriginalTrackingEventId ?? await ResolveLatestTrackingEventIdAsync(connection, null, piece.PieceId, ct);
        if (eventId <= 0) return null;

        var trackingEvent = await LoadTrackingEventForStageAttributionCorrectionAsync(connection, null, piece.PieceId, eventId, ct);
        if (trackingEvent is null) return null;

        var currentEmployeeId = await ResolveEmployeeIdAsync(connection, null, trackingEvent.EmployeeCode, ct);
        var currentEmployeeEligible = !string.IsNullOrWhiteSpace(trackingEvent.EmployeeCode) && await IsEmployeeEligibleForStageAsync(connection, null, trackingEvent.EmployeeCode, piece.PieceType, trackingEvent.Stage, ct);
        var eligibleEmployees = await LoadEligibleEmployeesForStageAsync(connection, null, piece.PieceType, trackingEvent.Stage, ct);
        var wageRecord = await LoadLatestPieceWageRecordForTrackingAsync(connection, null, piece.PieceId, eventId, ct);
        var hasPendingPayrollWage = wageRecord is not null && (wageRecord.Status == "PendingPayroll" || wageRecord.Status == "Generated" || wageRecord.Status == "Approved");
        var hasPaidPayrollWage = wageRecord is not null && (wageRecord.Status == "Paid" || wageRecord.PayrollRecordId is not null);
        var isDamageLostReprint = await IsDamageLostReprintForPieceAsync(connection, null, piece.PieceId, ct);
        var targetCandidate = eligibleEmployees.FirstOrDefault();
        var decision = EvaluateStageAttributionCorrectionDecision(
            currentEmployeeCode: trackingEvent.EmployeeCode,
            targetEmployeeCode: targetCandidate?.EmployeeCode,
            isAdministrativeUser: true,
            currentEmployeeEligible: currentEmployeeEligible,
            targetEmployeeEligible: targetCandidate is not null,
            sameEmployee: string.Equals(trackingEvent.EmployeeCode, targetCandidate?.EmployeeCode, StringComparison.OrdinalIgnoreCase),
            hasPendingPayrollWage: hasPendingPayrollWage,
            hasPaidPayrollWage: hasPaidPayrollWage,
            isIdempotentDuplicate: false,
            isDamageLostReprint: isDamageLostReprint);

        return new StageAttributionCorrectionPreviewDto(
            piece.PieceId,
            piece.TrackingCode,
            piece.ProductTypeId,
            piece.PieceType,
            piece.PieceStatus,
            eventId,
            trackingEvent.Stage,
            currentEmployeeId,
            trackingEvent.EmployeeCode,
            trackingEvent.EventTime,
            wageRecord?.PieceWageRecordId,
            wageRecord?.Status,
            wageRecord?.PayrollPeriodId,
            wageRecord?.PayrollRecordId,
            decision.IsAllowed,
            decision.IsAllowed ? null : decision.Mode,
            eligibleEmployees);
    }

    public async Task<StageAttributionCorrectionResultDto> ExecuteStageAttributionCorrectionAsync(StageAttributionCorrectionExecutionCommand request, CancellationToken ct)
    {
        if (request is null)
        {
            return new StageAttributionCorrectionResultDto(0, string.Empty, 0, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty, null, null, "Rejected", DateTime.UtcNow, null, false, "Request body is required.");
        }

        if (request.PieceId is null && string.IsNullOrWhiteSpace(request.TrackingCode))
        {
            return new StageAttributionCorrectionResultDto(0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Either PieceId or TrackingCode is required.");
        }

        if (request.OriginalTrackingEventId <= 0)
        {
            return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "OriginalTrackingEventId is required.");
        }

        if (request.CorrectEmployeeId <= 0)
        {
            return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "CorrectEmployeeId is required.");
        }

        if (request.AuthorizedByUserId <= 0)
        {
            return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Server-side AuthorizedByUserId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, string.Empty, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, string.Empty, null, null, "Rejected", DateTime.UtcNow, null, false, "IdempotencyKey is required.");
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        try
        {
            var piece = await ResolvePieceForStageAttributionCorrectionAsync(connection, transaction, request.PieceId, request.TrackingCode, ct);
            if (piece is null)
            {
                await transaction.RollbackAsync(ct);
                return new StageAttributionCorrectionResultDto(request.PieceId ?? 0, request.TrackingCode ?? string.Empty, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Piece not found.");
            }

            var trackingEvent = await LoadTrackingEventForStageAttributionCorrectionAsync(connection, transaction, piece.PieceId, request.OriginalTrackingEventId, ct);
            if (trackingEvent is null)
            {
                await transaction.RollbackAsync(ct);
                return new StageAttributionCorrectionResultDto(piece.PieceId, piece.TrackingCode, request.OriginalTrackingEventId, string.Empty, string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Original tracking event not found.");
            }

            var targetEmployee = await LoadEmployeeByIdAsync(connection, transaction, request.CorrectEmployeeId, ct);
            if (targetEmployee is null)
            {
                await transaction.RollbackAsync(ct);
                return new StageAttributionCorrectionResultDto(piece.PieceId, piece.TrackingCode, request.OriginalTrackingEventId, trackingEvent.Stage, trackingEvent.EmployeeCode ?? string.Empty, string.Empty, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "Rejected", DateTime.UtcNow, null, false, "Target employee not found.");
            }

            var currentEmployeeEligible = !string.IsNullOrWhiteSpace(trackingEvent.EmployeeCode) && await IsEmployeeEligibleForStageAsync(connection, transaction, trackingEvent.EmployeeCode, piece.PieceType, trackingEvent.Stage, ct);
            var targetEmployeeEligible = await IsEmployeeEligibleForStageAsync(connection, transaction, targetEmployee.EmployeeCode, piece.PieceType, trackingEvent.Stage, ct);
            var hasPendingPayrollWage = await HasPendingPayrollWageForPieceAsync(connection, transaction, piece.PieceId, request.OriginalTrackingEventId, ct);
            var hasPaidPayrollWage = await HasPaidPayrollWageForPieceAsync(connection, transaction, piece.PieceId, request.OriginalTrackingEventId, ct);
            var isIdempotentDuplicate = await IsStageAttributionCorrectionDuplicateAsync(connection, transaction, request.IdempotencyKey, ct);
            var isDamageLostReprint = await IsDamageLostReprintForPieceAsync(connection, transaction, piece.PieceId, ct);
            var decision = EvaluateStageAttributionCorrectionDecision(
                currentEmployeeCode: trackingEvent.EmployeeCode,
                targetEmployeeCode: targetEmployee.EmployeeCode,
                isAdministrativeUser: true,
                currentEmployeeEligible: currentEmployeeEligible,
                targetEmployeeEligible: targetEmployeeEligible,
                sameEmployee: string.Equals(trackingEvent.EmployeeCode, targetEmployee.EmployeeCode, StringComparison.OrdinalIgnoreCase),
                hasPendingPayrollWage: hasPendingPayrollWage,
                hasPaidPayrollWage: hasPaidPayrollWage,
                isIdempotentDuplicate: isIdempotentDuplicate,
                isDamageLostReprint: isDamageLostReprint);

            if (!decision.IsAllowed)
            {
                await transaction.RollbackAsync(ct);
                return new StageAttributionCorrectionResultDto(piece.PieceId, piece.TrackingCode, request.OriginalTrackingEventId, trackingEvent.Stage, trackingEvent.EmployeeCode ?? string.Empty, targetEmployee.EmployeeCode, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, decision.Mode, DateTime.UtcNow, null, false, decision.Mode == "PayrollLocked" ? "تم رفض التصحيح لأن الأجر معلّق أو تم تسوية الرواتب." : "تم رفض التصحيح لأن القاعدة التشغيلية لا تسمح به.");
            }

            var existingIdempotent = await IsStageAttributionCorrectionDuplicateAsync(connection, transaction, request.IdempotencyKey, ct);
            if (existingIdempotent)
            {
                await transaction.RollbackAsync(ct);
                return new StageAttributionCorrectionResultDto(piece.PieceId, piece.TrackingCode, request.OriginalTrackingEventId, trackingEvent.Stage, trackingEvent.EmployeeCode ?? string.Empty, targetEmployee.EmployeeCode, request.AuthorizedByUserId, request.Reason, request.IdempotencyKey, null, null, "IdempotentDuplicate", DateTime.UtcNow, null, false, "تم تنفيذ هذا التصحيح مسبقًا لنفس IdempotencyKey.");
            }

            var noteText = $"[StageAttributionCorrection:{request.IdempotencyKey}] reason={request.Reason}; correctedByUserId={request.AuthorizedByUserId}; fromEmployee={trackingEvent.EmployeeCode}; toEmployee={targetEmployee.EmployeeCode};";
            const string updateTrackingSql = @"
                UPDATE dbo.TrackingEvents
                SET EmployeeCode = @correctEmployeeCode,
                    Notes = CASE WHEN Notes IS NULL OR Notes = N'' THEN @note ELSE Notes + N' | ' + @note END
                WHERE TrackingEventID = @trackingEventId AND PieceID = @pieceId;";
            await using (var updateTracking = new SqlCommand(updateTrackingSql, connection, transaction))
            {
                updateTracking.Parameters.AddWithValue("@correctEmployeeCode", targetEmployee.EmployeeCode);
                updateTracking.Parameters.AddWithValue("@note", noteText);
                updateTracking.Parameters.AddWithValue("@trackingEventId", request.OriginalTrackingEventId);
                updateTracking.Parameters.AddWithValue("@pieceId", piece.PieceId);
                if (await updateTracking.ExecuteNonQueryAsync(ct) != 1)
                {
                    throw new InvalidOperationException("تعذر تحديث سجل المرحلة الحالي مع الموظف الصحيح.");
                }
            }

            const string updateWageSql = @"
                UPDATE dbo.PieceWageRecords
                SET EmployeeId = @correctEmployeeId,
                    EmployeeCode = @correctEmployeeCode,
                    Notes = CASE WHEN Notes IS NULL OR Notes = N'' THEN @note ELSE Notes + N' | ' + @note END
                WHERE PieceID = @pieceId AND TrackingEventID = @trackingEventId AND (PayrollRecordId IS NULL OR PayrollRecordId = 0);";
            await using (var updateWage = new SqlCommand(updateWageSql, connection, transaction))
            {
                updateWage.Parameters.AddWithValue("@correctEmployeeId", request.CorrectEmployeeId);
                updateWage.Parameters.AddWithValue("@correctEmployeeCode", targetEmployee.EmployeeCode);
                updateWage.Parameters.AddWithValue("@note", noteText);
                updateWage.Parameters.AddWithValue("@pieceId", piece.PieceId);
                updateWage.Parameters.AddWithValue("@trackingEventId", request.OriginalTrackingEventId);
                await updateWage.ExecuteNonQueryAsync(ct);
            }

            const string auditSql = @"
                INSERT INTO dbo.StageAttributionCorrections
                    (PieceID, TrackingEventID, PreviousEmployeeId, PreviousEmployeeCode, CorrectEmployeeId, CorrectEmployeeCode, Stage, Reason, IdempotencyKey, AuthorizedByUserId, Status, CreatedAt, CompletedAt)
                VALUES (@pieceId, @trackingEventId, @previousEmployeeId, @previousEmployeeCode, @correctEmployeeId, @correctEmployeeCode, @stage, @reason, @idempotencyKey, @authorizedByUserId, @status, @createdAt, @completedAt);";
            await using (var auditCommand = new SqlCommand(auditSql, connection, transaction))
            {
                auditCommand.Parameters.AddWithValue("@pieceId", piece.PieceId);
                auditCommand.Parameters.AddWithValue("@trackingEventId", request.OriginalTrackingEventId);
                auditCommand.Parameters.AddWithValue("@previousEmployeeId", await ResolveEmployeeIdAsync(connection, transaction, trackingEvent.EmployeeCode, ct));
                auditCommand.Parameters.AddWithValue("@previousEmployeeCode", trackingEvent.EmployeeCode ?? (object)DBNull.Value);
                auditCommand.Parameters.AddWithValue("@correctEmployeeId", request.CorrectEmployeeId);
                auditCommand.Parameters.AddWithValue("@correctEmployeeCode", targetEmployee.EmployeeCode);
                auditCommand.Parameters.AddWithValue("@stage", trackingEvent.Stage);
                auditCommand.Parameters.AddWithValue("@reason", request.Reason);
                auditCommand.Parameters.AddWithValue("@idempotencyKey", request.IdempotencyKey);
                auditCommand.Parameters.AddWithValue("@authorizedByUserId", request.AuthorizedByUserId);
                auditCommand.Parameters.AddWithValue("@status", "Applied");
                auditCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                auditCommand.Parameters.AddWithValue("@completedAt", DateTime.UtcNow);
                await auditCommand.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new StageAttributionCorrectionResultDto(
                piece.PieceId,
                piece.TrackingCode,
                request.OriginalTrackingEventId,
                trackingEvent.Stage,
                trackingEvent.EmployeeCode ?? string.Empty,
                targetEmployee.EmployeeCode,
                request.AuthorizedByUserId,
                request.Reason,
                request.IdempotencyKey,
                await LoadLatestPieceWageRecordForTrackingAsync(connection, null, piece.PieceId, request.OriginalTrackingEventId, ct) is { } latestWage ? latestWage.PieceWageRecordId : null,
                null,
                "Applied",
                DateTime.UtcNow,
                DateTime.UtcNow,
                true,
                "تمت تسوية نسبة المرحلة بنجاح وقد تم ربطها بمعرّف التكرار.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkCardDto?> GetWorkCardAsync(int id, CancellationToken ct)
    {
        const string sql = """
            SELECT p.PieceID, oi.OrderItemID, o.OrderID, o.OrderNumber, p.PieceNumber, p.TrackingCode,
                   c.CustomerCode, c.CustomerName, c.PhoneNumber, oi.PieceType, oi.Quantity, oi.FabricCode, oi.FabricType, oi.FabricColor,
                   oi.Request1, oi.Request2, oi.Notes1, oi.Notes2, oi.MeasurementSnapshot, o.DeliveryDate, p.PieceStatus
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
        var measurementSnapshot = reader.NullableString("MeasurementSnapshot");
        var catalogNumber = TryExtractSnapshotValue(measurementSnapshot, "_catalogNumber", "catalogNumber", "CatalogNumber", "barcode", "Barcode");
        var request1 = reader.NullableString("Request1") ?? TryExtractSnapshotValue(measurementSnapshot, "request1", "Request1");
        var request2 = reader.NullableString("Request2") ?? TryExtractSnapshotValue(measurementSnapshot, "request2", "Request2");
        var specialRequest = TryExtractSnapshotValue(measurementSnapshot, "specialRequest", "SpecialRequest");
        var card = new WorkCardDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.NullableString("CustomerCode"), reader.NullableString("CustomerName"), reader.NullableString("PhoneNumber"), reader.GetString(9), reader.GetInt32(10), reader.NullableString("FabricType"), reader.NullableString("FabricColor"), reader.NullableString("FabricCode"), catalogNumber, request1, request2, reader.NullableString("Notes1"), reader.NullableString("Notes2"), specialRequest, measurementSnapshot, reader.NullableDateTime("DeliveryDate"), reader.GetString(20), []);
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

    private static async Task<PieceCorrectionContext?> ResolvePieceForStageAttributionCorrectionAsync(SqlConnection connection, SqlTransaction? transaction, int? pieceId, string? trackingCode, CancellationToken ct)
    {
        var sql = @"
            SELECT p.PieceID, p.TrackingCode, p.PieceStatus, oi.PieceType,
                   COALESCE((
                       SELECT TOP 1 pt.ProductTypeId
                       FROM dbo.PricingProductTypes pt WITH (NOLOCK)
                       WHERE pt.IsActive = 1 AND (pt.Code = oi.PieceType OR pt.NameAr = oi.PieceType)
                       ORDER BY CASE WHEN pt.Code = oi.PieceType THEN 0 ELSE 1 END, pt.ProductTypeId
                   ), 0) AS ProductTypeId
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi ON oi.OrderItemID = p.OrderItemID
            WHERE (@pieceId IS NOT NULL AND p.PieceID = @pieceId)
               OR (@trackingCode IS NOT NULL AND p.TrackingCode = @trackingCode);";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@trackingCode", string.IsNullOrWhiteSpace(trackingCode) ? (object)DBNull.Value : trackingCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new PieceCorrectionContext(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(3),
            reader.GetString(2),
            reader.IsDBNull(4) ? 0 : reader.GetInt32(4));
    }

    private static async Task<int> ResolveLatestTrackingEventIdAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, CancellationToken ct)
    {
        const string sql = "SELECT TOP 1 TrackingEventID FROM dbo.TrackingEvents WITH (NOLOCK) WHERE PieceID = @pieceId ORDER BY EventTime DESC, TrackingEventID DESC;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        var result = await command.ExecuteScalarAsync(ct);
        return result is int value ? value : 0;
    }

    private static async Task<TrackingEventCorrectionContext?> LoadTrackingEventForStageAttributionCorrectionAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, int trackingEventId, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 TrackingEventID, Stage, EmployeeCode, EventTime, Notes
            FROM dbo.TrackingEvents WITH (NOLOCK)
            WHERE PieceID = @pieceId AND TrackingEventID = @trackingEventId
            ORDER BY EventTime DESC, TrackingEventID DESC;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new TrackingEventCorrectionContext(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.NullableString("EmployeeCode"),
            reader.GetDateTime(3),
            reader.NullableString("Notes"));
    }

    private static async Task<PieceWageCorrectionContext?> LoadLatestPieceWageRecordForTrackingAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, int trackingEventId, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 PieceWageRecordID, Status, PayrollPeriodId, PayrollRecordId
            FROM dbo.PieceWageRecords WITH (NOLOCK)
            WHERE PieceID = @pieceId AND TrackingEventID = @trackingEventId
            ORDER BY PieceWageRecordID DESC;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new PieceWageCorrectionContext(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    private static async Task<IReadOnlyList<EligibleStageEmployeeDto>> LoadEligibleEmployeesForStageAsync(SqlConnection connection, SqlTransaction? transaction, string pieceType, string stage, CancellationToken ct)
    {
        const string sql = @"
            SELECT e.EmployeeID, e.EmployeeCode, e.FullName, e.JobTitle, a.Rate
            FROM dbo.EmployeePieceRateAssignments a WITH (NOLOCK)
            INNER JOIN dbo.Employees e WITH (NOLOCK) ON e.EmployeeID = a.EmployeeId
            WHERE a.PieceType = @pieceType
              AND a.Stage = @stage
              AND a.IsActive = 1
              AND e.IsActive = 1
              AND (a.EffectiveFrom IS NULL OR a.EffectiveFrom <= @now)
              AND (a.EffectiveTo IS NULL OR a.EffectiveTo >= @now)
            ORDER BY e.FullName, e.EmployeeCode;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceType", pieceType.Trim());
        command.Parameters.AddWithValue("@stage", stage.Trim());
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var employees = new List<EligibleStageEmployeeDto>();
        while (await reader.ReadAsync(ct))
        {
            employees.Add(new EligibleStageEmployeeDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.NullableString("FullName"),
                reader.NullableString("JobTitle"),
                reader.GetDecimal(4),
                true));
        }
        return employees;
    }

    private static async Task<EmployeeCorrectionContext?> LoadEmployeeByIdAsync(SqlConnection connection, SqlTransaction? transaction, int employeeId, CancellationToken ct)
    {
        const string sql = @"SELECT EmployeeID, EmployeeCode, FullName, JobTitle, IsActive FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeID = @employeeId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new EmployeeCorrectionContext(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.NullableString("FullName"),
            reader.NullableString("JobTitle"),
            reader.GetBoolean(4));
    }

    private static async Task<bool> IsEmployeeEligibleForStageAsync(SqlConnection connection, SqlTransaction? transaction, string? employeeCode, string pieceType, string stage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return false;
        const string sql = @"
            SELECT TOP 1 1
            FROM dbo.EmployeePieceRateAssignments a WITH (NOLOCK)
            INNER JOIN dbo.Employees e WITH (NOLOCK) ON e.EmployeeID = a.EmployeeId
            WHERE e.EmployeeCode = @employeeCode
              AND a.PieceType = @pieceType
              AND a.Stage = @stage
              AND a.IsActive = 1
              AND e.IsActive = 1
              AND (a.EffectiveFrom IS NULL OR a.EffectiveFrom <= @now)
              AND (a.EffectiveTo IS NULL OR a.EffectiveTo >= @now);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeCode", employeeCode.Trim());
        command.Parameters.AddWithValue("@pieceType", pieceType.Trim());
        command.Parameters.AddWithValue("@stage", stage.Trim());
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    private static async Task<bool> HasPendingPayrollWageForPieceAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, int trackingEventId, CancellationToken ct)
    {
        const string sql = @"SELECT TOP 1 1 FROM dbo.PieceWageRecords WITH (NOLOCK) WHERE PieceID = @pieceId AND TrackingEventID = @trackingEventId AND Status IN ('PendingPayroll','Generated','Approved');";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    private static async Task<bool> HasPaidPayrollWageForPieceAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, int trackingEventId, CancellationToken ct)
    {
        const string sql = @"SELECT TOP 1 1 FROM dbo.PieceWageRecords WITH (NOLOCK) WHERE PieceID = @pieceId AND TrackingEventID = @trackingEventId AND (Status = 'Paid' OR PayrollRecordId IS NOT NULL);";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        command.Parameters.AddWithValue("@trackingEventId", trackingEventId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    private static async Task<bool> IsStageAttributionCorrectionDuplicateAsync(SqlConnection connection, SqlTransaction? transaction, string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        const string sql = @"SELECT TOP 1 1 FROM dbo.StageAttributionCorrections WITH (NOLOCK) WHERE IdempotencyKey = @idempotencyKey;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@idempotencyKey", idempotencyKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
    }

    private static async Task<bool> IsDamageLostReprintForPieceAsync(SqlConnection connection, SqlTransaction? transaction, int pieceId, CancellationToken ct)
    {
        const string sql = @"SELECT TOP 1 1 FROM dbo.TrackingEvents WITH (NOLOCK) WHERE PieceID = @pieceId AND (LOWER(Notes) LIKE '%damage%' OR LOWER(Notes) LIKE '%lost%' OR LOWER(Notes) LIKE '%reprint%' OR LOWER(Notes) LIKE '%damaged%');";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct);
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
            // Ignore malformed measurement snapshots; source values are already stored in dedicated columns.
        }

        return null;
    }

    private static decimal StageProgressPercent(string currentStage)
    {
        if (string.IsNullOrWhiteSpace(currentStage)) return 0m;
        var normalized = currentStage.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "new" => 0m,
            "printing" => 15m,
            "fabricprep" => 30m,
            "cutting" => 45m,
            "sewing" => 65m,
            "buttons" => 80m,
            "ironing" => 88m,
            "quality" => 95m,
            "assembly" => 97m,
            "ready" => 100m,
            "readyforsale" => 100m,
            "delivered" => 100m,
            _ => 0m,
        };
    }

    private static bool IsCompletedStatus(string currentStage)
    {
        if (string.IsNullOrWhiteSpace(currentStage)) return false;
        var normalized = currentStage.Trim();
        return normalized.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ReadyForSale", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Assembly", StringComparison.OrdinalIgnoreCase) && !normalized.Equals("New", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN p.PieceStatus IN (N'Printing',N'Cutting',N'InProduction') THEN 1 ELSE 0 END),0), COALESCE(SUM(CASE WHEN p.PieceStatus=N'Ready' THEN 1 ELSE 0 END),0), COALESCE(SUM(CASE WHEN p.PieceStatus=N'Delivered' THEN 1 ELSE 0 END),0), (SELECT COUNT(DISTINCT Stage) FROM dbo.TrackingEvents), (SELECT COUNT(*) FROM dbo.ReadyMadeInventoryProducts WHERE Status=N'AvailableForSale' AND IsActive=1), COALESCE(SUM(CASE WHEN o.DeliveryDate < SYSDATETIME() AND o.OrderStatus NOT IN (N'Delivered',N'Cancelled') THEN 1 ELSE 0 END),0) FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID INNER JOIN dbo.Orders o ON o.OrderID=oi.OrderID";
        await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
        return new ProductionDashboardDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    public async Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken ct)
    {
        const string orderSql = @"
            WITH piece_stats AS (
                SELECT oi.OrderID,
                       COUNT(p.PieceID) AS TotalPieces,
                       SUM(CASE WHEN p.PieceStatus IN (N'Ready', N'ReadyForSale', N'Delivered') THEN 1 ELSE 0 END) AS CompletedPieces,
                       SUM(CASE WHEN p.PieceStatus NOT IN (N'Ready', N'ReadyForSale', N'Delivered') AND p.PieceStatus IS NOT NULL THEN 1 ELSE 0 END) AS IncompletePieces,
                       SUM(CASE WHEN p.PieceStatus = N'Delivered' THEN 1 ELSE 0 END) AS DeliveredPieces,
                       SUM(CASE WHEN p.PieceStatus IS NOT NULL AND p.PieceStatus NOT IN (N'Ready', N'ReadyForSale', N'Delivered') THEN 1 ELSE 0 END) AS InProgressPieces
                FROM dbo.OrderItems oi WITH (NOLOCK)
                LEFT JOIN dbo.Pieces p WITH (NOLOCK) ON p.OrderItemID = oi.OrderItemID
                GROUP BY oi.OrderID
            ), tracking_stats AS (
                SELECT te.OrderID,
                       MAX(te.EventTime) AS LastTrackingEventAt,
                       COUNT(DISTINCT te.TrackingEventID) AS TrackingEventCount
                FROM dbo.TrackingEvents te WITH (NOLOCK)
                GROUP BY te.OrderID
            )
            SELECT
                o.OrderID,
                o.OrderNumber,
                c.CustomerCode,
                c.CustomerName,
                o.OrderDate,
                o.DeliveryDate,
                o.OrderStatus,
                COALESCE(ps.TotalPieces, 0) AS TotalPieces,
                COALESCE(ps.CompletedPieces, 0) AS CompletedPieces,
                COALESCE(ps.IncompletePieces, 0) AS IncompletePieces,
                COALESCE(ps.DeliveredPieces, 0) AS DeliveredPieces,
                COALESCE(ps.InProgressPieces, 0) AS InProgressPieces,
                COALESCE(ts.LastTrackingEventAt, o.CreatedDate) AS LastTrackingEventAt,
                COALESCE(ts.TrackingEventCount, 0) AS TrackingEventCount
            FROM dbo.Orders o WITH (NOLOCK)
            INNER JOIN dbo.Customers c WITH (NOLOCK) ON c.CustomerID = o.CustomerID
            LEFT JOIN piece_stats ps ON ps.OrderID = o.OrderID
            LEFT JOIN tracking_stats ts ON ts.OrderID = o.OrderID
            WHERE o.OrderStatus <> N'Cancelled'
            ORDER BY o.DeliveryDate, o.OrderID DESC;";

        const string pieceSql = @"
            WITH latest_events AS (
                SELECT te.PieceID,
                       te.Stage,
                       te.EmployeeCode,
                       te.EventTime,
                       ROW_NUMBER() OVER (PARTITION BY te.PieceID ORDER BY te.EventTime DESC, te.TrackingEventID DESC) AS rn
                FROM dbo.TrackingEvents te WITH (NOLOCK)
                WHERE te.PieceID IS NOT NULL
            )
            SELECT
                p.PieceID,
                oi.OrderID,
                oi.PieceType,
                p.TrackingCode,
                COALESCE(le.Stage, p.PieceStatus) AS CurrentStage,
                le.EventTime AS LastTrackingEventAt,
                le.EmployeeCode AS LastEmployeeCode
            FROM dbo.Pieces p WITH (NOLOCK)
            INNER JOIN dbo.OrderItems oi WITH (NOLOCK) ON oi.OrderItemID = p.OrderItemID
            LEFT JOIN latest_events le ON le.PieceID = p.PieceID AND le.rn = 1
            ORDER BY oi.OrderID, p.PieceID;";

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var orderCommand = new SqlCommand(orderSql, connection);
        await using var orderReader = await orderCommand.ExecuteReaderAsync(ct);
        var summaries = new List<OrderMonitoringSummary>();
        while (await orderReader.ReadAsync(ct))
        {
            summaries.Add(new OrderMonitoringSummary(
                orderReader.GetInt32(0),
                orderReader.GetString(1),
                orderReader.NullableString("CustomerCode") ?? "N/A",
                orderReader.NullableString("CustomerName") ?? "غير محدد",
                orderReader.NullableDateTime("OrderDate"),
                orderReader.NullableDateTime("DeliveryDate"),
                orderReader.GetString(6),
                orderReader.GetInt32(7),
                orderReader.GetInt32(8),
                orderReader.GetInt32(9),
                orderReader.GetInt32(10),
                orderReader.GetInt32(11),
                orderReader.NullableDateTime("LastTrackingEventAt"),
                orderReader.GetInt32(13))); 
        }

        var pieceSnapshotsByOrder = new Dictionary<int, List<FactoryMonitoringPieceSnapshot>>();
        await using var pieceCommand = new SqlCommand(pieceSql, connection);
        await using var pieceReader = await pieceCommand.ExecuteReaderAsync(ct);
        while (await pieceReader.ReadAsync(ct))
        {
            var orderId = pieceReader.GetInt32(1);
            var pieceType = pieceReader.NullableString("PieceType") ?? "UNKNOWN";
            var trackingCode = pieceReader.NullableString("TrackingCode") ?? $"TRK-{pieceReader.GetInt32(0)}";
            var currentStage = pieceReader.NullableString("CurrentStage") ?? "New";
            var lastTrackingEventAt = pieceReader.NullableDateTime("LastTrackingEventAt");
            var lastEmployeeCode = pieceReader.NullableString("LastEmployeeCode");
            var snapshot = new FactoryMonitoringPieceSnapshot(
                pieceReader.GetInt32(0),
                pieceType,
                trackingCode,
                currentStage,
                FactoryMonitoringEngine.NextStageFor(currentStage),
                lastTrackingEventAt,
                lastEmployeeCode,
                StageProgressPercent(currentStage),
                IsCompletedStatus(currentStage));

            if (!pieceSnapshotsByOrder.TryGetValue(orderId, out var list))
            {
                list = [];
                pieceSnapshotsByOrder[orderId] = list;
            }

            list.Add(snapshot);
        }

        var atRiskOrders = new List<FactoryMonitoringOrderDto>();
        var stalledOrders = new List<FactoryMonitoringOrderDto>();
        var blockedOrders = new List<FactoryMonitoringOrderDto>();
        var readyForDeliveryOrders = new List<FactoryMonitoringOrderDto>();

        foreach (var summary in summaries)
        {
            var progressPercent = summary.TotalPieces == 0 ? 0 : Convert.ToInt32(Math.Round((summary.CompletedPieces / (decimal)summary.TotalPieces) * 100m, MidpointRounding.AwayFromZero));
            var pieceSnapshots = pieceSnapshotsByOrder.TryGetValue(summary.OrderId, out var orderPieces) ? orderPieces : [];
            var delayPiece = FactoryMonitoringEngine.ResolveDelayPiece(pieceSnapshots);
            var classification = summary.TotalPieces > 0 && summary.IncompletePieces == 0
                ? new FactoryMonitoringClassificationResult("ReadyForDelivery", "كل القطع مكتملة وجاهزة للتسليم.")
                : FactoryMonitoringEngine.ClassifyOrder(
                    summary.TotalPieces,
                    summary.CompletedPieces,
                    summary.IncompletePieces,
                    summary.DeliveredPieces,
                    summary.InProgressPieces,
                    summary.DeliveryDate,
                    summary.LastTrackingEventAt,
                    progressPercent,
                    summary.TrackingEventCount > 0,
                    summary.OrderStatus);

            var dto = new FactoryMonitoringOrderDto(
                summary.OrderId,
                summary.OrderNumber,
                summary.CustomerCode,
                summary.CustomerName,
                summary.OrderDate,
                summary.DeliveryDate,
                FactoryMonitoringEngine.DaysRemaining(summary.DeliveryDate),
                summary.TotalPieces,
                summary.CompletedPieces,
                summary.IncompletePieces,
                progressPercent,
                classification.Classification,
                classification.Reason,
                delayPiece?.TrackingCode,
                delayPiece?.PieceType,
                delayPiece?.CurrentStage,
                delayPiece?.NextStage,
                delayPiece?.LastEmployeeCode,
                delayPiece?.LastTrackingEventAt,
                summary.OrderStatus);

            switch (classification.Classification)
            {
                case "AtRisk":
                    atRiskOrders.Add(dto); break;
                case "Stalled":
                    stalledOrders.Add(dto); break;
                case "Blocked":
                    blockedOrders.Add(dto); break;
                default:
                    readyForDeliveryOrders.Add(dto); break;
            }
        }

        var grandTotalPieces = summaries.Sum(x => x.TotalPieces);
        var grandCompletedPieces = summaries.Sum(x => x.CompletedPieces);
        var overallProgress = grandTotalPieces == 0 ? 0m : Math.Round((grandCompletedPieces / (decimal)grandTotalPieces) * 100m, 2, MidpointRounding.AwayFromZero);
        var lastUpdatedAt = summaries.Select(x => x.LastTrackingEventAt ?? x.OrderDate ?? DateTime.UtcNow).DefaultIfEmpty(DateTime.UtcNow).Max();

        return new FactoryMonitoringDashboardDto(
            atRiskOrders.Count,
            stalledOrders.Count,
            blockedOrders.Count,
            readyForDeliveryOrders.Count,
            overallProgress,
            lastUpdatedAt,
            atRiskOrders,
            stalledOrders,
            blockedOrders,
            readyForDeliveryOrders);
    }

    public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderId,ProductionOrderNumber,ProductionName,TotalCost,ProfitPercentage,SuggestedSellingPrice,Status,Notes,CreatedAt FROM dbo.ReadyMadeProductionOrders ORDER BY CreatedAt DESC,ReadyMadeProductionOrderId DESC", reader => new ReadyMadeProductionOrderDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetString(6),reader.NullableString("Notes"),reader.GetDateTime(8)), null, ct);
    public async Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken ct) => (await QueryAsync("SELECT ReadyMadeProductionOrderId,ProductionOrderNumber,ProductionName,TotalCost,ProfitPercentage,SuggestedSellingPrice,Status,Notes,CreatedAt FROM dbo.ReadyMadeProductionOrders WHERE ReadyMadeProductionOrderId = @id", reader => new ReadyMadeProductionOrderDto(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetDecimal(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetString(6),reader.NullableString("Notes"),reader.GetDateTime(8)), orderId, ct)).SingleOrDefault();
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderItemId,ReadyMadeProductionOrderId,ProductTypeId,PieceType,Quantity,FabricCode,FabricType,FabricColor,CatalogNumber,FabricCost,PieceCost,LineTotal,MeasurementSnapshot,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderItems WHERE ReadyMadeProductionOrderId=@id ORDER BY ReadyMadeProductionOrderItemId", reader => new ReadyMadeProductionOrderItemDto(reader.GetInt32(0),reader.GetInt32(1),reader.NullableInt32("ProductTypeId"),reader.GetString(3),reader.GetInt32(4),reader.NullableString("FabricCode"),reader.NullableString("FabricType"),reader.NullableString("FabricColor"),reader.NullableString("CatalogNumber"),reader.NullableDecimal("FabricCost"),reader.NullableDecimal("PieceCost"),reader.NullableDecimal("LineTotal"),reader.NullableString("MeasurementSnapshot"),reader.GetString(13),reader.GetDateTime(14)), id, ct);
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int id, CancellationToken ct) => QueryAsync("SELECT ReadyMadeProductionOrderPieceInstanceId,ReadyMadeProductionOrderItemId,PieceNumber,TrackingCode,PieceStatus,CreatedAt FROM dbo.ReadyMadeProductionOrderPieceInstances WHERE ReadyMadeProductionOrderItemId=@id ORDER BY PieceNumber,ReadyMadeProductionOrderPieceInstanceId", reader => new ReadyMadeProductionPieceDto(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetDateTime(5)), id, ct);
    public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int id, CancellationToken ct) => QueryAsync("SELECT TrackingEventID,OrderItemID,OrderID,TrackingCode,Stage,Status,EventTime,EmployeeCode,Notes,IsReverted,RevertedAt,PieceID,ReadyMadeProductionOrderPieceInstanceId FROM dbo.TrackingEvents WHERE ReadyMadeProductionOrderPieceInstanceId=@id ORDER BY EventTime,TrackingEventID", MapTracking, id, ct);
    public async Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken ct)
    {
        if (order.Items.Count == 0) throw new InvalidOperationException("يجب إضافة بند واحد على الأقل.");
        var pricingLines = await ResolveReadyMadePricingAsync(order.Items, ct);
        var fabricRequirements = pricingLines
            .GroupBy(line => line.FabricCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReadyMadeFabricRequirement(
                group.Key,
                group.Sum(line => line.RequiredInches)))
            .ToList();
        var officialTotalCost = pricingLines.Sum(line => line.Pricing.FullCostTotal ?? 0m);
        var officialSuggestedSellingPrice = pricingLines.Sum(line => line.Pricing.FinalPriceTotal ?? 0m);
        var officialGlobalProfitPercentage = pricingLines
            .Select(line => line.Pricing.GlobalProfitPercentage ?? 0m)
            .DefaultIfEmpty()
            .First();
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var existingOrderId = await FindReadyMadeOrderByRequestReferenceAsync(connection, transaction, order.RequestReference, ct);
            if (existingOrderId is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                var existingOrder = await GetReadyMadeOrderByIdAsync(existingOrderId.Value, ct);
                return existingOrder is null
                    ? throw new InvalidOperationException("تعذر استعادة أمر الإنتاج المرتبط بطلب الإعادة.")
                    : new ReadyMadeProductionOrderCreateResultDto(
                        existingOrder.ReadyMadeProductionOrderId,
                        existingOrder.ProductionOrderNumber,
                        existingOrder.ProductionName,
                        existingOrder.TotalCost,
                        existingOrder.ProfitPercentage,
                        existingOrder.SuggestedSellingPrice,
                        existingOrder.Status,
                        existingOrder.CreatedAt);
            }

            var lockedFabricStocks = new Dictionary<string, ReadyMadeFabricStock>(StringComparer.OrdinalIgnoreCase);
            foreach (var requirement in fabricRequirements)
            {
                var stock = await LoadReadyMadeFabricStockAsync(connection, transaction, requirement.FabricCode, ct);
                if (stock.AvailableInches < requirement.RequiredInches)
                {
                    throw new InvalidOperationException(
                        $"الكمية المتوفرة للقماش {requirement.FabricCode} لا تكفي لهذا الأمر. المتوفر: {stock.AvailableInches:0.##} بوصة، المطلوب: {requirement.RequiredInches:0.##} بوصة.");
                }

                lockedFabricStocks[requirement.FabricCode] = stock;
            }

            var orderNumber = await GetNextReadyMadeOrderNumberAsync(connection, transaction, ct);
            var status = "New";
            var storedNotes = AppendRequestReference(order.Notes, order.RequestReference);
            const string orderSql = @"
                INSERT INTO dbo.ReadyMadeProductionOrders
                    (ProductionOrderNumber, ProductionName, TotalCost, ProfitPercentage, SuggestedSellingPrice, Status, Notes, CreatedAt)
                OUTPUT INSERTED.ReadyMadeProductionOrderId, INSERTED.ProductionOrderNumber, INSERTED.ProductionName, INSERTED.TotalCost, INSERTED.ProfitPercentage, INSERTED.SuggestedSellingPrice, INSERTED.Status, INSERTED.CreatedAt
                VALUES (@number, @name, @totalCost, @profit, @sell, @status, @notes, @createdAt);";
            await using var orderCommand = new SqlCommand(orderSql, connection, transaction);
            orderCommand.Parameters.AddWithValue("@number", orderNumber);
            orderCommand.Parameters.AddWithValue("@name", order.ProductionName.Trim());
            orderCommand.Parameters.AddWithValue("@totalCost", officialTotalCost);
            orderCommand.Parameters.AddWithValue("@profit", officialGlobalProfitPercentage);
            orderCommand.Parameters.AddWithValue("@sell", officialSuggestedSellingPrice);
            orderCommand.Parameters.AddWithValue("@status", status);
            AddNullable(orderCommand, "@notes", storedNotes);
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
            var createdItems = new List<(int ItemId, ReadyMadePricingLine Line)>();

            for (var itemIndex = 0; itemIndex < order.Items.Count; itemIndex++)
            {
                var item = order.Items[itemIndex];
                var pricingLine = pricingLines[itemIndex];
                if (string.IsNullOrWhiteSpace(item.PieceType)) throw new InvalidOperationException("نوع القطعة مطلوب.");
                if (item.Quantity <= 0) throw new InvalidOperationException("كمية القطع يجب أن تكون أكبر من صفر.");
                if (item.ProductTypeId <= 0) throw new InvalidOperationException("ProductTypeId الرسمي مطلوب لكل بند إنتاج جاهز.");
                if (!await ProductTypeExistsAsync(connection, transaction, item.ProductTypeId, ct)) throw new InvalidOperationException("ProductTypeId غير موجود أو غير فعال.");

                const string itemSql = @"
                    INSERT INTO dbo.ReadyMadeProductionOrderItems
                        (ReadyMadeProductionOrderId, ProductTypeId, PieceType, Quantity, FabricCode, FabricType, FabricColor, CatalogNumber, InchPrice, FabricCost, PieceCost, LineTotal, Consumption, MeasurementSnapshot, PieceStatus, CreatedAt)
                    OUTPUT INSERTED.ReadyMadeProductionOrderItemId
                    VALUES (@orderId, @productTypeId, @pieceType, @quantity, @fabricCode, @fabricType, @fabricColor, @catalogNumber, @inchPrice, @fabricCost, @pieceCost, @lineTotal, @consumption, @measurementSnapshot, N'New', @createdAt);";
                await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
                itemCommand.Parameters.AddWithValue("@orderId", orderId);
                itemCommand.Parameters.AddWithValue("@productTypeId", item.ProductTypeId);
                itemCommand.Parameters.AddWithValue("@pieceType", item.PieceType.Trim());
                itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
                AddNullable(itemCommand, "@fabricCode", item.FabricCode);
                AddNullable(itemCommand, "@fabricType", item.FabricType);
                AddNullable(itemCommand, "@fabricColor", item.FabricColor);
                AddNullable(itemCommand, "@catalogNumber", item.CatalogNumber);
                itemCommand.Parameters.AddWithValue("@inchPrice", pricingLine.Pricing.InchPrice ?? 0m);
                itemCommand.Parameters.AddWithValue("@fabricCost", (pricingLine.Pricing.FabricCostPerPiece ?? 0m) * item.Quantity);
                itemCommand.Parameters.AddWithValue("@pieceCost", (pricingLine.Pricing.OperationalCostPerPiece ?? 0m) * item.Quantity);
                itemCommand.Parameters.AddWithValue("@lineTotal", pricingLine.Pricing.FullCostTotal ?? 0m);
                itemCommand.Parameters.AddWithValue("@consumption", pricingLine.Pricing.ConsumptionPerPiece);
                AddNullable(itemCommand, "@measurementSnapshot", MergePricingSnapshot(item.MeasurementSnapshot, pricingLine.Pricing));
                itemCommand.Parameters.AddWithValue("@createdAt", now);
                var itemId = (int)(await itemCommand.ExecuteScalarAsync(ct))!;
                createdItems.Add((itemId, pricingLine));

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

            foreach (var createdItem in createdItems)
            {
                await InventoryRepository.ConsumeFabricCodeInventoryAsync(
                    connection,
                    transaction,
                    new ConsumeFabricCodeInventoryDto
                    {
                        FabricCode = createdItem.Line.FabricCode,
                        ReadyMadeProductionOrderItemId = createdItem.ItemId,
                        QuantityInches = createdItem.Line.RequiredInches,
                        SourceOperationId = InventoryRepository.CreateFabricConsumptionOperationId(createdItem.ItemId, true)
                    },
                    ct);
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

    private async Task<IReadOnlyList<ReadyMadePricingLine>> ResolveReadyMadePricingAsync(
        IReadOnlyList<ReadyMadeProductionOrderCreateItemDto> items,
        CancellationToken ct)
    {
        if (consumptionRules is null || pricingEngine is null)
        {
            throw new InvalidOperationException("خدمات الاستهلاك والتسعير الرسمية غير مهيأة.");
        }

        var lines = new List<ReadyMadePricingLine>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Quantity <= 0) throw new InvalidOperationException($"كمية البند {index + 1} يجب أن تكون أكبر من صفر.");
            if (item.ProductTypeId <= 0) throw new InvalidOperationException($"معرف نوع المنتج الرسمي مطلوب للبند {index + 1}.");

            var fabricCode = item.FabricCode?.Trim();
            if (string.IsNullOrWhiteSpace(fabricCode))
                throw new InvalidOperationException($"كود القماش مطلوب للبند {index + 1}.");

            var evaluation = await consumptionRules.EvaluateAsync(
                new EvaluateConsumptionRequestDto(
                    item.ProductTypeId,
                    ParseMeasurementSnapshot(item.MeasurementSnapshot)),
                ct);
            if (evaluation is null)
            {
                throw new InvalidOperationException($"لا توجد قاعدة استهلاك رسمية مطابقة للبند {index + 1}.");
            }

            if (!IsInchUnit(evaluation.Unit))
            {
                throw new InvalidOperationException($"وحدة استهلاك القماش للبند {index + 1} ليست بوصة معتمدة.");
            }

            var requiredInches = evaluation.Value * item.Quantity;
            if (requiredInches <= 0)
            {
                throw new InvalidOperationException($"تعذر حساب استهلاك القماش للبند {index + 1}.");
            }

            var pricing = await pricingEngine.CalculateAsync(new PricingEngineRequestDto
            {
                ProductTypeId = item.ProductTypeId,
                FabricCode = fabricCode,
                Consumption = evaluation.Value,
                ConsumptionUnit = evaluation.Unit,
                Quantity = item.Quantity,
            }, ct);
            if (!pricing.IsReady || pricing.FabricCostPerPiece is null || pricing.FullCostPerPiece is null || pricing.FinalPricePerPiece is null)
            {
                var reason = pricing.Reasons.Count == 0
                    ? "تعذر إصدار السعر الرسمي للبند."
                    : string.Join(" ", pricing.Reasons);
                throw new InvalidOperationException($"البند {index + 1}: {reason}");
            }

            lines.Add(new ReadyMadePricingLine(item, fabricCode.ToUpperInvariant(), requiredInches, pricing));
        }

        return lines;
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
                var raw = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value >= 0)
                {
                    values[property.Name] = value;
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("بيانات المقاسات المحفوظة غير صالحة.");
        }

        return values;
    }

    private static string MergePricingSnapshot(string? measurementSnapshot, PricingEngineResponseDto pricing)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(measurementSnapshot))
        {
            try
            {
                using var document = JsonDocument.Parse(measurementSnapshot);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        payload[property.Name] = property.Value.Clone();
                    }
                }
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("بيانات المقاسات المحفوظة غير صالحة.");
            }
        }

        payload["_pricing"] = new Dictionary<string, object?>
        {
            ["productTypeId"] = pricing.ProductTypeId,
            ["consumptionPerPiece"] = pricing.ConsumptionPerPiece,
            ["consumptionUnit"] = pricing.ConsumptionUnit,
            ["inchPrice"] = pricing.InchPrice,
            ["fabricCostPerPiece"] = pricing.FabricCostPerPiece,
            ["operationalCostPerPiece"] = pricing.OperationalCostPerPiece,
            ["fullCostPerPiece"] = pricing.FullCostPerPiece,
            ["fullCostTotal"] = pricing.FullCostTotal,
            ["pieceProfitPercentage"] = pricing.PieceProfitPercentage,
            ["globalProfitPercentage"] = pricing.GlobalProfitPercentage,
            ["finalPricePerPiece"] = pricing.FinalPricePerPiece,
            ["finalPriceTotal"] = pricing.FinalPriceTotal,
        };
        return JsonSerializer.Serialize(payload);
    }

    private static ReadyMadePricingSnapshot? ReadPricingSnapshot(string? measurementSnapshot)
    {
        if (string.IsNullOrWhiteSpace(measurementSnapshot)) return null;
        try
        {
            using var document = JsonDocument.Parse(measurementSnapshot);
            if (!document.RootElement.TryGetProperty("_pricing", out var pricing)
                || pricing.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ReadyMadePricingSnapshot(
                pricing.GetProperty("fullCostPerPiece").GetDecimal(),
                pricing.GetProperty("finalPricePerPiece").GetDecimal());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static bool IsInchUnit(string? unit) =>
        string.Equals(unit?.Trim(), "Inch", StringComparison.OrdinalIgnoreCase)
        || string.Equals(unit?.Trim(), "Inches", StringComparison.OrdinalIgnoreCase)
        || string.Equals(unit?.Trim(), "بوصة", StringComparison.OrdinalIgnoreCase);

    private static async Task<int?> FindReadyMadeOrderByRequestReferenceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string? requestReference,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestReference)) return null;
        const string sql = @"
            SELECT TOP (1) ReadyMadeProductionOrderId
            FROM dbo.ReadyMadeProductionOrders WITH (UPDLOCK, HOLDLOCK)
            WHERE CHARINDEX(@marker, ISNULL(Notes, N'')) > 0;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@marker", $"[RequestReference:{requestReference.Trim()}]");
        var value = await command.ExecuteScalarAsync(ct);
        return value is int orderId ? orderId : null;
    }

    private static string? AppendRequestReference(string? notes, string? requestReference)
    {
        if (string.IsNullOrWhiteSpace(requestReference)) return notes;
        var marker = $"[RequestReference:{requestReference.Trim()}]";
        return string.IsNullOrWhiteSpace(notes) ? marker : $"{notes.Trim()}\n{marker}";
    }

    private static async Task<ReadyMadeFabricStock> LoadReadyMadeFabricStockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string fabricCode,
        CancellationToken ct)
    {
        int? legacyCode = int.TryParse(fabricCode, out var parsedCode) ? parsedCode : null;
        decimal legacyAvailable = 0;
        decimal legacyFactor = 36m;
        var hasLegacyRecord = false;
        if (legacyCode is not null)
        {
            const string legacySql = @"
                SELECT TOP (1) Unit, AvailableQuantity
                FROM dbo.Fabrics_Inventory WITH (UPDLOCK, HOLDLOCK)
                WHERE FabricCode = @fabricCode;";
            await using var legacyCommand = new SqlCommand(legacySql, connection, transaction);
            legacyCommand.Parameters.AddWithValue("@fabricCode", legacyCode.Value);
            await using var legacyReader = await legacyCommand.ExecuteReaderAsync(ct);
            if (await legacyReader.ReadAsync(ct))
            {
                hasLegacyRecord = true;
                legacyFactor = StorageUnitToInches(legacyReader.NullableString("Unit"));
                legacyAvailable = legacyReader.NullableDecimal("AvailableQuantity") ?? 0m;
            }
            await legacyReader.CloseAsync();
        }

        const string itemSql = @"
            SELECT TOP (1) InventoryItemID, Unit, AvailableQuantity, InchPrice, YardPrice
            FROM dbo.InventoryItems WITH (UPDLOCK, HOLDLOCK)
            WHERE ItemCode = @fabricCode AND IsActive = 1;";
        await using var itemCommand = new SqlCommand(itemSql, connection, transaction);
        itemCommand.Parameters.AddWithValue("@fabricCode", fabricCode);
        await using var itemReader = await itemCommand.ExecuteReaderAsync(ct);
        if (!await itemReader.ReadAsync(ct))
        {
            throw new InvalidOperationException($"كود القماش {fabricCode} لا يرتبط بسجل مخزون تشغيلي للحركة.");
        }

        var inventoryItemId = itemReader.GetInt32(0);
        var inventoryUnit = itemReader.NullableString("Unit") ?? "Yard";
        var inventoryAvailable = itemReader.NullableDecimal("AvailableQuantity") ?? 0m;
        var inventoryFactor = StorageUnitToInches(inventoryUnit);
        var inventoryUnitCost = inventoryFactor == 36m
            ? itemReader.NullableDecimal("YardPrice") ?? 0m
            : itemReader.NullableDecimal("InchPrice") ?? 0m;
        await itemReader.CloseAsync();

        if (!hasLegacyRecord)
        {
            return new ReadyMadeFabricStock(
                fabricCode,
                null,
                inventoryItemId,
                inventoryUnit,
                inventoryFactor,
                null,
                inventoryAvailable * inventoryFactor,
                inventoryUnitCost);
        }

        var legacyAvailableInches = legacyAvailable * legacyFactor;
        var inventoryAvailableInches = inventoryAvailable * inventoryFactor;
        return new ReadyMadeFabricStock(
            fabricCode,
            legacyCode,
            inventoryItemId,
            inventoryUnit,
            inventoryFactor,
            legacyFactor,
            Math.Min(legacyAvailableInches, inventoryAvailableInches),
            inventoryUnitCost);
    }

    private static async Task DeductReadyMadeFabricAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string orderNumber,
        ReadyMadeFabricRequirement requirement,
        ReadyMadeFabricStock stock,
        DateTime now,
        CancellationToken ct)
    {
        if (stock.AvailableInches < requirement.RequiredInches)
        {
            throw new InvalidOperationException(
                $"الكمية المتوفرة للقماش {requirement.FabricCode} لا تكفي لهذا الأمر.");
        }

        if (stock.LegacyFabricCode is not null)
        {
            const string legacySql = @"
                UPDATE dbo.Fabrics_Inventory
                SET AvailableQuantity = AvailableQuantity - @quantity,
                    UsedQuantity = ISNULL(UsedQuantity, 0) + @quantity
                WHERE FabricCode = @fabricCode AND AvailableQuantity >= @quantity;";
            await using var legacyCommand = new SqlCommand(legacySql, connection, transaction);
            legacyCommand.Parameters.AddWithValue("@fabricCode", stock.LegacyFabricCode.Value);
            legacyCommand.Parameters.AddWithValue("@quantity", requirement.RequiredInches / (stock.LegacyUnitFactor ?? 36m));
            if (await legacyCommand.ExecuteNonQueryAsync(ct) != 1)
            {
                throw new InvalidOperationException($"تعذر خصم رصيد القماش {requirement.FabricCode}.");
            }
        }

        var inventoryQuantity = requirement.RequiredInches / stock.InventoryUnitFactor;
        const string inventorySql = @"
            UPDATE dbo.InventoryItems
            SET CurrentQuantity = CurrentQuantity - @quantity,
                AvailableQuantity = AvailableQuantity - @quantity,
                UpdatedAt = @updatedAt
            WHERE InventoryItemID = @inventoryItemId
              AND AvailableQuantity >= @quantity;";
        await using var inventoryCommand = new SqlCommand(inventorySql, connection, transaction);
        inventoryCommand.Parameters.AddWithValue("@quantity", inventoryQuantity);
        inventoryCommand.Parameters.AddWithValue("@updatedAt", now);
        inventoryCommand.Parameters.AddWithValue("@inventoryItemId", stock.InventoryItemId);
        if (await inventoryCommand.ExecuteNonQueryAsync(ct) != 1)
        {
            throw new InvalidOperationException($"تعذر خصم رصيد القماش {requirement.FabricCode} من المخزون التشغيلي.");
        }

        const string movementSql = @"
            IF NOT EXISTS (
                SELECT 1 FROM dbo.InventoryTransactions WITH (UPDLOCK, HOLDLOCK)
                WHERE ReferenceNumber = @reference AND TransactionType = N'Consumption')
            INSERT INTO dbo.InventoryTransactions
                (InventoryItemID, TransactionType, Quantity, ReferenceNumber, Notes, CreatedAt, TotalCostImpact, UnitCost)
            VALUES
                (@inventoryItemId, N'Consumption', @quantity, @reference, @notes, @createdAt, @totalCost, @unitCost);";
        await using var movementCommand = new SqlCommand(movementSql, connection, transaction);
        movementCommand.Parameters.AddWithValue("@inventoryItemId", stock.InventoryItemId);
        movementCommand.Parameters.AddWithValue("@quantity", inventoryQuantity);
        movementCommand.Parameters.AddWithValue("@reference", $"{orderNumber}:Fabric:{requirement.FabricCode}");
        movementCommand.Parameters.AddWithValue("@notes", $"استهلاك قماش لأمر الإنتاج الجاهز. الكمية بالبوصة: {requirement.RequiredInches:0.##}");
        movementCommand.Parameters.AddWithValue("@createdAt", now);
        movementCommand.Parameters.AddWithValue("@totalCost", inventoryQuantity * stock.InventoryUnitCost);
        movementCommand.Parameters.AddWithValue("@unitCost", stock.InventoryUnitCost);
        await movementCommand.ExecuteNonQueryAsync(ct);
    }

    private static decimal StorageUnitToInches(string? unit) =>
        unit?.Trim().ToLowerInvariant() switch
        {
            "inch" or "inches" or "بوصة" => 1m,
            _ => 36m,
        };
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken ct) => QueryAsync("SELECT o.OrderID,o.OrderNumber,o.CustomerID,c.CustomerCode,c.CustomerName,c.PhoneNumber,o.OrderStatus,o.DeliveryDate FROM dbo.Orders o INNER JOIN dbo.Customers c ON c.CustomerID=o.CustomerID ORDER BY CASE WHEN o.DeliveryDate IS NULL THEN 1 ELSE 0 END,o.DeliveryDate,o.OrderID DESC", reader => new ProductionDeliveryDto(reader.GetInt32(0),reader.GetString(1),reader.GetInt32(2),reader.NullableString("CustomerCode"),reader.NullableString("CustomerName"),reader.NullableString("PhoneNumber"),reader.GetString(6),reader.NullableDateTime("DeliveryDate")), null, ct);

    private static PieceDto MapPiece(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8), reader.NullableInt32("ProductTypeId"));
    private static PieceDto MapReadyMadePiece(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetDateTime(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8), reader.NullableInt32("ProductTypeId"), true, reader.GetInt32(0));

    private static async Task<string> GetNextReadyMadeOrderNumberAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "ProductionTrackingPrefix", "RMP-", ct);
        var sql = @"
            SELECT ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(ProductionOrderNumber, CHARINDEX('-', ProductionOrderNumber) + 1, 20))), 0) + 1
            FROM dbo.ReadyMadeProductionOrders WITH (TABLOCKX, HOLDLOCK)
            WHERE ProductionOrderNumber LIKE @prefix;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", $"{prefix}%");
        var next = (int)(await command.ExecuteScalarAsync(ct))!;
        return $"{prefix}{next:D6}";
    }

    private static async Task<bool> ProductTypeExistsAsync(SqlConnection connection, SqlTransaction transaction, int productTypeId, CancellationToken ct)
    {
        const string sql = "SELECT TOP (1) 1 FROM dbo.PricingProductTypes WHERE ProductTypeId = @productTypeId AND IsActive = 1";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@productTypeId", productTypeId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<string> GetNextTrackingCodeAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "PieceTrackingPrefix", "TRK-", ct);
        var sql = @"
            SELECT ISNULL(MAX(TRY_CONVERT(int, REPLACE(TrackingCode, @prefix2, ''))), 0) + 1
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

    private sealed record PieceCorrectionContext(int PieceId, string TrackingCode, string PieceType, string PieceStatus, int ProductTypeId);
    private sealed record TrackingEventCorrectionContext(int TrackingEventId, string Stage, string? EmployeeCode, DateTime EventTime, string? Notes);
    private sealed record PieceWageCorrectionContext(int PieceWageRecordId, string Status, int? PayrollPeriodId, int? PayrollRecordId);
    private sealed record EmployeeCorrectionContext(int EmployeeId, string EmployeeCode, string? FullName, string? JobTitle, bool IsActive);
    private sealed record ReadyMadeFabricRequirement(string FabricCode, decimal RequiredInches);
    private sealed record ReadyMadePricingLine(
        ReadyMadeProductionOrderCreateItemDto Item,
        string FabricCode,
        decimal RequiredInches,
        PricingEngineResponseDto Pricing);
    private sealed record ReadyMadePricingSnapshot(decimal FullCostPerPiece, decimal FinalPricePerPiece);
    private sealed record ReadyMadeFabricStock(
        string FabricCode,
        int? LegacyFabricCode,
        int InventoryItemId,
        string InventoryUnit,
        decimal InventoryUnitFactor,
        decimal? LegacyUnitFactor,
        decimal AvailableInches,
        decimal InventoryUnitCost);

    private sealed record OrderMonitoringSummary(
        int OrderId,
        string OrderNumber,
        string CustomerCode,
        string CustomerName,
        DateTime? OrderDate,
        DateTime? DeliveryDate,
        string OrderStatus,
        int TotalPieces,
        int CompletedPieces,
        int IncompletePieces,
        int DeliveredPieces,
        int InProgressPieces,
        DateTime? LastTrackingEventAt,
        int TrackingEventCount);
}