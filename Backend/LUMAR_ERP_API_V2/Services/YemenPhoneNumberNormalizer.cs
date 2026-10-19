namespace LUMAR_ERP_API_V2.Services;

public interface IYemenPhoneNumberNormalizer
{
    string? TryNormalizeMobileNumber(string? phoneNumber);
}

public sealed class YemenPhoneNumberNormalizer : IYemenPhoneNumberNormalizer
{
    public string? TryNormalizeMobileNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digitsOnly))
            return null;

        if (digitsOnly.StartsWith("00", StringComparison.Ordinal))
            digitsOnly = digitsOnly[2..];

        if (digitsOnly.StartsWith("+", StringComparison.Ordinal))
            digitsOnly = digitsOnly.TrimStart('+');

        if (digitsOnly.StartsWith("967", StringComparison.Ordinal))
        {
            var local = digitsOnly[3..];
            if (local.Length == 9 && local.StartsWith("7", StringComparison.Ordinal))
                return $"967{local}";

            return null;
        }

        if (digitsOnly.Length == 9 && digitsOnly.StartsWith("7", StringComparison.Ordinal))
            return $"967{digitsOnly}";

        if (digitsOnly.Length == 10 && digitsOnly.StartsWith("0", StringComparison.Ordinal) && digitsOnly[1..].StartsWith("7", StringComparison.Ordinal))
            return $"967{digitsOnly[1..]}";

        return null;
    }
}
