using System.Globalization;

namespace LUMAR_ERP_API_V2.Services;

public interface IMessageTemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, object?> values);
}

public sealed class MessageTemplateRenderer : IMessageTemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, object?> values)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var result = template;
        foreach (var pair in values)
        {
            var key = pair.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = FormatValue(pair.Value);
            var doubleBraceToken = $"{{{{{key}}}}}";
            var singleBraceToken = $"{{{key}}}";

            result = result.Replace(doubleBraceToken, value, StringComparison.OrdinalIgnoreCase);
            result = result.Replace(singleBraceToken, value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is DateOnly dateOnly)
            return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is DateTime date)
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is DateTimeOffset offset)
            return offset.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is decimal d)
            return d.ToString(CultureInfo.InvariantCulture);

        if (value is double doubleValue)
            return doubleValue.ToString(CultureInfo.InvariantCulture);

        if (value is float floatValue)
            return floatValue.ToString(CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
