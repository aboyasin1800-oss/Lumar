using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PiecePointSettingsService(IPiecePointSettingsRepository repository) : IPiecePointSettingsService
{
    public Task<IReadOnlyList<ProductLoyaltyPointSettingDto>> GetProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => repository.GetProductLoyaltyPointSettingsAsync(cancellationToken);
    public Task<ProductLoyaltyPointSettingDto?> UpsertProductLoyaltyPointSettingAsync(int productTypeId, UpdateProductLoyaltyPointSettingDto request, CancellationToken cancellationToken) => repository.UpsertProductLoyaltyPointSettingAsync(productTypeId, request, cancellationToken);
    public Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypeLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => repository.GetReadyMadeProductTypeLoyaltyPointSettingsAsync(cancellationToken);
    public Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(int productTypeId, UpdateReadyMadeProductTypeLoyaltyPointSettingDto request, CancellationToken cancellationToken) => repository.UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(productTypeId, request, cancellationToken);
    public Task<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>> GetImportedProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => repository.GetImportedProductLoyaltyPointSettingsAsync(cancellationToken);
    public Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedProductLoyaltyPointSettingAsync(int importedReadyMadeProductId, UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken) => repository.UpsertImportedProductLoyaltyPointSettingAsync(importedReadyMadeProductId, request, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> GetActiveProductPointSettingAsync(int productTypeId, CancellationToken cancellationToken) => repository.GetActiveProductPointSettingAsync(productTypeId, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> GetActiveReadyMadeProductTypePointSettingAsync(int productTypeId, CancellationToken cancellationToken) => repository.GetActiveReadyMadeProductTypePointSettingAsync(productTypeId, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> GetActiveImportedPointSettingAsync(int importedReadyMadeProductId, CancellationToken cancellationToken) => repository.GetActiveImportedPointSettingAsync(importedReadyMadeProductId, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> GetActivePieceSettingAsync(string pieceCode, CancellationToken cancellationToken) => repository.GetActivePieceSettingAsync(pieceCode, cancellationToken);

    public Task<LoyaltyProgramSettingsDto?> GetProgramSettingsAsync(CancellationToken cancellationToken) => repository.GetProgramSettingsAsync(cancellationToken);

    public Task<PiecePointsResultDto> EvaluatePiecePointsAsync(int customerId, int orderId, string pieceCode, decimal quantity, string source, string? notes, CancellationToken cancellationToken) => repository.EvaluatePiecePointsAsync(customerId, orderId, pieceCode, quantity, source, notes, cancellationToken);
}
