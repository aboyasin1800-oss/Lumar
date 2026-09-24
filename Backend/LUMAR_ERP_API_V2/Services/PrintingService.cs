using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PrintingService(IPrintingRepository repository) : IPrintingService
{
    public Task<IReadOnlyList<MeasurementCardPrintHistoryDto>> GetPieceHistoryAsync(int pieceId, bool isReadyMade, CancellationToken cancellationToken) =>
        repository.GetPieceHistoryAsync(pieceId, isReadyMade, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto> PrepareAsync(int pieceId, bool isReadyMade, PrepareMeasurementCardPrintDto request, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.PrepareAsync(pieceId, isReadyMade, request, user, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto> CompleteAsync(int printHistoryId, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.CompleteAsync(printHistoryId, user, cancellationToken);

    public Task<MeasurementCardPrintHistoryDto?> FailAsync(int printHistoryId, string? failureReason, CurrentUserDto user, CancellationToken cancellationToken) =>
        repository.FailAsync(printHistoryId, failureReason, user, cancellationToken);
}
