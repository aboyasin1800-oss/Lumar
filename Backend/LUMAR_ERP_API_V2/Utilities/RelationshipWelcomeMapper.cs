namespace LUMAR_ERP_API_V2.Utilities;

public static class RelationshipWelcomeMapper
{
    private static readonly IReadOnlyDictionary<string, string> StandardMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["أب"] = "والدك",
            ["أم"] = "والدتك",
            ["أخ"] = "أخاك",
            ["اخ"] = "أخاك",
            ["Brother"] = "أخاك",
            ["أخت"] = "أختك",
            ["ابن"] = "ابنك",
            ["ابنة"] = "ابنتك",
            ["ابن أخ"] = "ابن أخيك",
            ["ابن أخت"] = "ابن أختك",
            ["ابن عم"] = "ابن عمك",
            ["ابن خال"] = "ابن خالك",
            ["صديق"] = "صديقك",
            ["Friend"] = "صديقك",
        };

    private static readonly HashSet<string> InvalidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Primary",
        "???",
        "أخرى",
        "فارغ",
        "NULL",
        "لا توجد صلة"
    };

    public static string? TryMap(string? relationshipType)
    {
        if (string.IsNullOrWhiteSpace(relationshipType))
            return null;

        var normalized = relationshipType.Trim();
        if (InvalidValues.Contains(normalized))
            return null;

        return StandardMap.TryGetValue(normalized, out var mapped) ? mapped : normalized;
    }
}
