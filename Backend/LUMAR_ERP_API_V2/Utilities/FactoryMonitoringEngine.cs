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
        var dueSoon = IsDueSoon(deliveryDate);
        var noRealMovement = !hasAnyTrackingEvents || lastTrackingEventAt is null || (DateTime.UtcNow - lastTrackingEventAt.Value).TotalDays >= 7;
        var slowProgress = inProgressPieces > 0 && progressPercent < 60m && (DateTime.UtcNow - (lastTrackingEventAt ?? DateTime.UtcNow)).TotalDays >= 3;

        if (dueSoon && incompletePieces > 0)
        {
            return new FactoryMonitoringClassificationResult("AtRisk", "موعد التسليم قريب وهناك قطع غير مكتملة.");
        }

        if (inProgressPieces > 0 && slowProgress)
        {
            return new FactoryMonitoringClassificationResult("Stalled", "يوجد تقدم لكنه بطيء ومؤخر نسبياً.");
        }

        if (noRealMovement || (totalPieces > 0 && inProgressPieces == 0 && completedPieces == 0 && deliveredPieces == 0 && string.Equals(normalizedOrderStatus, "New", StringComparison.OrdinalIgnoreCase)))
        {
            return new FactoryMonitoringClassificationResult("Blocked", "لا توجد حركة إنتاج حقيقية أو الطلب عالق منذ فترة طويلة.");
        }

        return new FactoryMonitoringClassificationResult("AtRisk", "حالة الطلب تحتاج متابعة.");
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
        return remaining <= 3 && remaining >= 0;
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
