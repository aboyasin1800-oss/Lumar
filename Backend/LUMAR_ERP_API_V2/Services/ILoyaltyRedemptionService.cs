using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Services;

public interface ILoyaltyRedemptionService
{
    Task<LoyaltyRedemptionHistoryDto> GetHistoryAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoyaltyRedemptionDto>> GetHistoryByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionPreviewDto> PreviewAsync(int customerId, int orderId, decimal pointsRedeemed, decimal pointMonetaryValue, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionResultDto> ApplyAsync(int customerId, int orderId, decimal pointsRedeemed, decimal pointMonetaryValue, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionDto> ReverseAsync(int redemptionId, CancellationToken cancellationToken);
}
