using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SettingsService(
    ISettingsRepository r,
    IConfiguration configuration,
    IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
    IWhatsAppConnectionSettingsStore whatsappSettingsStore) : ISettingsService
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

    public async Task<WhatsAppConnectionSettingsDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c)
    {
        var runtime = await whatsappSettingsStore.GetRuntimeSettingsAsync(c);
        var payload = new WhatsAppConnectionSettingsDto(
            Enabled: runtime.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion,
            PhoneNumberId: runtime.PhoneNumberId ?? string.Empty,
            TimeoutSeconds: runtime.TimeoutSeconds <= 0 ? 30 : runtime.TimeoutSeconds,
            IsConfigured: runtime.Enabled && !string.IsNullOrWhiteSpace(runtime.PhoneNumberId) && !string.IsNullOrWhiteSpace(runtime.AccessToken),
            TokenConfigured: !string.IsNullOrWhiteSpace(runtime.AccessToken),
            Message: BuildWhatsAppStatusMessage(runtime.Enabled, runtime.PhoneNumberId, runtime.AccessToken));

        return payload;
    }

    public async Task<WhatsAppConnectionSettingsDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsDto config, CancellationToken c)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var request = new WhatsAppConnectionSettingsUpdateDto(
            Enabled: config.Enabled,
            PhoneNumberId: config.PhoneNumberId,
            AccessToken: config.AccessToken,
            VerifyToken: config.VerifyToken,
            AppSecret: config.AppSecret,
            BaseUrl: config.BaseUrl,
            ApiVersion: config.ApiVersion,
            TimeoutSeconds: config.TimeoutSeconds);

        await whatsappSettingsStore.UpdateAsync(request, c);
        return await GetWhatsAppConnectionSettingsAsync(c);
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

    private static string BuildWhatsAppStatusMessage(bool enabled, string? phoneNumberId, string? accessToken)
    {
        if (!enabled)
        {
            return "تم إيقاف مزود واتساب مؤقتاً، ولا يتم إرسال الرسائل حتى يتم تفعيله.";
        }

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return "تم التحقق من اكتمال إعدادات الاتصال، ولم يتم إدخال رمز الوصول النهائي بعد.";
        }

        return "تم التحقق من اكتمال إعدادات الاتصال.";
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
}