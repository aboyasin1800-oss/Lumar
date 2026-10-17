using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class WhatsAppConnectionSettingsStore(
    IConfiguration configuration,
    IOptions<WhatsAppOptions> options,
    IDataProtectionProvider protectionProvider) : IWhatsAppConnectionSettingsStore
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("LUMAR_ERP_API_V2:WhatsAppConnectionSecrets:v1");

    public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var settings = GetCurrentSettings();
        var tokenConfigured = !string.IsNullOrWhiteSpace(settings.AccessToken);
        var phoneConfigured = !string.IsNullOrWhiteSpace(settings.PhoneNumberId);
        var connectionConfigured = settings.Enabled && tokenConfigured && phoneConfigured;

        return Task.FromResult(new WhatsAppConnectionStatusDto(
            Enabled: settings.Enabled,
            PhoneNumberId: settings.PhoneNumberId,
            TokenConfigured: tokenConfigured,
            ConnectionConfigured: connectionConfigured,
            Message: connectionConfigured ? "تمت تهيئة اتصال واتساب بشكل كامل." : "اتصال واتساب غير مكتمل. يلزم رقم الهاتف ورمز الوصول."));
    }

    public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = GetCurrentSettings();
        return Task.FromResult(new WhatsAppRuntimeSettings(
            Enabled: settings.Enabled,
            BaseUrl: settings.BaseUrl,
            ApiVersion: settings.ApiVersion,
            PhoneNumberId: settings.PhoneNumberId,
            AccessToken: settings.AccessToken,
            VerifyToken: settings.VerifyToken,
            AppSecret: settings.AppSecret,
            TimeoutSeconds: settings.TimeoutSeconds));
    }

    public async Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var current = GetCurrentSettings();
        var section = configuration.GetSection(WhatsAppOptions.SectionName);

        section["Enabled"] = request.Enabled.ToString();
        section["BaseUrl"] = string.IsNullOrWhiteSpace(request.BaseUrl) ? current.BaseUrl : request.BaseUrl.Trim();
        section["ApiVersion"] = string.IsNullOrWhiteSpace(request.ApiVersion) ? current.ApiVersion : request.ApiVersion.Trim();
        section["PhoneNumberId"] = string.IsNullOrWhiteSpace(request.PhoneNumberId) ? current.PhoneNumberId : request.PhoneNumberId.Trim();
        section["TimeoutSeconds"] = request.TimeoutSeconds <= 0 ? current.TimeoutSeconds.ToString() : request.TimeoutSeconds.ToString();

        await StoreSecretAsync("AccessToken", request.AccessToken, current.AccessToken);
        await StoreSecretAsync("VerifyToken", request.VerifyToken, current.VerifyToken);
        await StoreSecretAsync("AppSecret", request.AppSecret, current.AppSecret);

        return await GetStatusAsync(cancellationToken);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = GetCurrentSettings();
        return Task.FromResult(settings.Enabled && !string.IsNullOrWhiteSpace(settings.PhoneNumberId) && !string.IsNullOrWhiteSpace(settings.AccessToken));
    }

    private WhatsAppOptions GetCurrentSettings()
    {
        var section = configuration.GetSection(WhatsAppOptions.SectionName);
        var baseUrl = section["BaseUrl"] ?? options.Value.BaseUrl;
        var apiVersion = section["ApiVersion"] ?? options.Value.ApiVersion;
        var phoneNumberId = section["PhoneNumberId"] ?? options.Value.PhoneNumberId;
        var timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : options.Value.TimeoutSeconds;

        var accessToken = ReadProtectedSecret("AccessToken", section["AccessToken"] ?? options.Value.AccessToken);
        var verifyToken = ReadProtectedSecret("VerifyToken", section["VerifyToken"] ?? options.Value.VerifyToken);
        var appSecret = ReadProtectedSecret("AppSecret", section["AppSecret"] ?? options.Value.AppSecret);

        return new WhatsAppOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            BaseUrl = baseUrl,
            ApiVersion = apiVersion,
            PhoneNumberId = phoneNumberId,
            AccessToken = accessToken,
            VerifyToken = verifyToken,
            AppSecret = appSecret,
            TimeoutSeconds = timeoutSeconds,
        };
    }

    private async Task StoreSecretAsync(string key, string? incomingValue, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return;
        }

        var secret = incomingValue.Trim();
        var secrets = await LoadSecretsAsync();
        secrets[key] = _protector.Protect(secret);
        await SaveSecretsAsync(secrets);
    }

    private string ReadProtectedSecret(string key, string fallbackValue)
    {
        if (!TryLoadSecrets(out var secrets))
        {
            return string.Empty;
        }

        if (!secrets.TryGetValue(key, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        try
        {
            var unprotected = _protector.Unprotect(protectedValue);
            return string.IsNullOrWhiteSpace(unprotected) ? string.Empty : unprotected;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetSecretStorePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDirectory, "LUMAR_ERP_API_V2", "WhatsAppSecrets", "settings.json");
    }

    private static async Task<Dictionary<string, string>> LoadSecretsAsync()
    {
        var path = GetSecretStorePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var model = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return model ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryLoadSecrets(out Dictionary<string, string> secrets)
    {
        try
        {
            var path = GetSecretStorePath();
            if (!File.Exists(path))
            {
                secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return false;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return false;
            }

            var model = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            secrets = model ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return secrets.Count > 0;
        }
        catch
        {
            secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return false;
        }
    }

    private static async Task SaveSecretsAsync(Dictionary<string, string> secrets)
    {
        var path = GetSecretStorePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(path, json);
    }
}
