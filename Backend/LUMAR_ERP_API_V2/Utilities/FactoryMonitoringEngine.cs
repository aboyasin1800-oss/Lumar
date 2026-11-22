using System.Globalization;
using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Utilities;

public static class FactoryMonitoringEngine
{
    private static readonly string[] CanonicalStages =
    [
        "Printing",
        "FabricPrep",
        "Cutting",
        "Sewing",
        "Buttons",
        "Ironing",
        "Quality",
        "Assembly",
        "Delivered"
    ];

    public static FactoryMonitoringClassificationResult ClassifyOrder(
        int totalPieces,
        int completedPieces,
        int incompletePieces,
        int deliveredPieces,
        int inProgressPieces,
        DateTime? deliveryDate,
        DateTime? lastTrackingEventAt,
        decimal progressPercent,
        bool hasAnyTrackingEvents,
        string? orderStatus)
    {
        var normalizedOrderStatus = NormalizeOrderStatus(orderStatus);

        if (string.Equals(normalizedOrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedOrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return new FactoryMonitoringClassificationResult("Excluded", "الطلب انتهى وتخرج من مراقبة الإنتاج.");
        }

        if (string.Equals(normalizedOrderStatus, "ReadyForDelivery", StringComparison.OrdinalIgnoreCase)
            || (totalPieces > 0 && incompletePieces == 0))
        {
            return new FactoryMonitoringClassificationResult("ReadyForDelivery", "حالة الطلب جاهزة للتسليم أو اكتملت قطعه الفعلية.");
        }

        if (IsDueSoon(deliveryDate) && incompletePieces > 0)
        {
            return new FactoryMonitoringClassificationResult("AtRisk", "موعد التسليم خلال يومين والطلب غير مكتمل.");
        }

        var stoppedForThreeDays = hasAnyTrackingEvents
            && lastTrackingEventAt.HasValue
            && (DateTime.UtcNow - lastTrackingEventAt.Value).TotalDays >= 3;
        if (inProgressPieces > 0 && stoppedForThreeDays)
        {
            return new FactoryMonitoringClassificationResult("Stalled", "بدأ الإنتاج ولم تسجل حركة إنتاج منذ 3 أيام.");
        }

        var neverStarted = totalPieces > 0
            && inProgressPieces == 0
            && completedPieces == 0
            && deliveredPieces == 0
            && !hasAnyTrackingEvents;
        if (neverStarted)
        {
            return new FactoryMonitoringClassificationResult("Blocked", "لم يبدأ الإنتاج ولا توجد حركة إنتاج حقيقية.");
        }

        return new FactoryMonitoringClassificationResult("Normal", "الطلب نشط ولا ينطبق عليه تحذير تشغيلي حالي.");
    }

    public static FactoryMonitoringPieceSnapshot? ResolveDelayPiece(IEnumerable<FactoryMonitoringPieceSnapshot> pieces)
    {
        var list = pieces.Where(p => p is { IsCompleted: false, CurrentStage: not null }).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return list
            .OrderBy(p => StageIndex(p.CurrentStage))
            .ThenBy(p => p.LastTrackingEventAt ?? DateTime.MinValue)
            .ThenByDescending(p => p.ProgressPercent)
            .FirstOrDefault();
    }

    public static int StageIndex(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return int.MaxValue;
        var normalized = NormalizeStage(stage);
        for (var index = 0; index < CanonicalStages.Length; index++)
        {
            if (string.Equals(CanonicalStages[index], normalized, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    public static string? NextStageFor(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return null;
        var current = NormalizeStage(stage);
        var currentIndex = StageIndex(current);
        if (currentIndex == int.MaxValue || currentIndex >= CanonicalStages.Length - 1)
        {
            return null;
        }

        return CanonicalStages[currentIndex + 1];
    }

    public static int CalculateProgressPercent(int totalPieces, int completedPieces, int deliveredPieces)
    {
        if (totalPieces <= 0) return 0;
        var denominator = Math.Max(totalPieces, 1);
        var effectiveCompleted = Math.Max(completedPieces, deliveredPieces);
        return (int)Math.Round((effectiveCompleted / (decimal)denominator) * 100m, MidpointRounding.AwayFromZero);
    }

    public static int DaysRemaining(DateTime? deliveryDate)
    {
        if (!deliveryDate.HasValue) return 0;
        var diff = deliveryDate.Value.Date - DateTime.UtcNow.Date;
        return diff.Days;
    }

    private static bool IsDueSoon(DateTime? deliveryDate)
    {
        if (!deliveryDate.HasValue) return false;
        var remaining = DaysRemaining(deliveryDate);
        return remaining <= 2 && remaining >= 0;
    }

    private static string NormalizeOrderStatus(string? orderStatus)
    {
        if (string.IsNullOrWhiteSpace(orderStatus)) return "New";
        return orderStatus.Trim();
    }

    private static string NormalizeStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return string.Empty;
        return stage.Trim();
    }
}
