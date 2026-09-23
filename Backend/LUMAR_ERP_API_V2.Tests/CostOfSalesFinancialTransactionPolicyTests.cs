using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class CostOfSalesFinancialTransactionPolicyTests
{
    [Fact]
    public void ShouldCreateTransaction_ForNewSoldTrackingCodeWithPositiveActualCost()
    {
        var shouldCreate = CostOfSalesFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 1250.00m, false);

        Assert.True(shouldCreate);
        Assert.Equal("TRK-000999", CostOfSalesFinancialTransactionPolicy.GetReferenceNumber("TRK-000999"));
        Assert.Equal("Cost of sold item", CostOfSalesFinancialTransactionPolicy.GetDescription());
    }

    [Fact]
    public void ShouldNotCreateDuplicateTransaction_ForSameTrackingCode()
    {
        var shouldCreate = CostOfSalesFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 1250.00m, true);

        Assert.False(shouldCreate);
    }

    [Fact]
    public void ShouldNotCreateTransaction_ForEmptyTrackingCodeOrZeroAmount()
    {
        Assert.False(CostOfSalesFinancialTransactionPolicy.ShouldCreateTransaction(null, 1250.00m, false));
        Assert.False(CostOfSalesFinancialTransactionPolicy.ShouldCreateTransaction("TRK-000999", 0m, false));
    }
}
