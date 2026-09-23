namespace LUMAR_ERP_API_V2.Services;

public interface IOrderLoyaltyIntegrationService
{
    Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderLoyaltyProcessingResult> ProcessOrderAsync(int orderId, CancellationToken cancellationToken);
}
