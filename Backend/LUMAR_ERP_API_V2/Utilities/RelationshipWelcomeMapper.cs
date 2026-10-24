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

        if (StandardMap.TryGetValue(normalized, out var mapped))
            return mapped;

        return IsLikelyRealRelationship(normalized) ? "قريبك" : null;
    }

    private static bool IsLikelyRealRelationship(string normalized)
    {
        if (normalized.Length < 2)
            return false;

        var text = normalized.Trim();
        if (text.Contains(" ", StringComparison.Ordinal) || text.Contains("-", StringComparison.Ordinal))
            return true;

        return char.IsLetter(text[0]) || char.IsLetter(text[^1]);
    }
}
