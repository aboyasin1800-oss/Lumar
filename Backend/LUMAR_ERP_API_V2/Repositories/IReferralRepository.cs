using LUMAR_ERP_API_V2.DTOs.Referral;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IReferralRepository
{
    Task<ReferralDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralDashboardSearchResultDto>> SearchDashboardAsync(string query, CancellationToken cancellationToken);
    Task<ReferralAnalyticsDto> GetAnalyticsAsync(string? search, DateTime? from, DateTime? to, CancellationToken cancellationToken);
    Task<ReferralRewardsScreenDto> GetRewardsScreenAsync(string? transactionType, DateTime? from, DateTime? to, int? customerId, string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralCodeDto>> GetCodesByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<ReferralAccountSummaryDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<ReferralAccountSummaryDto> UpsertAccountAsync(ReferralAccountSummaryDto account, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<ReferralTransactionDto> CreateRewardGrantedAsync(int referrerCustomerId, int referredCustomerId, int? referralCodeId, int orderId, decimal fixedRewardAmount, decimal loyaltyPoints, string notes, CancellationToken cancellationToken);
    Task<ReferralCodeDto?> EnsureCodeAsync(int customerId, string? preferredCode, CancellationToken cancellationToken);
    Task<RegisterReferralResponse> RegisterAsync(RegisterReferralRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralRootDto>> GetRootsAsync(CancellationToken cancellationToken);
    Task<ReferralTreeDto?> GetTreeAsync(int customerId, CancellationToken cancellationToken);
}
