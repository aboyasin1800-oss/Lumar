using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SettingsService(
    ISettingsRepository r,
    IConfiguration configuration,
    IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor) : ISettingsService
{
    private static readonly IReadOnlyDictionary<string, string> SupportedCurrencies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["YER"] = "الريال اليمني",
            ["SAR"] = "الريال السعودي",
            ["USD"] = "الدولار الأمريكي",
            ["EGP"] = "الجنيه المصري",
            ["QAR"] = "الريال القطري",
            ["OMR"] = "الريال العماني",
            ["AED"] = "الدرهم الإماراتي",
            ["KWD"] = "الدينار الكويتي",
            ["IQD"] = "الدينار العراقي",
            ["SDG"] = "الجنيه السوداني",
        };

    public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken c) => r.GetAllAsync(c);

    public Task<SystemSettingDetailsDto?> GetByIdAsync(int i, CancellationToken c) => r.GetByIdAsync(i, c);

    public Task<SystemSettingDetailsDto?> GetByKeyAsync(string k, CancellationToken c) => r.GetByKeyAsync(k, c);

    public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken c) => r.GetCategoriesAsync(c);

    public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken c) => r.GetByCategoryAsync(category, c);

    public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) => r.GetProductionRoutesConfigAsync(c);

    public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) => r.GetProductionRoutePieceTypesAsync(c);

    public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) => r.UpsertProductionRoutesConfigAsync(config, c);

    public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) => r.GetMessageSettingsAsync(c);

    public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c) => r.UpdateMessageSettingsAsync(config, c);

    public Task<WhatsAppConnectionSettingsDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c)
    {
        var options = whatsappOptionsMonitor.CurrentValue;
        if (options is null)
        {
            options = new WhatsAppOptions();
        }

        var payload = new WhatsAppConnectionSettingsDto(
            Enabled: options.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(options.ApiVersion) ? "v20.0" : options.ApiVersion,
            PhoneNumberId: options.PhoneNumberId ?? string.Empty,
            AccessToken: null,
            VerifyToken: null,
            AppSecret: null,
            TimeoutSeconds: options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds,
            IsConfigured: HasValidProviderSetup(options),
            AccessTokenMasked: MaskSecret(options.AccessToken),
            VerifyTokenMasked: MaskSecret(options.VerifyToken),
            AppSecretMasked: MaskSecret(options.AppSecret),
            Message: BuildWhatsAppStatusMessage(options));

        return Task.FromResult(payload);
    }

    public Task<WhatsAppConnectionSettingsDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsDto config, CancellationToken c)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var section = configuration.GetSection(WhatsAppOptions.SectionName);
        var existing = whatsappOptionsMonitor.CurrentValue ?? new WhatsAppOptions();

        var next = new WhatsAppOptions
        {
            Enabled = config.Enabled,
            BaseUrl = NormalizeText(config.BaseUrl, existing.BaseUrl, "https://graph.facebook.com"),
            ApiVersion = NormalizeText(config.ApiVersion, existing.ApiVersion, "v20.0"),
            PhoneNumberId = NormalizeOptionalText(config.PhoneNumberId, existing.PhoneNumberId),
            AccessToken = NormalizeOptionalSecret(config.AccessToken, existing.AccessToken),
            VerifyToken = NormalizeOptionalSecret(config.VerifyToken, existing.VerifyToken),
            AppSecret = NormalizeOptionalSecret(config.AppSecret, existing.AppSecret),
            TimeoutSeconds = config.TimeoutSeconds <= 0 ? 30 : config.TimeoutSeconds,
        };

        section["Enabled"] = next.Enabled.ToString();
        section["BaseUrl"] = next.BaseUrl;
        section["ApiVersion"] = next.ApiVersion;
        section["PhoneNumberId"] = next.PhoneNumberId;
        section["AccessToken"] = next.AccessToken;
        section["VerifyToken"] = next.VerifyToken;
        section["AppSecret"] = next.AppSecret;
        section["TimeoutSeconds"] = next.TimeoutSeconds.ToString();

        if (configuration is IConfigurationRoot root)
        {
            root.Reload();
        }

        return GetWhatsAppConnectionSettingsAsync(c);
    }

    public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c)
    {
        var code = preferredCurrency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.TryGetValue(code, out var supportedName))
        {
            throw new ArgumentException("العملة المحددة غير مدعومة للعرض.");
        }

        var name = currencyName.Trim();
        if (!string.Equals(name, supportedName, StringComparison.Ordinal) && !(code == "YER" && name == "ريال"))
        {
            throw new ArgumentException("اسم العملة لا يطابق الكود المحدد.");
        }

        return r.UpdateCurrencySettingsAsync(currencyName, name, c);
    }

    public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => r.UpdatePrintSettingsAsync(values, c);

    public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) => r.GetGeneralCashAccountSettingAsync(c);

    public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) => r.UpsertGeneralCashAccountSettingAsync(cashAccountId, c);

    private static bool HasValidProviderSetup(WhatsAppOptions options)
        => options.Enabled && !string.IsNullOrWhiteSpace(options.PhoneNumberId) && !string.IsNullOrWhiteSpace(options.AccessToken);

    private static string BuildWhatsAppStatusMessage(WhatsAppOptions options)
    {
        if (!options.Enabled)
        {
            return "تم إيقاف مزود واتساب مؤقتاً، ولا يتم إرسال الرسائل حتى يتم تفعيله.";
        }

        if (string.IsNullOrWhiteSpace(options.PhoneNumberId) || string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return "لم يتم إكمال إعداد مزود واتساب، ويحتاج إلى رقم الهاتف ورمز الوصول قبل إرسال الرسائل.";
        }

        return "إعداد مزود واتساب مكتمل ويمكن استخدامه لإرسال الرسائل.";
    }

    private static string NormalizeText(string? incoming, string? current, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return string.IsNullOrWhiteSpace(current) ? defaultValue : current.Trim();
        }

        return incoming.Trim();
    }

    private static string NormalizeOptionalText(string? incoming, string? current)
    {
        if (incoming is null)
        {
            return current ?? string.Empty;
        }

        return incoming.Trim();
    }

    private static string NormalizeOptionalSecret(string? incoming, string? current)
    {
        if (incoming is null)
        {
            return current ?? string.Empty;
        }

        var value = incoming.Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return value;
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..2]}********{trimmed[^2..]}";
    }
}