using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IPrintingRepository
{
    Task<IReadOnlyList<MeasurementCardPrintHistoryDto>> GetPieceHistoryAsync(int pieceId, bool isReadyMade, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto> PrepareAsync(int pieceId, bool isReadyMade, PrepareMeasurementCardPrintDto request, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto> CompleteAsync(int printHistoryId, CurrentUserDto user, CancellationToken cancellationToken);
    Task<MeasurementCardPrintHistoryDto?> FailAsync(int printHistoryId, string? failureReason, CurrentUserDto user, CancellationToken cancellationToken);
}
