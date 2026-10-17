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
        var persisted = LoadPersistentState();

        persisted.Enabled = request.Enabled;
        persisted.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? current.BaseUrl : request.BaseUrl.Trim();
        persisted.ApiVersion = string.IsNullOrWhiteSpace(request.ApiVersion) ? current.ApiVersion : request.ApiVersion.Trim();
        persisted.PhoneNumberId = string.IsNullOrWhiteSpace(request.PhoneNumberId) ? current.PhoneNumberId : request.PhoneNumberId.Trim();
        persisted.TimeoutSeconds = request.TimeoutSeconds <= 0 ? current.TimeoutSeconds : request.TimeoutSeconds;

        StoreSecretIfProvided(persisted.Secrets, "AccessToken", request.AccessToken);
        StoreSecretIfProvided(persisted.Secrets, "VerifyToken", request.VerifyToken);
        StoreSecretIfProvided(persisted.Secrets, "AppSecret", request.AppSecret);

        SavePersistentState(persisted);

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
        var persistent = LoadPersistentState();

        var sectionEnabled = bool.TryParse(section["Enabled"], out var parsedSectionEnabled) ? parsedSectionEnabled : (bool?)null;
        var enabled = persistent.Enabled ?? sectionEnabled ?? options.Value.Enabled;

        var baseUrl = !string.IsNullOrWhiteSpace(persistent.BaseUrl)
            ? persistent.BaseUrl
            : section["BaseUrl"] ?? options.Value.BaseUrl;
        var apiVersion = !string.IsNullOrWhiteSpace(persistent.ApiVersion)
            ? persistent.ApiVersion
            : section["ApiVersion"] ?? options.Value.ApiVersion;
        var phoneNumberId = !string.IsNullOrWhiteSpace(persistent.PhoneNumberId)
            ? persistent.PhoneNumberId
            : section["PhoneNumberId"] ?? options.Value.PhoneNumberId;
        var timeoutSeconds = persistent.TimeoutSeconds > 0
            ? persistent.TimeoutSeconds
            : (int.TryParse(section["TimeoutSeconds"], out var sectionTimeout) && sectionTimeout > 0 ? sectionTimeout : options.Value.TimeoutSeconds);

        var accessToken = ReadProtectedSecret(persistent.Secrets, "AccessToken", section["AccessToken"] ?? options.Value.AccessToken);
        var verifyToken = ReadProtectedSecret(persistent.Secrets, "VerifyToken", section["VerifyToken"] ?? options.Value.VerifyToken);
        var appSecret = ReadProtectedSecret(persistent.Secrets, "AppSecret", section["AppSecret"] ?? options.Value.AppSecret);

        return new WhatsAppOptions
        {
            Enabled = enabled,
            BaseUrl = baseUrl,
            ApiVersion = apiVersion,
            PhoneNumberId = phoneNumberId,
            AccessToken = accessToken,
            VerifyToken = verifyToken,
            AppSecret = appSecret,
            TimeoutSeconds = timeoutSeconds,
        };
    }

    private string ReadProtectedSecret(Dictionary<string, string> secrets, string key, string fallbackValue)
    {
        if (secrets.TryGetValue(key, out var protectedValue) && !string.IsNullOrWhiteSpace(protectedValue))
        {
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

        return string.IsNullOrWhiteSpace(fallbackValue) ? string.Empty : fallbackValue;
    }

    private void StoreSecretIfProvided(Dictionary<string, string> secrets, string key, string? incomingValue)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return;
        }

        secrets[key] = _protector.Protect(incomingValue.Trim());
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

    private static PersistentWhatsAppState LoadPersistentState()
    {
        var path = GetSecretStorePath();
        if (!File.Exists(path))
        {
            return new PersistentWhatsAppState();
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PersistentWhatsAppState();
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new PersistentWhatsAppState();
            }

            var state = new PersistentWhatsAppState();

            if (root.TryGetProperty("Enabled", out var enabledElement))
            {
                if (enabledElement.ValueKind == JsonValueKind.True)
                {
                    state.Enabled = true;
                }
                else if (enabledElement.ValueKind == JsonValueKind.False)
                {
                    state.Enabled = false;
                }
                else if (enabledElement.ValueKind == JsonValueKind.Null)
                {
                    state.Enabled = null;
                }
            }

            if (root.TryGetProperty("BaseUrl", out var baseUrlElement))
            {
                state.BaseUrl = baseUrlElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("ApiVersion", out var apiVersionElement))
            {
                state.ApiVersion = apiVersionElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("PhoneNumberId", out var phoneElement))
            {
                state.PhoneNumberId = phoneElement.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("TimeoutSeconds", out var timeoutElement) && timeoutElement.TryGetInt32(out var timeoutValue))
            {
                state.TimeoutSeconds = timeoutValue > 0 ? timeoutValue : 30;
            }

            var secretsElement = root.TryGetProperty("Secrets", out var secretsNode) ? secretsNode : root;
            if (secretsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in secretsElement.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(property.Name))
                    {
                        var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            state.Secrets[property.Name] = value;
                        }
                    }
                }
            }

            return state;
        }
        catch
        {
            return new PersistentWhatsAppState();
        }
    }

    private static void SavePersistentState(PersistentWhatsAppState state)
    {
        var path = GetSecretStorePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            Enabled = state.Enabled,
            BaseUrl = state.BaseUrl,
            ApiVersion = state.ApiVersion,
            PhoneNumberId = state.PhoneNumberId,
            TimeoutSeconds = state.TimeoutSeconds,
            Secrets = state.Secrets
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(path, json);
    }

    private sealed class PersistentWhatsAppState
    {
        public bool? Enabled { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
