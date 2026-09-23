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
        Assert.Contains("قريب", result.Reason, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("بطيء", result.Reason, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("عالق", result.Reason, StringComparison.OrdinalIgnoreCase);
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
