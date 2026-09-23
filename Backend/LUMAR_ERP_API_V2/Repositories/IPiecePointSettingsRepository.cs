using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IPiecePointSettingsRepository
{
    Task<IReadOnlyList<ProductLoyaltyPointSettingDto>> GetProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken);
    Task<ProductLoyaltyPointSettingDto?> UpsertProductLoyaltyPointSettingAsync(int productTypeId, UpdateProductLoyaltyPointSettingDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypeLoyaltyPointSettingsAsync(CancellationToken cancellationToken);
    Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(int productTypeId, UpdateReadyMadeProductTypeLoyaltyPointSettingDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>> GetImportedProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken);
    Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedProductLoyaltyPointSettingAsync(int importedReadyMadeProductId, UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken);
    Task<LoyaltyPiecePointSettingDto?> GetActiveProductPointSettingAsync(int productTypeId, CancellationToken cancellationToken);
    Task<LoyaltyPiecePointSettingDto?> GetActiveReadyMadeProductTypePointSettingAsync(int productTypeId, CancellationToken cancellationToken);
    Task<LoyaltyPiecePointSettingDto?> GetActiveImportedPointSettingAsync(int importedReadyMadeProductId, CancellationToken cancellationToken);
    Task<LoyaltyPiecePointSettingDto?> GetActivePieceSettingAsync(string pieceCode, CancellationToken cancellationToken);
    Task<LoyaltyProgramSettingsDto?> GetProgramSettingsAsync(CancellationToken cancellationToken);
    Task<PiecePointsResultDto> EvaluatePiecePointsAsync(int customerId, int orderId, string pieceCode, decimal quantity, string source, string? notes, CancellationToken cancellationToken);
}
