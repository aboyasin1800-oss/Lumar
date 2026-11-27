using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IPrintingRepository
{
    Task<IReadOnlyList<MeasurementCardPrintHistoryDto>> GetPieceHistoryAsync(int pieceId, bool isReadyMade, CancellationToken cancellationToken);
    Task<MeasurementCardStatusSummaryDto> GetStatusSummaryAsync(int? orderId, CancellationToken cancellationToken);
    Task<MeasurementCardEligibleSnapshotDto> GetEligibleSnapshotAsync(int? orderId, CancellationToken cancellationToken);
    Task<MeasurementCardBulkPrepareResultDto> PrepareBulkAsync(MeasurementCardBulkPrepareDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardBulkConfirmationResultDto> ConfirmBulkAsync(MeasurementCardBulkConfirmationDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardBulkConfirmationResultDto> CancelBulkAsync(MeasurementCardBulkConfirmationDto request, string? reason, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardBulkReconcileResultDto> ReconcileBulkAsync(MeasurementCardBulkReconcileDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardBulkConfirmationResultDto> RecoverBulkAsync(MeasurementCardBulkRecoveryDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto> PrepareAsync(int pieceId, bool isReadyMade, PrepareMeasurementCardPrintDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto> CompleteAsync(int printHistoryId, int? cashAccountId, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto?> FailAsync(int printHistoryId, string? failureReason, CurrentUserDto user, CancellationToken cancellationToken);
}
