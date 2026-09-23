namespace LUMAR_ERP_API_V2.Services;

public static class PrintSettingsValidator
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "MeasurementCardHeaderImage",
        "MeasurementCardHeaderUseImage",
        "MeasurementCardHeaderName",
        "MeasurementCardLocation",
        "MeasurementCardPhone1",
        "MeasurementCardPhone2",
    };

    public static Dictionary<string, string> NormalizeAndValidate(IReadOnlyDictionary<string, string> input)
    {
        if (input is null || input.Count == 0)
        {
            throw new ArgumentException("يجب إرسال إعدادات الطباعة المطلوبة.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in input)
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("لا يمكن أن يكون اسم المفتاح فارغاً.");
            }

            if (!AllowedKeys.Contains(key))
            {
                throw new ArgumentException($"المفتاح غير مسموح: {key}");
            }

            var value = pair.Value ?? string.Empty;
            if (key.Equals("MeasurementCardHeaderUseImage", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = value.Trim();
                if (!string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("MeasurementCardHeaderUseImage يجب أن يكون true أو false.");
                }

                result[key] = string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
                    ? "true"
                    : "false";
                continue;
            }

            result[key] = value.Trim();
        }

        return result;
    }
}
