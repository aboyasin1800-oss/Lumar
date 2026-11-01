using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed partial class OrderRepository
{
    public async Task<TailoringOrderEditSnapshot> ApplyEditAsync(
        int orderId,
        TailoringOrderEditRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));
        if (request.EditOperationId == Guid.Empty)
            throw new ArgumentException("معرف العملية مطلوب لتطبيق التعديل بأمان.", nameof(request));

        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "System" : actor.Trim();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var currentSnapshot = await ReadEditSnapshotAsync(connection, transaction, orderId, cancellationToken);
            if (currentSnapshot is null)
                throw new InvalidOperationException($"طلب التعديل رقم {orderId} غير موجود.");

            if (request.Details is null && request.ChangedPieces.Count == 0 && request.AddedPieces.Count == 0 && request.CancelledPieces.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return currentSnapshot;
            }

            var sameOperation = await TryReadOperationAsync(connection, transaction, orderId, request.EditOperationId, cancellationToken);
            if (sameOperation is not null)
            {
                var replay = await ReadEditSnapshotAsync(connection, transaction, orderId, cancellationToken)
                    ?? currentSnapshot;
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            var requestHash = ComputeRequestHash(request);
            var sameHash = await TryReadOperationByHashAsync(connection, transaction, orderId, requestHash, cancellationToken);
            if (sameHash is not null && sameHash.EditOperationId != request.EditOperationId)
                throw new TailoringOrderEditConflictException("هذا التعديل تم تطبيقه سابقًا باستخدام طلب مطابق دون السماح بتكرار نفس التغييرات.");
            if (!string.Equals(request.ExpectedVersion, currentSnapshot.Version, StringComparison.Ordinal))
                throw new TailoringOrderEditConflictException("نسخة الطلب الحالية لا تطابق رقم الإصدار المتوقع، لذلك تم رفض التعديل للحفاظ على تناسق البيانات.");

            await using (var operationCommand = new SqlCommand("""
                INSERT INTO dbo.TailoringOrderEditOperations (EditOperationId,OrderId,RequestHash,ResultJson,CreatedBy,CreatedAt)
                VALUES (@editOperationId,@orderId,@requestHash,NULL,@createdBy,@createdAt)
                """, connection, transaction))
            {
                operationCommand.Parameters.AddWithValue("@editOperationId", request.EditOperationId);
                operationCommand.Parameters.AddWithValue("@orderId", orderId);
                operationCommand.Parameters.AddWithValue("@requestHash", Convert.FromHexString(requestHash));
                operationCommand.Parameters.AddWithValue("@createdBy", normalizedActor);
                operationCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                await operationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var change in request.ChangedPieces)
                await ApplyChangedPieceAsync(connection, transaction, orderId, change, cancellationToken);

            foreach (var addition in request.AddedPieces)
                await AddNewPieceAsync(connection, transaction, orderId, addition, cancellationToken, request.EditOperationId);

            foreach (var cancellation in request.CancelledPieces)
                await CancelPieceAsync(connection, transaction, orderId, cancellation, cancellationToken, normalizedActor, request.EditOperationId);

            if (request.Details is not null)
                await ApplyOrderDetailsPatchAsync(connection, transaction, orderId, request.Details, cancellationToken);

            var hadCancellation = request.CancelledPieces.Count > 0;
            await UpdateOrderTotalsAfterEditAsync(connection, transaction, orderId, hadCancellation, cancellationToken);

            var finalSnapshot = await ReadEditSnapshotAsync(connection, transaction, orderId, cancellationToken)
                ?? throw new InvalidOperationException("تعذر قراءة لقطة الطلب بعد تطبيق التعديل.");

            await using (var operationCommand = new SqlCommand("""
                UPDATE dbo.TailoringOrderEditOperations
                SET ResultJson=@resultJson
                WHERE EditOperationId=@editOperationId AND OrderId=@orderId
                """, connection, transaction))
            {
                operationCommand.Parameters.AddWithValue("@editOperationId", request.EditOperationId);
                operationCommand.Parameters.AddWithValue("@orderId", orderId);
                operationCommand.Parameters.AddWithValue("@resultJson", JsonSerializer.Serialize(new
                {
                    Version = finalSnapshot.Version,
                    Order = finalSnapshot.Order,
                    Pieces = finalSnapshot.Pieces,
                    AddedPieceIds = finalSnapshot.AddedPieceIds
                }));
                await operationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return finalSnapshot;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
            }
            throw;
        }
    }

    private static string ComputeRequestHash(TailoringOrderEditRequest request)
    {
        var payload = new
        {
            Details = request.Details,
            ChangedPieces = request.ChangedPieces,
            AddedPieces = request.AddedPieces,
            CancelledPieces = request.CancelledPieces
        };

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        return Convert.ToHexString(hash);
    }

    private static async Task<TailoringOrderEditOperationRow?> TryReadOperationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        Guid editOperationId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT TOP(1) EditOperationId, RequestHash, ResultJson
            FROM dbo.TailoringOrderEditOperations WITH (UPDLOCK,HOLDLOCK)
            WHERE OrderId=@orderId AND EditOperationId=@editOperationId
            ORDER BY CreatedAt DESC
            """, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@editOperationId", editOperationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new TailoringOrderEditOperationRow(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? Array.Empty<byte>() : (byte[])reader.GetValue(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<TailoringOrderEditOperationRow?> TryReadOperationByHashAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT TOP(1) EditOperationId, RequestHash, ResultJson
            FROM dbo.TailoringOrderEditOperations WITH (UPDLOCK,HOLDLOCK)
            WHERE OrderId=@orderId AND RequestHash=@requestHash
            ORDER BY CreatedAt DESC
            """, connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@requestHash", Convert.FromHexString(requestHash));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new TailoringOrderEditOperationRow(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? Array.Empty<byte>() : (byte[])reader.GetValue(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task ApplyChangedPieceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        TailoringExistingPieceChange change,
        CancellationToken cancellationToken)
    {
        if (change.PieceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(change), "معرف القطعة المعدلة غير صالح.");

        var patch = change.Patch;
        var update = new List<string>();
        var parameters = new List<(string Name, object Value)>();

        if (patch.ProductTypeId is > 0)
        {
            update.Add("ProductTypeId=@productTypeId");
            parameters.Add(("@productTypeId", patch.ProductTypeId.Value));
        }
        if (!string.IsNullOrWhiteSpace(patch.FabricCode))
        {
            update.Add("FabricCode=@fabricCode");
            parameters.Add(("@fabricCode", patch.FabricCode.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.CatalogNumber))
        {
            update.Add("MeasurementSnapshot = JSON_MODIFY(CAST(MeasurementSnapshot AS nvarchar(max)), '$._catalogNumber', @catalogNumber)");
            parameters.Add(("@catalogNumber", patch.CatalogNumber.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.Request1))
        {
            update.Add("Request1=@request1");
            parameters.Add(("@request1", patch.Request1.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.Request2))
        {
            update.Add("Request2=@request2");
            parameters.Add(("@request2", patch.Request2.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.SpecialRequest))
        {
            update.Add("Notes2=@specialRequest");
            parameters.Add(("@specialRequest", patch.SpecialRequest.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.Notes1))
        {
            update.Add("Notes1=@notes1");
            parameters.Add(("@notes1", patch.Notes1.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(patch.Notes2))
        {
            update.Add("Notes2=@notes2");
            parameters.Add(("@notes2", patch.Notes2.Trim()));
        }

        if (patch.Measurements is not null && patch.Measurements.Count > 0)
        {
            var measurements = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var measurement in patch.Measurements)
                measurements[measurement.Key.Trim()] = measurement.Value;
            var snapshot = JsonSerializer.Serialize(measurements);
            update.Add("MeasurementSnapshot=@measurementSnapshot");
            parameters.Add(("@measurementSnapshot", snapshot));
        }

        if (update.Count == 0)
            return;

        var sql = new StringBuilder("UPDATE dbo.OrderItems SET ");
        sql.Append(string.Join(", ", update));
        sql.Append(" WHERE OrderItemID = (SELECT OrderItemID FROM dbo.Pieces WITH (UPDLOCK,HOLDLOCK) WHERE PieceID=@pieceId AND OrderItemID IN (SELECT OrderItemID FROM dbo.OrderItems WITH (UPDLOCK,HOLDLOCK) WHERE OrderID=@orderId));");
        parameters.Add(("@pieceId", change.PieceId));
        parameters.Add(("@orderId", orderId));

        await using var command = new SqlCommand(sql.ToString(), connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AddNewPieceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        TailoringNewPiece addition,
        CancellationToken cancellationToken,
        Guid editOperationId)
    {
        if (addition.ProductTypeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(addition), "نوع المنتج الجديد غير صالح.");

        var item = new CreateOrderItemDto
        {
            PieceType = "Tailoring",
            Quantity = 1,
            FabricCode = addition.FabricCode,
            ProductTypeId = addition.ProductTypeId,
            CatalogNumber = addition.CatalogNumber,
            Request1 = addition.Request1,
            Request2 = addition.Request2,
            SpecialRequest = addition.SpecialRequest,
            Notes1 = addition.Notes1,
            Notes2 = addition.Notes2,
            MeasurementSnapshot = JsonSerializer.Serialize(addition.Measurements)
        };

        var fabricLines = await ResolveOrderFabricLinesAsync([item], cancellationToken);
        if (fabricLines.Count == 0)
            throw new InvalidOperationException("تعذر تحديد احتياج القماش للقطعة المضافة.");

        var fabricLine = fabricLines[0];
        var now = DateTime.UtcNow;
        var orderItemId = await InsertOrderItemAsync(orderId, item, fabricLine, $"ADD-{addition.AddOperationId:N}", now, connection, transaction, cancellationToken);
        var pieceId = await InsertPieceAsync(orderItemId, $"ADD-{addition.AddOperationId:N}", 1, now, connection, transaction, cancellationToken);

        if (pricingEngine is null)
            throw new InvalidOperationException("محرك التسعير الرسمي مطلوب لإضافة قطعة جديدة.");

        var quote = await pricingEngine.CalculateAsync(new PricingEngineRequestDto
        {
            ProductTypeId = addition.ProductTypeId,
            FabricCode = addition.FabricCode,
            Consumption = fabricLine.RequiredInches,
            ConsumptionUnit = "Inch",
            Quantity = 1
        }, cancellationToken);
        if (!quote.IsReady || quote.FinalPricePerPiece is null)
            throw new InvalidOperationException(string.Join(" ", quote.Reasons));

        var basePoints = 0m;
        if (pointSettings is not null)
        {
            var settings = await pointSettings.GetActiveProductPointSettingAsync(addition.ProductTypeId, cancellationToken);
            basePoints = PiecePointsEngine.CalculateForItem(settings, 1m, 0m, (await pointSettings.GetProgramSettingsAsync(cancellationToken))?.IsEnabled == true);
        }

        await InsertPieceSaleValueAsync(connection, transaction, pieceId, new PieceSaleValue(quote.FinalPricePerPiece.Value, 0m), quote, basePoints, now, cancellationToken);
        await InsertFabricAsync(orderItemId, item, fabricLine, now, connection, transaction, cancellationToken);
        await InventoryRepository.ConsumeFabricCodeInventoryAsync(
            connection,
            transaction,
            new ConsumeFabricCodeInventoryDto
            {
                FabricCode = addition.FabricCode,
                OrderItemId = orderItemId,
                QuantityInches = fabricLine.RequiredInches,
                SourceOperationId = InventoryRepository.CreateFabricConsumptionOperationId(orderItemId, false)
            },
            cancellationToken);

        await using (var addOperation = new SqlCommand("""
            INSERT INTO dbo.TailoringPieceAddOperations (AddOperationId,EditOperationId,OrderId,PieceId,RequestHash)
            VALUES (@addOperationId,@editOperationId,@orderId,@pieceId,@requestHash)
            """, connection, transaction))
        {
            addOperation.Parameters.AddWithValue("@addOperationId", addition.AddOperationId);
            addOperation.Parameters.AddWithValue("@editOperationId", editOperationId);
            addOperation.Parameters.AddWithValue("@orderId", orderId);
            addOperation.Parameters.AddWithValue("@pieceId", pieceId);
            addOperation.Parameters.AddWithValue("@requestHash", Convert.FromHexString(ComputeRequestHash(new TailoringOrderEditRequest
            {
                EditOperationId = editOperationId,
                ExpectedVersion = string.Empty,
                AddedPieces = [addition]
            })));
            await addOperation.ExecuteNonQueryAsync(cancellationToken);
        }

        var buyerCustomerId = await GetOrderCustomerIdAsync(connection, transaction, orderId, cancellationToken);
        await ApplyPieceBenefitDeltaAsync(connection, transaction, orderId, buyerCustomerId, pieceId, basePoints, "AddedPiece", cancellationToken);
    }

    private async Task CancelPieceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        TailoringPieceCancellation cancellation,
        CancellationToken cancellationToken,
        string actor,
        Guid editOperationId)
    {
        if (cancellation.PieceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(cancellation), "معرف القطعة الملغاة غير صالح.");

        await using var check = new SqlCommand("""
            SELECT TOP(1) piece.PieceID, piece.PieceStatus
            FROM dbo.Pieces piece WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.OrderItems item WITH (UPDLOCK,HOLDLOCK) ON item.OrderItemID=piece.OrderItemID
            WHERE piece.PieceID=@pieceId AND item.OrderID=@orderId
            """, connection, transaction);
        check.Parameters.AddWithValue("@pieceId", cancellation.PieceId);
        check.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"القطعة رقم {cancellation.PieceId} غير موجودة ضمن الطلب رقم {orderId}.");

        var status = reader.GetString(1);
        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase))
            return;

        var sources = new List<(long FabricConsumptionSourceId, decimal ConsumedQuantity, short UnitId, Guid SourceOperationId)>();
        await using (var sourcesCommand = new SqlCommand("""
            SELECT s.FabricConsumptionSourceId, s.ConsumedQuantity, s.UnitId, s.SourceOperationId
            FROM dbo.FabricConsumptionSources s WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.Pieces piece WITH (UPDLOCK,HOLDLOCK) ON piece.OrderItemID=s.OrderItemId
            WHERE piece.PieceID=@pieceId
              AND s.OrderItemId IS NOT NULL
              AND (s.PieceId IS NULL OR s.PieceId=piece.PieceID)
            """, connection, transaction))
        {
            sourcesCommand.Parameters.AddWithValue("@pieceId", cancellation.PieceId);
            await using var sourcesReader = await sourcesCommand.ExecuteReaderAsync(cancellationToken);
            while (await sourcesReader.ReadAsync(cancellationToken))
            {
                sources.Add((sourcesReader.GetInt64(0), sourcesReader.GetDecimal(1), sourcesReader.GetInt16(2), sourcesReader.GetGuid(3)));
            }
        }

        foreach (var source in sources)
        {
            var quantityInches = source.ConsumedQuantity * (source.UnitId == 1 ? 36m : 1m);
            await InventoryRepository.ReverseFabricConsumptionInTransactionAsync(
                connection,
                transaction,
                new ReverseFabricConsumptionDto(
                    source.FabricConsumptionSourceId,
                    cancellation.PieceId,
                    quantityInches,
                    Guid.NewGuid(),
                    cancellation.Reason,
                    actor),
                cancellationToken);
        }

        await using (var cancellationCommand = new SqlCommand("""
            INSERT INTO dbo.TailoringPieceCancellations (PieceId,EditOperationId,Reason,CancelledAt)
            VALUES (@pieceId,@editOperationId,@reason,@cancelledAt)
            """, connection, transaction))
        {
            cancellationCommand.Parameters.AddWithValue("@pieceId", cancellation.PieceId);
            cancellationCommand.Parameters.AddWithValue("@editOperationId", editOperationId);
            cancellationCommand.Parameters.AddWithValue("@reason", cancellation.Reason.Trim());
            cancellationCommand.Parameters.AddWithValue("@cancelledAt", DateTime.UtcNow);
            await cancellationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var updatePiece = new SqlCommand("UPDATE dbo.Pieces SET PieceStatus=@status WHERE PieceID=@pieceId", connection, transaction))
        {
            updatePiece.Parameters.AddWithValue("@status", "Cancelled");
            updatePiece.Parameters.AddWithValue("@pieceId", cancellation.PieceId);
            await updatePiece.ExecuteNonQueryAsync(cancellationToken);
        }

        var buyerCustomerId = await GetOrderCustomerIdAsync(connection, transaction, orderId, cancellationToken);
        var basePoints = await GetPieceBasePointsAsync(connection, transaction, cancellation.PieceId, cancellationToken);
        await ApplyPieceBenefitDeltaAsync(connection, transaction, orderId, buyerCustomerId, cancellation.PieceId, -basePoints, "CancelledPiece", cancellationToken);
    }

    private static async Task<int> GetOrderCustomerIdAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT CustomerId FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID=@orderId", connection, transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"الطلب رقم {orderId} غير موجود أثناء تحديث الحوافز.");
        return reader.GetInt32(0);
    }

    private static async Task<decimal> GetPieceBasePointsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int pieceId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP(1) OriginalBasePoints FROM dbo.TailoringPieceSaleValues WITH (UPDLOCK,HOLDLOCK) WHERE PieceId=@pieceId", connection, transaction);
        command.Parameters.AddWithValue("@pieceId", pieceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return 0m;
        return reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
    }

    private static async Task ApplyPieceBenefitDeltaAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        int buyerCustomerId,
        int pieceId,
        decimal deltaPoints,
        string source,
        CancellationToken cancellationToken)
    {
        if (buyerCustomerId <= 0 || deltaPoints == 0m)
            return;

        var buyerType = deltaPoints > 0m ? "Earn" : "Reversal";
        await LoyaltyRepository.CreateTransactionInTransactionAsync(
            connection,
            transaction,
            buyerCustomerId,
            buyerType,
            deltaPoints,
            orderId,
            null,
            "PiecePurchase",
            $"OrderId:{orderId}|PieceId:{pieceId}|Source:{source}|Delta:{Math.Abs(deltaPoints)}",
            cancellationToken);

        var referralChain = await ResolveReferralChainAsync(connection, transaction, buyerCustomerId, cancellationToken);
        foreach (var (level, referrerCustomerId, _) in referralChain)
        {
            var percentage = level switch
            {
                1 => 0.50m,
                2 => 0.25m,
                3 => 0.125m,
                4 => 0.0625m,
                _ => 0m
            };
            var referralDelta = deltaPoints * percentage;
            if (referralDelta == 0m)
                continue;

            var rewardType = referralDelta > 0m ? "RewardGranted" : "RewardReversal";
            var rewardPoints = Math.Abs(referralDelta);
            await ReferralRepository.CreateRewardInTransactionAsync(
                connection,
                transaction,
                new ReferralRewardMutation(
                    referrerCustomerId,
                    buyerCustomerId,
                    null,
                    orderId,
                    rewardType,
                    0m,
                    rewardPoints,
                    $"Level:{level}|OrderId:{orderId}|PieceId:{pieceId}|Source:{source}|Delta:{rewardPoints}"),
                cancellationToken);

            await LoyaltyRepository.CreateTransactionInTransactionAsync(
                connection,
                transaction,
                referrerCustomerId,
                "Adjust",
                referralDelta,
                orderId,
                null,
                "ReferralReward",
                $"Reward{(referralDelta > 0m ? "Granted" : "Reversal")}:Level:{level}|OrderId:{orderId}|PieceId:{pieceId}|Source:{source}",
                cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<(int Level, int ReferrerCustomerId, int ReferredCustomerId)>> ResolveReferralChainAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int buyerCustomerId,
        CancellationToken cancellationToken)
    {
        var chain = new List<(int Level, int ReferrerCustomerId, int ReferredCustomerId)>();
        var currentCustomerId = buyerCustomerId;
        var level = 1;

        while (level <= 4)
        {
            await using var command = new SqlCommand("""
                SELECT TOP(1) ReferrerCustomerId
                FROM dbo.ReferralTransactions WITH (UPDLOCK,HOLDLOCK)
                WHERE TransactionType=N'Registration' AND ReferredCustomerId=@customerId
                ORDER BY CreatedAt DESC
                """, connection, transaction);
            command.Parameters.AddWithValue("@customerId", currentCustomerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                break;

            var referrerCustomerId = reader.GetInt32(0);
            chain.Add((level, referrerCustomerId, currentCustomerId));
            currentCustomerId = referrerCustomerId;
            level++;
        }

        return chain;
    }

    private static async Task ApplyOrderDetailsPatchAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        TailoringOrderDetailsPatch patch,
        CancellationToken cancellationToken)
    {
        if (patch.SetDeliveryDate)
        {
            await using var delivery = new SqlCommand("UPDATE dbo.Orders SET DeliveryDate=@deliveryDate, UpdatedDate=SYSUTCDATETIME() WHERE OrderID=@orderId", connection, transaction);
            delivery.Parameters.AddWithValue("@orderId", orderId);
            delivery.Parameters.AddWithValue("@deliveryDate", patch.DeliveryDate ?? DateTime.UtcNow);
            await delivery.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(patch.Notes))
        {
            await using var notes = new SqlCommand("UPDATE dbo.Orders SET Notes=@notes, UpdatedDate=SYSUTCDATETIME() WHERE OrderID=@orderId", connection, transaction);
            notes.Parameters.AddWithValue("@orderId", orderId);
            notes.Parameters.AddWithValue("@notes", patch.Notes.Trim());
            await notes.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateOrderTotalsAfterEditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int orderId,
        bool hadCancellation,
        CancellationToken cancellationToken)
    {
        await using var totalCommand = new SqlCommand("""
            SELECT ISNULL(SUM(value.ApprovedSalePrice),0)
            FROM dbo.Pieces piece WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.OrderItems item WITH (UPDLOCK,HOLDLOCK) ON item.OrderItemID=piece.OrderItemID
            LEFT JOIN dbo.TailoringPieceSaleValues value WITH (UPDLOCK,HOLDLOCK) ON value.PieceID=piece.PieceID
            WHERE item.OrderID=@orderId AND piece.PieceStatus NOT IN (N'Cancelled',N'Canceled')
            """, connection, transaction);
        totalCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var totalReader = await totalCommand.ExecuteReaderAsync(cancellationToken);
        if (!await totalReader.ReadAsync(cancellationToken))
            return;

        var totalAmount = totalReader.GetDecimal(0);
        await totalReader.DisposeAsync();

        await using var paidCommand = new SqlCommand("SELECT PaidAmount FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID=@orderId", connection, transaction);
        paidCommand.Parameters.AddWithValue("@orderId", orderId);
        await using var paidReader = await paidCommand.ExecuteReaderAsync(cancellationToken);
        if (!await paidReader.ReadAsync(cancellationToken))
            throw new InvalidOperationException($"الطلب رقم {orderId} غير موجود أثناء إعادة حساب الإجمالي.");

        var currentPaid = paidReader.GetDecimal(0);
        var nextPaid = hadCancellation ? totalAmount : currentPaid;
        var refundAmount = Math.Max(0m, currentPaid - totalAmount);
        if (hadCancellation && refundAmount > 0m)
        {
            await using var payment = new SqlCommand("""
                INSERT INTO dbo.Payments (OrderID,PaymentDate,Amount,PaymentMethod,ReferenceNo,Notes,CreatedDate,PaymentKind)
                VALUES (@orderId,@date,@amount,@method,@reference,@notes,@created,@kind)
                """, connection, transaction);
            payment.Parameters.AddWithValue("@orderId", orderId);
            payment.Parameters.AddWithValue("@date", DateTime.UtcNow);
            payment.Parameters.AddWithValue("@amount", refundAmount);
            payment.Parameters.AddWithValue("@method", "Cash");
            payment.Parameters.AddWithValue("@reference", $"EditRefund:{orderId}:{Guid.NewGuid():N}");
            payment.Parameters.AddWithValue("@notes", "Refund for tailoring edit");
            payment.Parameters.AddWithValue("@created", DateTime.UtcNow);
            payment.Parameters.AddWithValue("@kind", "Refund");
            await payment.ExecuteNonQueryAsync(cancellationToken);
        }

        var remaining = totalAmount - nextPaid;
        await using (var updateOrder = new SqlCommand("""
            UPDATE dbo.Orders
            SET TotalAmount=@totalAmount,
                PaidAmount=@paidAmount,
                RemainingAmount=@remainingAmount,
                UpdatedDate=SYSUTCDATETIME()
            WHERE OrderID=@orderId
            """, connection, transaction))
        {
            updateOrder.Parameters.AddWithValue("@orderId", orderId);
            updateOrder.Parameters.AddWithValue("@totalAmount", totalAmount);
            updateOrder.Parameters.AddWithValue("@paidAmount", nextPaid);
            updateOrder.Parameters.AddWithValue("@remainingAmount", remaining);
            await updateOrder.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record TailoringOrderEditOperationRow(Guid EditOperationId, byte[] RequestHash, string? ResultJson);
}
