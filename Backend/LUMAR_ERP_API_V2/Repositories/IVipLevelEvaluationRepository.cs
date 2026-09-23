using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IVipLevelEvaluationRepository
{
    Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<VipLevelEvaluationSourceDto>> GetSourceRowsAsync(int? customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetAffectedCustomerIdsAsync(int customerId, CancellationToken cancellationToken);
    Task SaveEvaluationAsync(
        int customerId,
        int vipLevelId,
        int directReferralCount,
        int networkSize,
        int networkMaxDepth,
        int ownOrderCount,
        int networkOrderCount,
        decimal score,
        string reason,
        DateTime evaluatedAtUtc,
        CancellationToken cancellationToken);
}
