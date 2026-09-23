using LUMAR_ERP_API_V2.DTOs.Referral;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ReferralService(
    IReferralRepository repository,
    IVipLevelEvaluationService vipLevelEvaluationService) : IReferralService
{
    public Task<ReferralDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => repository.GetDashboardAsync(cancellationToken);

    public Task<IReadOnlyList<ReferralDashboardSearchResultDto>> SearchDashboardAsync(string query, CancellationToken cancellationToken) => repository.SearchDashboardAsync(query, cancellationToken);

    public Task<ReferralAnalyticsDto> GetAnalyticsAsync(string? search, DateTime? from, DateTime? to, CancellationToken cancellationToken) => repository.GetAnalyticsAsync(search, from, to, cancellationToken);

    public Task<ReferralRewardsScreenDto> GetRewardsScreenAsync(string? transactionType, DateTime? from, DateTime? to, int? customerId, string? search, CancellationToken cancellationToken) => repository.GetRewardsScreenAsync(transactionType, from, to, customerId, search, cancellationToken);

    public Task<IReadOnlyList<ReferralCodeDto>> GetCodesByCustomerAsync(int customerId, CancellationToken cancellationToken) => repository.GetCodesByCustomerAsync(customerId, cancellationToken);

    public Task<ReferralAccountSummaryDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken) => repository.GetAccountByCustomerAsync(customerId, cancellationToken);

    public Task<IReadOnlyList<ReferralTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken) => repository.GetTransactionsByCustomerAsync(customerId, cancellationToken);

    public Task<ReferralCodeDto?> EnsureCodeAsync(int customerId, string? preferredCode, CancellationToken cancellationToken) => repository.EnsureCodeAsync(customerId, preferredCode, cancellationToken);

    public async Task<RegisterReferralResponse> RegisterAsync(RegisterReferralRequest request, CancellationToken cancellationToken)
    {
        var response = await repository.RegisterAsync(request, cancellationToken);
        await vipLevelEvaluationService.EvaluateNetworkAsync(request.ReferredCustomerId, cancellationToken);
        return response;
    }

    public Task<IReadOnlyList<ReferralRootDto>> GetRootsAsync(CancellationToken cancellationToken) => repository.GetRootsAsync(cancellationToken);

    public Task<ReferralTreeDto?> GetTreeAsync(int customerId, CancellationToken cancellationToken) => repository.GetTreeAsync(customerId, cancellationToken);
}
