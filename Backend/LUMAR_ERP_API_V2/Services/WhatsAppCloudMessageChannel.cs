using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Messages;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class WhatsAppCloudMessageChannel(
    IHttpClientFactory httpClientFactory,
    IWhatsAppConnectionSettingsStore settingsStore,
    ILogger<WhatsAppCloudMessageChannel>? logger = null) : IOutboundMessageChannel
{
    public string ChannelName => "WhatsApp";

    public async Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken)
    {
        var options = await settingsStore.GetRuntimeSettingsAsync(cancellationToken);

        if (!options.Enabled)
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "WhatsApp provider is disabled in configuration.", Retryable: false);

        if (string.IsNullOrWhiteSpace(options.AccessToken) || string.IsNullOrWhiteSpace(options.PhoneNumberId))
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "WhatsApp provider is not configured.", Retryable: false);

        var destination = NormalizePhoneNumber(message.PhoneNumber);
        if (string.IsNullOrWhiteSpace(destination))
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "Invalid WhatsApp destination.", Retryable: false);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "text",
            text = new { body = message.Body ?? string.Empty }
        };

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl.TrimEnd('/');
        var endpoint = $"{baseUrl}/{options.ApiVersion.Trim('/')}/{options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var client = httpClientFactory.CreateClient("WhatsAppCloud");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var isRetryable = response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == (HttpStatusCode)429;

            if (!response.IsSuccessStatusCode)
            {
                var safeResult = BuildSafeMetaFailure(response.StatusCode, raw, isRetryable);
                logger?.LogWarning(
                    "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} HttpStatusCode={HttpStatusCode} Outcome={Outcome} ProviderErrorCode={ProviderErrorCode} ProviderErrorSubcode={ProviderErrorSubcode} Retryable={Retryable} ProviderMessageIdPresent={ProviderMessageIdPresent} RecipientPresent={RecipientPresent}",
                    message.CustomerMessageId,
                    (int)response.StatusCode,
                    safeResult.Outcome,
                    safeResult.ProviderErrorCode,
                    safeResult.ProviderErrorSubcode,
                    safeResult.Retryable,
                    false,
                    !string.IsNullOrWhiteSpace(message.PhoneNumber));

                return safeResult;
            }

            var doc = JsonDocument.Parse(raw);
            var providerId = doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(providerId))
            {
                var failure = new OutboundAdapterResult(
                    OutboundDispatchOutcome.PermanentFailure,
                    HttpStatusCode: (int)response.StatusCode,
                    FailureReason: $"Meta HTTP {(int)response.StatusCode} | response missing provider message identifier.",
                    Retryable: false);

                logger?.LogWarning(
                    "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} HttpStatusCode={HttpStatusCode} Outcome={Outcome} ProviderMessageIdPresent={ProviderMessageIdPresent} Retryable={Retryable} RecipientPresent={RecipientPresent}",
                    message.CustomerMessageId,
                    (int)response.StatusCode,
                    OutboundDispatchOutcome.PermanentFailure,
                    false,
                    false,
                    !string.IsNullOrWhiteSpace(message.PhoneNumber));

                return failure;
            }

            var success = new OutboundAdapterResult(
                OutboundDispatchOutcome.Sent,
                ProviderMessageId: providerId,
                FailureReason: null,
                Retryable: false,
                HttpStatusCode: (int)response.StatusCode,
                ProviderErrorCode: null,
                ProviderErrorSubcode: null,
                SafeProviderErrorType: null);

            logger?.LogInformation(
                "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} HttpStatusCode={HttpStatusCode} Outcome={Outcome} ProviderMessageIdPresent={ProviderMessageIdPresent} Retryable={Retryable} RecipientPresent={RecipientPresent}",
                message.CustomerMessageId,
                (int)response.StatusCode,
                OutboundDispatchOutcome.Sent,
                true,
                false,
                !string.IsNullOrWhiteSpace(message.PhoneNumber));

            return success;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var failure = new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure, FailureReason: "WhatsApp request timed out.", Retryable: true);
            logger?.LogWarning(
                "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} Outcome={Outcome} Retryable={Retryable} RecipientPresent={RecipientPresent} ExceptionType={ExceptionType}",
                message.CustomerMessageId,
                OutboundDispatchOutcome.TransientFailure,
                true,
                !string.IsNullOrWhiteSpace(message.PhoneNumber),
                nameof(OperationCanceledException));
            return failure;
        }
        catch (HttpRequestException)
        {
            var failure = new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure, FailureReason: "WhatsApp request failed due to network conditions.", Retryable: true);
            logger?.LogWarning(
                "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} Outcome={Outcome} Retryable={Retryable} RecipientPresent={RecipientPresent} ExceptionType={ExceptionType}",
                message.CustomerMessageId,
                OutboundDispatchOutcome.TransientFailure,
                true,
                !string.IsNullOrWhiteSpace(message.PhoneNumber),
                nameof(HttpRequestException));
            return failure;
        }
        catch (Exception ex)
        {
            var failure = new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "Unexpected WhatsApp provider failure.", Retryable: false);
            logger?.LogError(
                "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} Outcome={Outcome} Retryable={Retryable} RecipientPresent={RecipientPresent} ExceptionType={ExceptionType}",
                message.CustomerMessageId,
                OutboundDispatchOutcome.PermanentFailure,
                false,
                !string.IsNullOrWhiteSpace(message.PhoneNumber),
                ex.GetType().Name);
            return failure;
        }
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (cleaned.Length < 8)
            return null;

        if (cleaned.StartsWith("966", StringComparison.Ordinal))
            return "+" + cleaned;

        if (cleaned.Length == 10 && cleaned.StartsWith("5", StringComparison.Ordinal))
            return "+966" + cleaned.TrimStart('0');

        if (cleaned.Length > 10)
            return "+" + cleaned.TrimStart('0');

        return null;
    }

    private static OutboundAdapterResult BuildSafeMetaFailure(HttpStatusCode statusCode, string raw, bool retryable)
    {
        var safeReason = "Meta HTTP " + (int)statusCode;
        var providerErrorCode = default(int?);
        var providerErrorSubcode = default(int?);
        var safeType = default(string?);

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
                    {
                        safeType = typeElement.GetString();
                    }

                    if (error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number)
                    {
                        providerErrorCode = codeElement.TryGetInt32(out var code) ? code : null;
                    }

                    if (error.TryGetProperty("error_subcode", out var subcodeElement) && subcodeElement.ValueKind == JsonValueKind.Number)
                    {
                        providerErrorSubcode = subcodeElement.TryGetInt32(out var subcode) ? subcode : null;
                    }

                    var message = string.Empty;
                    if (error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                    {
                        message = messageElement.GetString() ?? string.Empty;
                    }

                    var sanitized = SanitizeSensitiveMessage(message);
                    var parts = new List<string> { safeReason };
                    if (!string.IsNullOrWhiteSpace(safeType))
                    {
                        parts.Add(safeType);
                    }
                    if (providerErrorCode.HasValue)
                    {
                        parts.Add($"Code {providerErrorCode.Value}");
                    }
                    if (providerErrorSubcode.HasValue)
                    {
                        parts.Add($"Subcode {providerErrorSubcode.Value}");
                    }
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        parts.Add(sanitized);
                    }

                    return new OutboundAdapterResult(
                        statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429
                            ? OutboundDispatchOutcome.TransientFailure
                            : OutboundDispatchOutcome.PermanentFailure,
                        FailureReason: string.Join(" | ", parts),
                        Retryable: retryable,
                        HttpStatusCode: (int)statusCode,
                        ProviderErrorCode: providerErrorCode,
                        ProviderErrorSubcode: providerErrorSubcode,
                        SafeProviderErrorType: safeType);
                }
            }
            catch
            {
                // malformed JSON is intentionally ignored; safe fallback below
            }
        }

        return new OutboundAdapterResult(
            statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429
                ? OutboundDispatchOutcome.TransientFailure
                : OutboundDispatchOutcome.PermanentFailure,
            FailureReason: safeReason + " | Meta rejected the request.",
            Retryable: retryable,
            HttpStatusCode: (int)statusCode,
            ProviderErrorCode: providerErrorCode,
            ProviderErrorSubcode: providerErrorSubcode,
            SafeProviderErrorType: safeType);
    }

    private static string SanitizeSensitiveMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value;
        sanitized = sanitized.Replace("Authorization", "[authorization]", StringComparison.OrdinalIgnoreCase);
        sanitized = sanitized.Replace("Bearer", "[bearer]", StringComparison.OrdinalIgnoreCase);
        sanitized = sanitized.Replace("AccessToken", "[access_token]", StringComparison.OrdinalIgnoreCase);
        sanitized = sanitized.Replace("AppSecret", "[app_secret]", StringComparison.OrdinalIgnoreCase);
        sanitized = sanitized.Replace("VerifyToken", "[verify_token]", StringComparison.OrdinalIgnoreCase);

        var tokens = new[]
        {
            "EAA[0-9A-Za-z]+",
            "EA[0-9A-Za-z]+",
            "token[=:\\s][A-Za-z0-9._-]+",
            "secret[=:\\s][A-Za-z0-9._-]+"
        };

        foreach (var pattern in tokens)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, pattern, "[redacted]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return sanitized.Length > 220 ? sanitized[..217] + "..." : sanitized;
    }
}
