using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FactoryMonitoringEngineTests
{
    [Fact]
    public void ClassifyOrder_ReturnsAtRisk_WhenDueSoonAndHasIncompletePieces()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 6,
            completedPieces: 2,
            incompletePieces: 4,
            deliveredPieces: 2,
            inProgressPieces: 2,
            deliveryDate: DateTime.UtcNow.AddDays(2),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-1),
            progressPercent: 33m,
            hasAnyTrackingEvents: true,
            orderStatus: "InProduction");

        Assert.Equal("AtRisk", result.Classification);
        Assert.Contains("يومين", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassifyOrder_ReturnsStalled_WhenProgressExistsButNoRecentMovement()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 6,
            completedPieces: 1,
            incompletePieces: 5,
            deliveredPieces: 1,
            inProgressPieces: 4,
            deliveryDate: DateTime.UtcNow.AddDays(9),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-12),
            progressPercent: 20m,
            hasAnyTrackingEvents: true,
            orderStatus: "InProduction");

        Assert.Equal("Stalled", result.Classification);
        Assert.Contains("3 أيام", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassifyOrder_ReturnsBlocked_WhenNoRealMovementExists()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 5,
            completedPieces: 0,
            incompletePieces: 5,
            deliveredPieces: 0,
            inProgressPieces: 0,
            deliveryDate: DateTime.UtcNow.AddDays(20),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-30),
            progressPercent: 0m,
            hasAnyTrackingEvents: false,
            orderStatus: "New");

        Assert.Equal("Blocked", result.Classification);
        Assert.Contains("لم يبدأ", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    public void ClassifyOrder_ReturnsExcluded_ForTerminalStatus(string status)
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 3,
            completedPieces: 3,
            incompletePieces: 0,
            deliveredPieces: status == "Delivered" ? 3 : 0,
            inProgressPieces: 0,
            deliveryDate: DateTime.UtcNow.AddDays(-10),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-20),
            progressPercent: 100m,
            hasAnyTrackingEvents: true,
            orderStatus: status);

        Assert.Equal("Excluded", result.Classification);
    }

    [Fact]
    public void ClassifyOrder_ReturnsReadyForDelivery_WhenAllPiecesCompleted()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 4,
            completedPieces: 4,
            incompletePieces: 0,
            deliveredPieces: 0,
            inProgressPieces: 0,
            deliveryDate: DateTime.UtcNow.AddDays(5),
            lastTrackingEventAt: DateTime.UtcNow,
            progressPercent: 100m,
            hasAnyTrackingEvents: true,
            orderStatus: "ReadyForDelivery");

        Assert.Equal("ReadyForDelivery", result.Classification);
    }

    [Fact]
    public void ClassifyOrder_ReadyForDeliveryWinsOverOverdueOrAtRiskRules()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 1,
            completedPieces: 0,
            incompletePieces: 1,
            deliveredPieces: 0,
            inProgressPieces: 1,
            deliveryDate: DateTime.UtcNow.AddDays(-20),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-30),
            progressPercent: 10m,
            hasAnyTrackingEvents: true,
            orderStatus: "ReadyForDelivery");

        Assert.Equal("ReadyForDelivery", result.Classification);
        Assert.DoesNotContain(result.Classification, new[] { "AtRisk", "Stalled", "Blocked" });
    }

    [Fact]
    public void ClassifyOrder_DoesNotAtRisk_WhenMoreThanTwoDaysRemain()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 4,
            completedPieces: 2,
            incompletePieces: 2,
            deliveredPieces: 0,
            inProgressPieces: 1,
            deliveryDate: DateTime.UtcNow.AddDays(3),
            lastTrackingEventAt: DateTime.UtcNow,
            progressPercent: 50m,
            hasAnyTrackingEvents: true,
            orderStatus: "InProduction");

        Assert.Equal("Normal", result.Classification);
    }

    [Fact]
    public void ClassifyOrder_ReturnsStalled_WhenStartedAndStoppedForThreeDays()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 4,
            completedPieces: 1,
            incompletePieces: 3,
            deliveredPieces: 0,
            inProgressPieces: 2,
            deliveryDate: DateTime.UtcNow.AddDays(10),
            lastTrackingEventAt: DateTime.UtcNow.AddDays(-3),
            progressPercent: 25m,
            hasAnyTrackingEvents: true,
            orderStatus: "InProduction");

        Assert.Equal("Stalled", result.Classification);
    }

    [Fact]
    public void ClassifyOrder_ReturnsBlocked_WhenNeverStarted()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 4,
            completedPieces: 0,
            incompletePieces: 4,
            deliveredPieces: 0,
            inProgressPieces: 0,
            deliveryDate: DateTime.UtcNow.AddDays(10),
            lastTrackingEventAt: null,
            progressPercent: 0m,
            hasAnyTrackingEvents: false,
            orderStatus: "New");

        Assert.Equal("Blocked", result.Classification);
    }

    [Fact]
    public void ClassifyOrder_ReturnsNormal_ForActiveOrderWithoutOperationalWarning()
    {
        var result = FactoryMonitoringEngine.ClassifyOrder(
            totalPieces: 4,
            completedPieces: 2,
            incompletePieces: 2,
            deliveredPieces: 0,
            inProgressPieces: 1,
            deliveryDate: DateTime.UtcNow.AddDays(10),
            lastTrackingEventAt: DateTime.UtcNow,
            progressPercent: 50m,
            hasAnyTrackingEvents: true,
            orderStatus: "InProduction");

        Assert.Equal("Normal", result.Classification);
    }

    [Fact]
    public void ResolveDelayPiece_ChoosesMostDelayedPieceByCanonicalStageIndex()
    {
        var pieces = new[]
        {
            new FactoryMonitoringPieceSnapshot(
                PieceId: 1,
                PieceType: "SHIRT",
                TrackingCode: "TRK-1",
                CurrentStage: "Sewing",
                NextStage: "Buttons",
                LastTrackingEventAt: DateTime.UtcNow.AddDays(-3),
                LastEmployeeCode: "M-0034",
                ProgressPercent: 65m,
                IsCompleted: false),
            new FactoryMonitoringPieceSnapshot(
                PieceId: 2,
                PieceType: "SHIRT",
                TrackingCode: "TRK-2",
                CurrentStage: "FabricPrep",
                NextStage: "Cutting",
                LastTrackingEventAt: DateTime.UtcNow.AddDays(-5),
                LastEmployeeCode: "M-0035",
                ProgressPercent: 20m,
                IsCompleted: false),
            new FactoryMonitoringPieceSnapshot(
                PieceId: 3,
                PieceType: "SHIRT",
                TrackingCode: "TRK-3",
                CurrentStage: "Assembly",
                NextStage: null,
                LastTrackingEventAt: DateTime.UtcNow.AddDays(-1),
                LastEmployeeCode: "M-0036",
                ProgressPercent: 100m,
                IsCompleted: true)
        };

        var result = FactoryMonitoringEngine.ResolveDelayPiece(pieces);

        Assert.Equal(2, result.PieceId);
        Assert.Equal("FabricPrep", result.CurrentStage);
        Assert.Equal("Cutting", result.NextStage);
    }
}
