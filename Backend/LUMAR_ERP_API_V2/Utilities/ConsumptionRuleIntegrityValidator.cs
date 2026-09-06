using System.Globalization;

namespace LUMAR_ERP_API_V2.Utilities;

public sealed record ConsumptionRuleIntegrityCandidate(
    int ProductTypeId,
    decimal? FabricWidth,
    int? SizeClassId,
    string MeasurementKey,
    int Priority,
    decimal? MinValue,
    decimal? MaxValue,
    string Formula,
    string Status,
    int? RuleId = null);

public sealed record ConsumptionRuleIntegrityResult(
    int TotalRules,
    int ValidRules,
    int OverlapCount,
    int DuplicateCount,
    int GapCount,
    int ProductTypesCoveringAllSizes,
    int ProductTypesNeedingCompletion,
    IReadOnlyList<ConsumptionRuleIntegrityIssue> Issues);

public sealed record ConsumptionRuleIntegrityIssue(
    string Type,
    string Message,
    int? ProductTypeId,
    int? RuleId,
    string? FabricWidth,
    string? MeasurementKey);

public static class ConsumptionRuleIntegrityValidator
{
    public static ConsumptionRuleIntegrityResult Validate(IEnumerable<ConsumptionRuleIntegrityCandidate> rules)
    {
        var normalized = (rules ?? Enumerable.Empty<ConsumptionRuleIntegrityCandidate>())
            .Where(r => r is not null)
            .ToList();

        var issues = new List<ConsumptionRuleIntegrityIssue>();
        var groups = normalized
            .Where(r => string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => new { r.ProductTypeId, r.FabricWidth, r.MeasurementKey })
            .ToList();

        foreach (var group in groups)
        {
            var items = group.OrderBy(r => r.MinValue ?? decimal.MinValue)
                .ThenBy(r => r.MaxValue ?? decimal.MaxValue)
                .ToList();

            for (var i = 0; i < items.Count; i++)
            {
                var current = items[i];
                if (current.MinValue is null)
                {
                    issues.Add(new ConsumptionRuleIntegrityIssue("MISSING_MINIMUM", $"قيمة من مطلوبة للفئة {current.MeasurementKey}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                    continue;
                }

                if (!ConsumptionRuleRangePolicy.IsValid(current.MinValue, current.MaxValue))
                {
                    issues.Add(new ConsumptionRuleIntegrityIssue("INVALID_RANGE", $"النطاق غير صحيح: يجب أن تكون قيمة إلى أكبر من قيمة من في قاعدة {current.MeasurementKey}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                    continue;
                }

                for (var j = i + 1; j < items.Count; j++)
                {
                    var next = items[j];
                    if (next.MinValue is null || !ConsumptionRuleRangePolicy.IsValid(next.MinValue, next.MaxValue))
                    {
                        continue;
                    }

                    var sameRange = current.MinValue == next.MinValue && current.MaxValue == next.MaxValue;
                    if (sameRange)
                    {
                        issues.Add(new ConsumptionRuleIntegrityIssue("DUPLICATE", $"نطاق مكرر لنفس القطعة والعرض والقياس: {current.MeasurementKey}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                    }

                    var overlap = ConsumptionRuleRangePolicy.Overlaps(current.MinValue.Value, current.MaxValue, next.MinValue.Value, next.MaxValue);
                    if (overlap && !sameRange)
                    {
                        issues.Add(new ConsumptionRuleIntegrityIssue("OVERLAP", $"تداخل بين النطاقات في {current.MeasurementKey}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                    }
                }
            }

            var ordered = items
                .Where(r => r.MinValue.HasValue && ConsumptionRuleRangePolicy.IsValid(r.MinValue, r.MaxValue))
                .OrderBy(r => r.MinValue)
                .ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var current = ordered[i];
                var next = ordered[i + 1];

                if (!current.MaxValue.HasValue)
                {
                    issues.Add(new ConsumptionRuleIntegrityIssue("MISSING_MAXIMUM", $"الحد الأعلى مطلوب للفئة غير الأخيرة {current.MeasurementKey}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                    continue;
                }

                if (ConsumptionRuleRangePolicy.HasGap(current.MaxValue, next.MinValue))
                {
                    issues.Add(new ConsumptionRuleIntegrityIssue("GAP", $"فجوة بين النطاقات: {current.MeasurementKey} من {current.MinValue} إلى {current.MaxValue} ثم من {next.MinValue}.", current.ProductTypeId, current.RuleId, current.FabricWidth?.ToString(CultureInfo.InvariantCulture), current.MeasurementKey));
                }
            }
        }

        var duplicateCount = issues.Count(i => i.Type == "DUPLICATE");
        var overlapCount = issues.Count(i => i.Type == "OVERLAP");
        var gapCount = issues.Count(i => i.Type == "GAP");
        var validRules = normalized.Count(r => string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase)) - duplicateCount - overlapCount;
        var productTypesCoveringAllSizes = 0;
        var productTypesNeedingCompletion = 0;
        var productTypes = normalized
            .Where(r => string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.ProductTypeId)
            .Select(g => new { ProductTypeId = g.Key, Count = g.Count() })
            .ToList();

        foreach (var pt in productTypes)
        {
            var typeIssues = issues.Where(i => i.ProductTypeId == pt.ProductTypeId).ToList();
            if (!typeIssues.Any(i => i.Type == "GAP" || i.Type == "OVERLAP" || i.Type == "DUPLICATE"))
            {
                productTypesCoveringAllSizes++;
            }
            else
            {
                productTypesNeedingCompletion++;
            }
        }

        return new ConsumptionRuleIntegrityResult(
            normalized.Count(r => string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase)),
            Math.Max(0, validRules),
            overlapCount,
            duplicateCount,
            gapCount,
            productTypesCoveringAllSizes,
            productTypesNeedingCompletion,
            issues);
    }
}
