using System.Globalization;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class VipLevelEvaluationService(
    IVipLevelEvaluationRepository repository,
    ILoyaltyRepository loyaltyRepository) : IVipLevelEvaluationService
{
    public Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken) =>
        repository.GetCriteriaAsync(cancellationToken);

    public Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken) =>
        repository.GetClassifiedCustomersAsync(cancellationToken);

    public async Task<VipLevelEvaluationDto?> GetCustomerEvaluationAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new ArgumentException("CustomerId must be positive.");

        var rows = await repository.GetSourceRowsAsync(customerId, cancellationToken);
        return (await EvaluateRowsAsync(rows, cancellationToken, persist: false)).SingleOrDefault();
    }

    public async Task<VipLevelEvaluationDto?> EvaluateCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new ArgumentException("CustomerId must be positive.");

        await loyaltyRepository.EnsureAccountAsync(customerId, cancellationToken);
        var rows = await repository.GetSourceRowsAsync(customerId, cancellationToken);
        var evaluations = await EvaluateRowsAsync(rows, cancellationToken, persist: true);
        return evaluations.SingleOrDefault();
    }

    public async Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateAllAsync(CancellationToken cancellationToken)
    {
        var rows = await repository.GetSourceRowsAsync(null, cancellationToken);
        return await EvaluateRowsAsync(rows, cancellationToken, persist: true);
    }

    public async Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateNetworkAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new ArgumentException("CustomerId must be positive.");

        var affectedCustomerIds = await repository.GetAffectedCustomerIdsAsync(customerId, cancellationToken);
        foreach (var affectedCustomerId in affectedCustomerIds)
        {
            await loyaltyRepository.EnsureAccountAsync(affectedCustomerId, cancellationToken);
        }

        var rows = await repository.GetSourceRowsAsync(null, cancellationToken);
        var affected = affectedCustomerIds.ToHashSet();
        return await EvaluateRowsAsync(rows.Where(row => affected.Contains(row.CustomerId)).ToList(), cancellationToken, persist: true);
    }

    private async Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateRowsAsync(
        IReadOnlyList<VipLevelEvaluationSourceDto> rows,
        CancellationToken cancellationToken,
        bool persist)
    {
        var criteria = (await repository.GetCriteriaAsync(cancellationToken))
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.VipLevelId)
            .ToList();

        if (criteria.Count == 0) throw new InvalidOperationException("VIP evaluation criteria are not configured.");

        var evaluations = new List<VipLevelEvaluationDto>(rows.Count);
        foreach (var row in rows)
        {
            var selected = VipLevelEvaluationCalculator.SelectLevel(row, criteria);
            var evaluatedAt = DateTime.UtcNow;
            var changed = row.CurrentVipLevelId != selected.Criteria.VipLevelId;
            var reason = BuildReason(row, selected.Criteria, selected.Score);

            if (persist)
            {
                await repository.SaveEvaluationAsync(
                    row.CustomerId,
                    selected.Criteria.VipLevelId,
                    row.DirectReferralCount,
                    row.NetworkSize,
                    row.NetworkMaxDepth,
                    row.OwnOrderCount,
                    row.NetworkOrderCount,
                    selected.Score,
                    reason,
                    evaluatedAt,
                    cancellationToken);
            }

            evaluations.Add(new VipLevelEvaluationDto(
                row.CustomerId,
                row.CustomerCode,
                row.CustomerName,
                row.CurrentVipLevelId,
                row.CurrentVipLevelCode,
                row.CurrentVipLevelDisplayName,
                selected.Criteria.VipLevelId,
                selected.Criteria.Code,
                selected.Criteria.DisplayName,
                row.DirectReferralCount,
                row.Level1ReferralCount,
                row.Level2ReferralCount,
                row.Level3ReferralCount,
                row.Level4ReferralCount,
                row.NetworkSize,
                row.NetworkMaxDepth,
                row.OwnOrderCount,
                row.NetworkOrderCount,
                selected.Score,
                reason,
                evaluatedAt,
                changed));
        }

        return evaluations;
    }

    private static string BuildReason(
        VipLevelEvaluationSourceDto row,
        VipLevelEvaluationCriteriaDto criteria,
        decimal score)
    {
        static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        var baseline = criteria.MinimumScore <= 0m
            ? "المستوى الأساسي؛ لا يوجد حد اجتياز لهذا المستوى. "
            : string.Empty;

        return baseline + $"المستوى {criteria.DisplayName} وفق محرك الإحالات والنشاط: " +
            $"إحالات مباشرة {row.DirectReferralCount}، شجرة {row.NetworkSize} " +
            $"(المستويات 1-4: {row.Level1ReferralCount}/{row.Level2ReferralCount}/{row.Level3ReferralCount}/{row.Level4ReferralCount})، " +
            $"طلبات العميل {row.OwnOrderCount}، طلبات الشبكة {row.NetworkOrderCount}، " +
            $"الدرجة {Number(score)}% من حد {Number(criteria.MinimumScore)}%. " +
            $"الأوزان: مباشر {Number(criteria.DirectReferralWeight)}%، العميل {Number(criteria.OwnOrderWeight)}%، " +
            $"الشبكة {Number(criteria.NetworkOrderWeight)}%، حجم الشجرة {Number(criteria.NetworkSizeWeight)}%."
            .Trim();
    }
}
