using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class WhatsAppConnectionSettingsStore(
    IConfiguration configuration,
    IOptions<WhatsAppOptions> options) : IWhatsAppConnectionSettingsStore
{
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
            HasSecretAccessToken: tokenConfigured,
            AccessTokenMasked: tokenConfigured ? MaskToken(settings.AccessToken) : null,
            Message: connectionConfigured ? "تمت تهيئة اتصال واتساب بشكل كامل." : "اتصال واتساب غير مكتمل. يلزم رقم الهاتف ورمز الوصول."));
    }

    public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var section = configuration.GetSection(WhatsAppOptions.SectionName);

        section["Enabled"] = request.Enabled.ToString();
        section["BaseUrl"] = string.IsNullOrWhiteSpace(request.BaseUrl) ? options.Value.BaseUrl : request.BaseUrl.Trim();
        section["ApiVersion"] = string.IsNullOrWhiteSpace(request.ApiVersion) ? options.Value.ApiVersion : request.ApiVersion.Trim();
        section["PhoneNumberId"] = request.PhoneNumberId ?? options.Value.PhoneNumberId;
        section["AccessToken"] = string.IsNullOrWhiteSpace(request.AccessToken) ? options.Value.AccessToken : request.AccessToken.Trim();
        section["VerifyToken"] = string.IsNullOrWhiteSpace(request.VerifyToken) ? options.Value.VerifyToken : request.VerifyToken.Trim();
        section["AppSecret"] = string.IsNullOrWhiteSpace(request.AppSecret) ? options.Value.AppSecret : request.AppSecret.Trim();
        section["TimeoutSeconds"] = request.TimeoutSeconds <= 0 ? options.Value.TimeoutSeconds.ToString() : request.TimeoutSeconds.ToString();

        return GetStatusAsync(cancellationToken);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = GetCurrentSettings();
        return Task.FromResult(settings.Enabled && !string.IsNullOrWhiteSpace(settings.PhoneNumberId) && !string.IsNullOrWhiteSpace(settings.AccessToken));
    }

    private WhatsAppOptions GetCurrentSettings()
    {
        var section = configuration.GetSection(WhatsAppOptions.SectionName);
        return new WhatsAppOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            BaseUrl = section["BaseUrl"] ?? options.Value.BaseUrl,
            ApiVersion = section["ApiVersion"] ?? options.Value.ApiVersion,
            PhoneNumberId = section["PhoneNumberId"] ?? options.Value.PhoneNumberId,
            AccessToken = section["AccessToken"] ?? options.Value.AccessToken,
            VerifyToken = section["VerifyToken"] ?? options.Value.VerifyToken,
            AppSecret = section["AppSecret"] ?? options.Value.AppSecret,
            TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : options.Value.TimeoutSeconds,
        };
    }

    private static string MaskToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var token = value.Trim();
        if (token.Length <= 4)
        {
            return new string('*', token.Length);
        }

        return $"{token[..2]}********{token[^2..]}";
    }
}
