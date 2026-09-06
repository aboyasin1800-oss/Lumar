using System.Text.RegularExpressions;

namespace LUMAR_ERP_API_V2.Utilities;

public static partial class FabricCodeAllocator
{
    public static string GetNextAvailableCode(IEnumerable<string?> existingCodes, string prefix = "FA")
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        var max = 0;
        foreach (var raw in existingCodes)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var code = raw.Trim();
            if (!code.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var digits = code.Substring(normalizedPrefix.Length);
            if (!Regex.IsMatch(digits, "^\\d+$")) continue;
            if (int.TryParse(digits, out var value) && value > max)
            {
                max = value;
            }
        }

        return $"{normalizedPrefix}{(max + 1).ToString("D4")}";
    }

    public static IReadOnlyList<string> GetSequentialCodes(int count, IEnumerable<string?> existingCodes, string prefix = "FA")
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        var start = 0;
        foreach (var raw in existingCodes)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var code = raw.Trim();
            if (!code.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var digits = code.Substring(normalizedPrefix.Length);
            if (int.TryParse(digits, out var value) && value > start)
            {
                start = value;
            }
        }

        var result = new List<string>();
        for (var i = 0; i < count; i++)
        {
            result.Add($"{normalizedPrefix}{(start + 1 + i).ToString("D4")}");
        }

        return result;
    }

    private static string NormalizePrefix(string? prefix)
    {
        var value = prefix?.Trim() ?? "FA";
        return string.IsNullOrWhiteSpace(value) ? "FA" : value;
    }
}
