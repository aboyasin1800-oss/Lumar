using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class WipToFinishedGoodsFinancialTransactionPolicyTests
{
    [Fact]
    public void ShouldCreateTransaction_ForNewReadyMadeProductWithPositiveActualCost()
    {
        var shouldCreate = WipToFinishedGoodsFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 1250.00m, false);

        Assert.True(shouldCreate);
        Assert.Equal("TRK-000999", WipToFinishedGoodsFinancialTransactionPolicy.GetReferenceNumber("TRK-000999"));
        Assert.Equal("Transfer from Work In Progress to Finished Goods", WipToFinishedGoodsFinancialTransactionPolicy.GetDescription());
    }

    [Fact]
    public void ShouldNotCreateDuplicateTransaction_ForSameTrackingCode()
    {
        var shouldCreate = WipToFinishedGoodsFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 1250.00m, true);

        Assert.False(shouldCreate);
    }

    [Fact]
    public void ShouldNotCreateTransaction_ForEmptyTrackingCodeOrZeroAmount()
    {
        Assert.False(WipToFinishedGoodsFinancialTransactionPolicy.ShouldCreateTransaction(null, 1250.00m, false));
        Assert.False(WipToFinishedGoodsFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 0m, false));
    }
}
