using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class CancelledPieceReadyInventoryTransferGuardTests
{
    [Fact]
    public void ShouldAllowTransfer_WhenOrderCancelledAndAssemblyCompleteAndNotTransferred()
    {
        var result = CancelledPieceReadyInventoryTransferGuard.Evaluate(
            orderCancelled: true,
            pieceStartedProduction: true,
            hasAssemblyStage: true,
            pieceStatus: "Ready",
            finalStage: "Assembly",
            alreadyTransferred: false);

        Assert.True(result.IsEligible);
        Assert.False(result.IsAlreadyTransferred);
        Assert.Equal("Assembly", result.FinalStage);
    }

    [Theory]
    [InlineData(false, true, true, "Ready", "Assembly", false)]
    [InlineData(true, false, true, "Ready", "Assembly", false)]
    [InlineData(true, true, false, "Ready", "Assembly", false)]
    [InlineData(true, true, true, "InProduction", "Assembly", false)]
    [InlineData(true, true, true, "Ready", "Assembly", true)]
    public void ShouldRejectTransfer_WhenAnyGuardFails(
        bool orderCancelled,
        bool pieceStartedProduction,
        bool hasAssemblyStage,
        string pieceStatus,
        string finalStage,
        bool alreadyTransferred)
    {
        var result = CancelledPieceReadyInventoryTransferGuard.Evaluate(
            orderCancelled,
            pieceStartedProduction,
            hasAssemblyStage,
            pieceStatus,
            finalStage,
            alreadyTransferred);

        Assert.False(result.IsEligible);
    }
}
