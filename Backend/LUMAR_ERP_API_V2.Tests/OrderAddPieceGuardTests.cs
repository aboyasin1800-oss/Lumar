using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class OrderAddPieceGuardTests
{
    [Fact]
    public void AllowsAddPiece_WhenOrderIsNew_AndNoProductionTrackingExists()
    {
        var result = OrderAddPieceGuard.CanAddPiece("New", hasAnyTrackingEvent: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData("New")]
    [InlineData("InProduction")]
    [InlineData("Confirmed")]
    public void AllowsIndependentNewPiece_BeforeReadiness_EvenWhenOtherPiecesHaveTracking(string status)
    {
        var result = OrderAddPieceGuard.CanAddPiece(status, hasAnyTrackingEvent: true);

        Assert.True(result);
    }

    [Theory]
    [InlineData("ReadyForDelivery")]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    [InlineData("Canceled")]
    [InlineData("Unknown")]
    public void BlocksAddPiece_WhenOrderCannotAcceptNewWork(string status)
    {
        var result = OrderAddPieceGuard.CanAddPiece(status, hasAnyTrackingEvent: false);

        Assert.False(result);
    }
}
