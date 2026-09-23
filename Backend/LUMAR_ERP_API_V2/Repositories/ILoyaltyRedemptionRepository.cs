using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ILoyaltyRedemptionRepository
{
    Task<LoyaltyRedemptionHistoryDto> GetHistoryAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionDto?> GetByIdAsync(int redemptionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoyaltyRedemptionDto>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionResultDto> ApplyAtomicAsync(int customerId, int orderId, decimal pointsRedeemed, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionDto> CreateAsync(int customerId, int orderId, long loyaltyTransactionId, decimal pointsRedeemed, decimal pointMonetaryValue, decimal creditAmount, string referenceNumber, CancellationToken cancellationToken);
    Task<LoyaltyCreditDto> CreateCreditAsync(int customerId, int orderId, int redemptionId, decimal creditAmount, decimal pointsRedeemed, string source, string? referenceNumber, CancellationToken cancellationToken);
    Task<LoyaltyRedemptionDto> ReverseAsync(int redemptionId, long reversalTransactionId, CancellationToken cancellationToken);
}
