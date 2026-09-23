using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class DeliveryRevenueRecognitionResolverTests
{
    [Fact]
    public void ShouldUseNetAmountAfterDiscountForRevenueRecognition()
    {
        var amount = DeliveryRevenueRecognitionResolver.ResolveRevenueAmount(1500m, 200m);

        Assert.Equal(1300m, amount);
    }

    [Fact]
    public void ShouldOnlyRecognizeWhenDeliveredAndNotAlreadyRecognized()
    {
        Assert.True(DeliveryRevenueRecognitionResolver.ShouldRecognizeRevenue(false, "Delivered"));
        Assert.False(DeliveryRevenueRecognitionResolver.ShouldRecognizeRevenue(true, "Delivered"));
        Assert.False(DeliveryRevenueRecognitionResolver.ShouldRecognizeRevenue(false, "New"));
    }

    [Fact]
    public void ShouldCreateRefundOnlyWhenPaidAmountExistsAndRefundHasNotBeenRecorded()
    {
        Assert.True(OrderCancellationFinancialMovementResolver.ShouldCreateRefund(1200m, false));
        Assert.False(OrderCancellationFinancialMovementResolver.ShouldCreateRefund(0m, false));
        Assert.False(OrderCancellationFinancialMovementResolver.ShouldCreateRefund(1200m, true));
    }

    [Fact]
    public void ShouldCreateRevenueReversalOnlyOnceForCancelledRecognizedOrder()
    {
        Assert.True(OrderCancellationFinancialMovementResolver.ShouldCreateRevenueReversal(true, false, "Cancelled"));
        Assert.False(OrderCancellationFinancialMovementResolver.ShouldCreateRevenueReversal(false, false, "Cancelled"));
        Assert.False(OrderCancellationFinancialMovementResolver.ShouldCreateRevenueReversal(true, true, "Cancelled"));
        Assert.False(OrderCancellationFinancialMovementResolver.ShouldCreateRevenueReversal(true, false, "Delivered"));
    }
}
