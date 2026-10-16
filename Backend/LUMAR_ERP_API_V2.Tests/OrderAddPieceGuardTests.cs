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

    [Fact]
    public void BlocksAddPiece_WhenAnyProductionTrackingExists()
    {
        var result = OrderAddPieceGuard.CanAddPiece("New", hasAnyTrackingEvent: true);

        Assert.False(result);
    }

    [Fact]
    public void BlocksAddPiece_WhenOrderIsNotNew()
    {
        var result = OrderAddPieceGuard.CanAddPiece("InProduction", hasAnyTrackingEvent: false);

        Assert.False(result);
    }
}
