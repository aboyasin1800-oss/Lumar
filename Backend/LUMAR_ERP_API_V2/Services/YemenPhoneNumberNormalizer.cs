using System.Globalization;
using System.Text.RegularExpressions;
using PhoneNumbers;

namespace LUMAR_ERP_API_V2.Services;

public interface IPhoneNumberNormalizer
{
    string? TryNormalizeMobileNumber(string? phoneNumber);
}

public interface IYemenPhoneNumberNormalizer : IPhoneNumberNormalizer
{
}

public sealed class YemenPhoneNumberNormalizer : IYemenPhoneNumberNormalizer, IPhoneNumberNormalizer
{
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    private static readonly HashSet<string> SupportedCountryCodes =
    ["967", "966", "971", "968", "974", "965", "973"];

    public string? TryNormalizeMobileNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var trimmed = phoneNumber.Trim();
        if (trimmed.Any(char.IsLetter) || Regex.IsMatch(trimmed, @"[^0-9+()\s-]"))
            return null;

        var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digitsOnly.StartsWith("00", StringComparison.Ordinal))
            digitsOnly = digitsOnly[2..];

        // Preserve the exact existing Yemen compatibility contract before international parsing.
        if (digitsOnly.StartsWith("967", StringComparison.Ordinal))
        {
            var local = digitsOnly[3..];
            if (local.Length == 9 && local.StartsWith("7", StringComparison.Ordinal))
                return $"967{local}";
            return null;
        }

        if (digitsOnly.Length == 9 && digitsOnly.StartsWith("7", StringComparison.Ordinal))
            return $"967{digitsOnly}";

        var countryCode = SupportedCountryCodes
            .Where(digitsOnly.StartsWith)
            .OrderByDescending(code => code.Length)
            .FirstOrDefault();
        if (countryCode is null)
            return null;

        try
        {
            var parsed = PhoneUtil.Parse("+" + digitsOnly, countryCode);
            if (!PhoneUtil.IsValidNumber(parsed)
                || parsed.CountryCode.ToString(CultureInfo.InvariantCulture) != countryCode)
                return null;

            var numberType = PhoneUtil.GetNumberType(parsed);
            if (numberType is not PhoneNumberType.MOBILE
                and not PhoneNumberType.FIXED_LINE_OR_MOBILE)
                return null;

            var e164 = PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
            return e164.StartsWith("+", StringComparison.Ordinal) ? e164[1..] : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
