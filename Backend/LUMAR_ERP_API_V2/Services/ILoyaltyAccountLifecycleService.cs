using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Services;

public interface ILoyaltyAccountLifecycleService
{
    Task<LoyaltyAccountDto?> EvaluateAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto?> PrepareQualifyingPurchaseAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto?> MarkQualifyingPurchaseAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto?> ReactivateManuallyAsync(int customerId, CancellationToken cancellationToken);
    Task<int> EvaluateAllAsync(CancellationToken cancellationToken);
}
