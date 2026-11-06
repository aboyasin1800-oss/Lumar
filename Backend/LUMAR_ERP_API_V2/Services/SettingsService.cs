using System.Diagnostics;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SettingsService(
    ISettingsRepository r,
    IConfiguration configuration,
    IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
    IWhatsAppConnectionSettingsStore whatsappSettingsStore,
    ILogger<SettingsService> logger) : ISettingsService
{
    private const string ApprovedDatabaseName = "LUMAR_ERP_ES_VALIDATION_R3";
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

    public async Task<WhatsAppConnectionSettingsResponseDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c)
    {
        var runtime = await whatsappSettingsStore.GetRuntimeSettingsAsync(c);
        var connectionConfigured = runtime.Enabled
            && !string.IsNullOrWhiteSpace(runtime.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(runtime.AccessToken);

        return new WhatsAppConnectionSettingsResponseDto(
            Enabled: runtime.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion,
            PhoneNumberId: runtime.PhoneNumberId ?? string.Empty,
            TimeoutSeconds: runtime.TimeoutSeconds <= 0 ? 30 : runtime.TimeoutSeconds,
            IsConfigured: connectionConfigured,
            TokenConfigured: !string.IsNullOrWhiteSpace(runtime.AccessToken),
            ConnectionConfigured: connectionConfigured,
            Message: BuildWhatsAppStatusMessage(runtime.Enabled, runtime.PhoneNumberId, runtime.AccessToken));
    }

    public async Task<WhatsAppConnectionSettingsResponseDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsUpdateDto config, CancellationToken c)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        await whatsappSettingsStore.UpdateAsync(config, c);
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

    public async Task<TestDataResetExecutionResultDto> ExecuteTestDataResetAsync(CancellationToken c)
    {
        var scriptPath = ResolveResetScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            var failure = new TestDataResetExecutionResultDto(
                false,
                "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                ApprovedDatabaseName,
                "Reset engine script was not found on the backend host.");
            logger.LogError("{Message} {Details}", failure.Message, failure.Details);
            return failure;
        }

        try
        {
            var executeSuccess = await RunResetCommandAsync(scriptPath, "Execute", true, c);
            if (!executeSuccess)
            {
                return new TestDataResetExecutionResultDto(
                    false,
                    "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                    ApprovedDatabaseName,
                    "Execute stage failed. The reset engine rejected the operation or the approved database guard failed.");
            }

            var verifySuccess = await RunResetCommandAsync(scriptPath, "Verify", false, c);
            if (!verifySuccess)
            {
                return new TestDataResetExecutionResultDto(
                    false,
                    "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                    ApprovedDatabaseName,
                    "Verify stage failed after execution. The reset was not accepted as complete.");
            }

            return new TestDataResetExecutionResultDto(
                true,
                "تم تنظيف بيانات الاختبار بنجاح.",
                ApprovedDatabaseName,
                "Execute + Verify completed successfully and the approved baseline guard remained active.",
                Verified: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test data reset failed for approved database {DatabaseName}.", ApprovedDatabaseName);
            return new TestDataResetExecutionResultDto(
                false,
                "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                ApprovedDatabaseName,
                ex.Message);
        }
    }

    private static async Task<bool> RunResetCommandAsync(string scriptPath, string mode, bool allowExecute, CancellationToken cancellationToken)
    {
        var arguments = allowExecute
            ? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Database \"{ApprovedDatabaseName}\" -Mode {mode} -AllowExecute"
            : $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Database \"{ApprovedDatabaseName}\" -Mode {mode}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the approved reset engine.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.IsNullOrWhiteSpace(standardOutput) ? standardError : $"{standardOutput}{Environment.NewLine}{standardError}";
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Reset engine failed in mode '{mode}': {output}");
        }

        return true;
    }

    private static string ResolveResetScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine(Environment.CurrentDirectory, "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine("D:\\YASIN\\Backend", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine("D:", "YASIN", "Backend", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1")
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
            {
                return resolved;
            }
        }

        return string.Empty;
    }

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