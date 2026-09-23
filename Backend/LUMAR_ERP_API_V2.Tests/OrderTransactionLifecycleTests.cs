using LUMAR_ERP_API_V2.Repositories;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class OrderTransactionLifecycleTests
{
    [Fact]
    public void ShouldNotAttemptRollbackAfterSuccessfulCommit()
    {
        Assert.False(OrderRepository.ShouldRollbackAfterFailure(committed: true, transactionUsable: true));
        Assert.False(OrderRepository.ShouldRollbackAfterFailure(committed: true, transactionUsable: false));
    }

    [Fact]
    public void ShouldAttemptRollbackOnlyBeforeCommitAndWhenTransactionIsStillUsable()
    {
        Assert.True(OrderRepository.ShouldRollbackAfterFailure(committed: false, transactionUsable: true));
        Assert.False(OrderRepository.ShouldRollbackAfterFailure(committed: false, transactionUsable: false));
    }
}
