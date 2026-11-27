using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PrintingService(IPrintingRepository repository) : IPrintingService
{
    public Task<IReadOnlyList<MeasurementCardPrintHistoryDto>> GetPieceHistoryAsync(int pieceId, bool isReadyMade, CancellationToken cancellationToken) =>
        repository.GetPieceHistoryAsync(pieceId, isReadyMade, cancellationToken);

    public Task<MeasurementCardStatusSummaryDto> GetStatusSummaryAsync(int? orderId, CancellationToken cancellationToken) =>
        repository.GetStatusSummaryAsync(orderId, cancellationToken);

    public Task<MeasurementCardEligibleSnapshotDto> GetEligibleSnapshotAsync(int? orderId, CancellationToken cancellationToken) =>
        repository.GetEligibleSnapshotAsync(orderId, cancellationToken);

    public Task<MeasurementCardBulkPrepareResultDto> PrepareBulkAsync(MeasurementCardBulkPrepareDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.PrepareBulkAsync(request, user, cancellationToken);

    public Task<MeasurementCardBulkConfirmationResultDto> ConfirmBulkAsync(MeasurementCardBulkConfirmationDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.ConfirmBulkAsync(request, user, cancellationToken);

    public Task<MeasurementCardBulkConfirmationResultDto> CancelBulkAsync(MeasurementCardBulkConfirmationDto request, string? reason, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.CancelBulkAsync(request, reason, user, cancellationToken);

    public Task<MeasurementCardBulkReconcileResultDto> ReconcileBulkAsync(MeasurementCardBulkReconcileDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.ReconcileBulkAsync(request, user, cancellationToken);

    public Task<MeasurementCardBulkConfirmationResultDto> RecoverBulkAsync(MeasurementCardBulkRecoveryDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.RecoverBulkAsync(request, user, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto> PrepareAsync(int pieceId, bool isReadyMade, PrepareMeasurementCardPrintDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.PrepareAsync(pieceId, isReadyMade, request, user, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto> CompleteAsync(int printHistoryId, int? cashAccountId, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.CompleteAsync(printHistoryId, cashAccountId, user, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto?> FailAsync(int printHistoryId, string? failureReason, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.FailAsync(printHistoryId, failureReason, user, cancellationToken);
}
