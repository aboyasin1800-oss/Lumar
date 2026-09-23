using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Services;

public interface IVipLevelEvaluationService
{
    Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken);
    Task<VipLevelEvaluationDto?> GetCustomerEvaluationAsync(int customerId, CancellationToken cancellationToken);
    Task<VipLevelEvaluationDto?> EvaluateCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateNetworkAsync(int customerId, CancellationToken cancellationToken);
}
