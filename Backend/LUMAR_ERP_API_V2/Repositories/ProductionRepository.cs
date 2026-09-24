using System.Globalization;
using System.Text.Json;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Consumption;
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

            var validation = ProductionTrackingEngine.ValidateTransition(identity.ProductTypeId, pieceContext.PieceStatus, request.RequestedStage);
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

            var executionSource = ResolveExecutionSource(request);
            if (executionSource == "Scanner" && NeedsScanner(request.RequestedStage) && !string.IsNullOrWhiteSpace(request.ScannerCode))
            {
                var scannerValidation = await ValidateScannerAsync(connection, transaction, request.ScannerCode, request.EmployeeCode, ct);
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
            if (sameStageAlreadyApplied)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProductionTrackingAdvanceResultDto(pieceContext.PieceId, pieceContext.TrackingCode, pieceContext.PieceStatus, pieceContext.PieceStatus, nextStage, "This stage has already been applied to the piece.", false);
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
                    identity.Code,
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
                pieceContext.IsReadyMade);
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

    private static async Task<ScannerValidationResult> ValidateScannerAsync(SqlConnection connection, SqlTransaction transaction, string? scannerCode, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scannerCode))
        {
            return ScannerValidationResult.Invalid("ScannerCode is required for this production stage.");
        }

        const string scannerSql = "SELECT ScannerId, ScannerCode, IsActive FROM dbo.Scanners WITH (NOLOCK) WHERE ScannerCode = @scannerCode";
        await using var scannerCommand = new SqlCommand(scannerSql, connection, transaction);
        scannerCommand.Parameters.AddWithValue("@scannerCode", scannerCode.Trim());
        await using var scannerReader = await scannerCommand.ExecuteReaderAsync(ct);
        if (!await scannerReader.ReadAsync(ct)) return ScannerValidationResult.Invalid("Scanner does not exist.");
        var isActive = scannerReader.GetBoolean(2);
        if (!isActive) return ScannerValidationResult.Invalid("Scanner is inactive.");

        var employeeCodeResult = await ResolveEmployeeCodeAsync(connection, transaction, scannerCode, employeeCode, ct);
        if (string.IsNullOrWhiteSpace(employeeCodeResult)) return ScannerValidationResult.Invalid("No active employee is linked to the supplied scanner code.");
        return ScannerValidationResult.Valid(employeeCodeResult);
    }

    private static async Task<string?> ResolveEmployeeCodeAsync(SqlConnection connection, SqlTransaction transaction, string? scannerCode, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scannerCode)) return null;

        const string sql = "SELECT EmployeeID, EmployeeCode, ScannerCode, IsActive FROM dbo.Employees WITH (NOLOCK) WHERE ScannerCode = @scannerCode ORDER BY EmployeeID";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@scannerCode", scannerCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        var employees = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            if (reader.GetBoolean(3)) employees.Add(reader.GetString(1));
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
        var rateResolution = PieceWageEngine.ResolveRate(pieceTypeCode, stage, rateList);
        if (!rateResolution.IsValid)
        {
            return new PieceWageInsertOutcome(false, rateResolution.Message);
        }

        var employeeId = await ResolveEmployeeIdAsync(connection, transaction, employeeCode, ct);
        if (!employeeId.HasValue)
        {
            return new PieceWageInsertOutcome(false, "The employee linked to the tracking event could not be resolved for piece wage creation.");
        }

        var existingWages = await CountPieceWageRecordsForTrackingEventAsync(connection, transaction, trackingEventId, ct);
        if (existingWages > 0)
        {
            return new PieceWageInsertOutcome(false, "A PieceWageRecord already exists for this TrackingEvent.");
        }

        var quantity = 1m;
        var totalWage = PieceWageEngine.CalculateTotalWage(quantity, rateResolution.WageRate!.Value);
        var validation = PieceWageEngine.ValidateRecord(pieceTypeCode, stage, rateResolution.WageRate.Value, employeeCode, quantity, existingWages);
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
        command.Parameters.AddWithValue("@wageRate", rateResolution.WageRate.Value);
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

        const string sql = "SELECT EmployeeID FROM dbo.Employees WITH (NOLOCK) WHERE EmployeeCode = @employeeCode AND IsActive = 1 ORDER BY EmployeeID";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeCode", employeeCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return reader.GetInt32(0);
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
        return productId is null
            ? ReadyMadeInventoryTransferOutcome.Failed
            : new ReadyMadeInventoryTransferOutcome(Convert.ToInt32(productId), true, true);
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

        const string updateSql = "UPDATE dbo.Orders SET OrderStatus = N'ReadyForDelivery' WHERE OrderID = @orderId AND OrderStatus <> N'Cancelled'";
        await using var updateCommand = new SqlCommand(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@orderId", orderId);
        return await updateCommand.ExecuteNonQueryAsync(ct) > 0;
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

    private static bool RequiresPieceWage(string stage) =>
        string.Equals(stage, "Cutting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, "Sewing", StringComparison.OrdinalIgnoreCase);

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
    private sealed record ScannerValidationResult(bool IsValid, string Message)
    {
        public static ScannerValidationResult Valid(string employeeCode) => new(true, employeeCode);
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
        const string sql = "SELECT COUNT(*), SUM(CASE WHEN p.PieceStatus IN (N'Printing',N'Cutting',N'InProduction') THEN 1 ELSE 0 END), SUM(CASE WHEN p.PieceStatus=N'Ready' THEN 1 ELSE 0 END), SUM(CASE WHEN p.PieceStatus=N'Delivered' THEN 1 ELSE 0 END), (SELECT COUNT(DISTINCT Stage) FROM dbo.TrackingEvents), (SELECT COUNT(*) FROM dbo.ReadyMadeInventoryProducts WHERE Status=N'AvailableForSale' AND IsActive=1), SUM(CASE WHEN o.DeliveryDate < SYSDATETIME() AND o.OrderStatus NOT IN (N'Delivered',N'Cancelled') THEN 1 ELSE 0 END) FROM dbo.Pieces p INNER JOIN dbo.OrderItems oi ON oi.OrderItemID=p.OrderItemID INNER JOIN dbo.Orders o ON o.OrderID=oi.OrderID";
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

            foreach (var requirement in fabricRequirements)
            {
                await DeductReadyMadeFabricAsync(
                    connection,
                    transaction,
                    orderNumber,
                    requirement,
                    lockedFabricStocks[requirement.FabricCode],
                    now,
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