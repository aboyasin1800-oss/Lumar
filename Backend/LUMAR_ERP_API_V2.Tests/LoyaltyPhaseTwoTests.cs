using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class LoyaltyPhaseTwoTests
{
    [Fact]
    public void EnsureAccount_InitializesSnapshotFieldsCorrectly()
    {
        var account = LoyaltyAccountResolver.EnsureAccount(new LoyaltyAccountDto(
            0,
            42,
            0m,
            0m,
            0m,
            0m,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow),
            42);

        Assert.Equal(42, account.CustomerId);
        Assert.Equal(0m, account.CurrentPoints);
        Assert.Equal(0m, account.LifetimeEarnedPoints);
        Assert.Equal(0m, account.LifetimeRedeemedPoints);
        Assert.Equal(0m, account.PendingExpirePoints);
    }

    [Fact]
    public void Earn_UsesBalanceBeforeAndBalanceAfterAlongsideCurrentPoints()
    {
        var account = new LoyaltyAccountDto(1, 7, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);

        var result = LoyaltyAccountResolver.ApplyTransaction(account, "Earn", 50m, null, null, "Order", "Earn points from order");

        Assert.Equal(0m, result.BalanceBefore);
        Assert.Equal(50m, result.BalanceAfter);
        Assert.Equal(50m, result.Account.CurrentPoints);
        Assert.Equal(50m, result.Account.LifetimeEarnedPoints);
    }

    [Fact]
    public void Adjust_TracksSignedDeltaAndCurrentPoints()
    {
        var account = new LoyaltyAccountDto(2, 9, 25m, 25m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);

        var result = LoyaltyAccountResolver.ApplyTransaction(account, "Adjust", -10m, null, null, "ManualAdjustment", "Manual correction");

        Assert.Equal(25m, result.BalanceBefore);
        Assert.Equal(15m, result.BalanceAfter);
        Assert.Equal(15m, result.Account.CurrentPoints);
        Assert.Equal(-10m, result.Transaction.Points);
    }

    [Fact]
    public void Reversal_UsesLastBalanceAfterAsCurrentPointsBase()
    {
        var account = new LoyaltyAccountDto(3, 11, 100m, 100m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);

        var result = LoyaltyAccountResolver.ApplyTransaction(account, "Reversal", -40m, null, null, "OrderCancellation", "Reversal for cancelled order");

        Assert.Equal(100m, result.BalanceBefore);
        Assert.Equal(60m, result.BalanceAfter);
        Assert.Equal(60m, result.Account.CurrentPoints);
        Assert.Equal("Reversal", result.Transaction.TransactionType);
    }
}
