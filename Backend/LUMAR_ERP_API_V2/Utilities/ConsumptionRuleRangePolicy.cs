namespace LUMAR_ERP_API_V2.Utilities;

public readonly record struct ConsumptionRuleRange(decimal MinimumValue, decimal? MaximumValue)
{
    public bool Contains(decimal value) => MinimumValue <= value && (!MaximumValue.HasValue || value < MaximumValue.Value);
}

public static class ConsumptionRuleRangePolicy
{
    public static bool IsValid(decimal? minimumValue, decimal? maximumValue)
        => minimumValue.HasValue && (!maximumValue.HasValue || minimumValue.Value < maximumValue.Value);

    public static bool Overlaps(decimal minimumValue, decimal? maximumValue, decimal otherMinimumValue, decimal? otherMaximumValue)
    {
        if (!IsValid(minimumValue, maximumValue) || !IsValid(otherMinimumValue, otherMaximumValue))
        {
            return false;
        }

        return (!maximumValue.HasValue || otherMinimumValue < maximumValue.Value)
            && (!otherMaximumValue.HasValue || minimumValue < otherMaximumValue.Value);
    }

    public static bool HasGap(decimal? previousMaximumValue, decimal? nextMinimumValue)
        => previousMaximumValue.HasValue
            && nextMinimumValue.HasValue
            && nextMinimumValue.Value > previousMaximumValue.Value;

    public static bool IsConnected(decimal? previousMaximumValue, decimal? nextMinimumValue)
        => previousMaximumValue.HasValue
            && nextMinimumValue.HasValue
            && previousMaximumValue.Value == nextMinimumValue.Value;

    public static IReadOnlyList<ConsumptionRuleIntegrityCandidate> FindMatches(
        IEnumerable<ConsumptionRuleIntegrityCandidate> candidates,
        int productTypeId,
        string measurementKey,
        decimal value,
        decimal? fabricWidth = null)
    {
        var normalizedMeasurementKey = measurementKey.Trim();
        return candidates
            .Where(candidate => candidate.ProductTypeId == productTypeId)
            .Where(candidate => string.Equals(candidate.MeasurementKey.Trim(), normalizedMeasurementKey, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => !fabricWidth.HasValue || candidate.FabricWidth == fabricWidth)
            .Where(candidate => candidate.MinValue.HasValue && new ConsumptionRuleRange(candidate.MinValue.Value, candidate.MaxValue).Contains(value))
            .ToList();
    }

    public static ConsumptionRuleIntegrityCandidate? FindSingleMatch(
        IEnumerable<ConsumptionRuleIntegrityCandidate> candidates,
        int productTypeId,
        string measurementKey,
        decimal value,
        decimal? fabricWidth = null)
    {
        var matches = FindMatches(candidates, productTypeId, measurementKey, value, fabricWidth);
        if (matches.Count > 1)
        {
            throw new InvalidOperationException("تطابق أكثر من نطاق صالح مع قيمة القياس؛ بيانات النطاقات متداخلة.");
        }

        return matches.SingleOrDefault();
    }
}
