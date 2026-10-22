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
    private static readonly YemenPhoneNumberNormalizer Normalizer = new();

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

        var payload = CreateOutboundPayload(message, destination);

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

    private static object CreateOutboundPayload(CustomerMessageDto message, string destination)
    {
        var templateDefinition = MetaTemplateCatalog.Resolve(message.TemplateKey, message.EventType);
        if (templateDefinition is not null)
        {
            var components = new List<object>();
            if (templateDefinition.RequiresHeader)
            {
                var headerComponent = MetaTemplateCatalog.TryCreateHeaderComponent(message, templateDefinition);
                if (headerComponent is not null)
                {
                    components.Add(headerComponent);
                }
            }

            if (!string.IsNullOrWhiteSpace(message.Body))
            {
                components.Add(new
                {
                    type = "body",
                    parameters = new[]
                    {
                        new { type = "text", text = message.Body }
                    }
                });
            }

            return new
            {
                messaging_product = "whatsapp",
                to = destination,
                type = "template",
                template = new
                {
                    name = templateDefinition.MetaName,
                    language = new { code = templateDefinition.LanguageCode },
                    components = components.ToArray()
                }
            };
        }

        return new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "text",
            text = new { body = message.Body ?? string.Empty }
        };
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
        => Normalizer.TryNormalizeMobileNumber(phoneNumber);

    public sealed record MetaTemplateDefinition(string MetaName, string LanguageCode, bool RequiresHeader)
    {
        public static object? TryCreateHeaderComponent(CustomerMessageDto message, MetaTemplateDefinition definition)
        {
            if (!definition.RequiresHeader)
                return null;

            var imageUrl = TryResolveHeaderImage(message);
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                return new
                {
                    type = "header",
                    parameters = new[]
                    {
                        new { type = "image", image = new { link = imageUrl } }
                    }
                };
            }

            if (!string.IsNullOrWhiteSpace(message.Title))
            {
                return new
                {
                    type = "header",
                    parameters = new[]
                    {
                        new { type = "text", text = message.Title }
                    }
                };
            }

            return null;
        }

        private static string? TryResolveHeaderImage(CustomerMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message.ReferenceId))
                return null;

            if (!Uri.TryCreate(message.ReferenceId, UriKind.Absolute, out var uri))
                return null;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
        }
    }

    public static class MetaTemplateCatalog
    {
        private static readonly IReadOnlyDictionary<string, MetaTemplateDefinition> Templates =
            new Dictionary<string, MetaTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["lumar_order_created"] = new("lumar_order_created", "ar", false),
                ["lumar_order_ready"] = new("lumar_order_ready", "ar", false),
                ["lumar_order_delivered"] = new("lumar_order_delivered", "ar", false),
                ["lumar_piece_cancelled"] = new("lumar_piece_cancelled", "ar", false),
                ["lumar_order_cancelled"] = new("lumar_order_cancelled", "ar", false),
                ["lumar_referrer_reward"] = new("lumar_referrer_reward", "ar", false),
                ["lumar_referrer_reward_relation"] = new("lumar_referrer_reward_relation", "ar", false),
                ["lumar_order_created_with_referrer"] = new("lumar_order_created_with_referrer", "ar", false),
                ["lumar_order_created_with_referrer_relation"] = new("lumar_order_created_with_referrer_relation", "ar", false),
                ["lumar_friday_greeting"] = new("lumar_friday_greeting", "ar", false),
                ["lumar_general_campaign"] = new("lumar_general_campaign", "ar", true),

                ["ordercreated"] = new("lumar_order_created", "ar", false),
                ["order_created"] = new("lumar_order_created", "ar", false),
                ["orderready"] = new("lumar_order_ready", "ar", false),
                ["order_ready"] = new("lumar_order_ready", "ar", false),
                ["orderdelivered"] = new("lumar_order_delivered", "ar", false),
                ["order_delivered"] = new("lumar_order_delivered", "ar", false),
                ["piececancelled"] = new("lumar_piece_cancelled", "ar", false),
                ["piece_cancelled"] = new("lumar_piece_cancelled", "ar", false),
                ["ordercancelled"] = new("lumar_order_cancelled", "ar", false),
                ["order_cancelled"] = new("lumar_order_cancelled", "ar", false),
                ["referrerreward"] = new("lumar_referrer_reward", "ar", false),
                ["referrer_reward"] = new("lumar_referrer_reward", "ar", false),
                ["referrerrewardrelation"] = new("lumar_referrer_reward_relation", "ar", false),
                ["referrer_reward_relation"] = new("lumar_referrer_reward_relation", "ar", false),
                ["ordercreatedwithreferrer"] = new("lumar_order_created_with_referrer", "ar", false),
                ["order_created_with_referrer"] = new("lumar_order_created_with_referrer", "ar", false),
                ["withreferrer"] = new("lumar_order_created_with_referrer", "ar", false),
                ["ordercreatedwithreferrerrelation"] = new("lumar_order_created_with_referrer_relation", "ar", false),
                ["order_created_with_referrer_relation"] = new("lumar_order_created_with_referrer_relation", "ar", false),
                ["withreferrerrelation"] = new("lumar_order_created_with_referrer_relation", "ar", false),
                ["fridaygreeting"] = new("lumar_friday_greeting", "ar", false),
                ["friday_greeting"] = new("lumar_friday_greeting", "ar", false),
                ["generalcampaign"] = new("lumar_general_campaign", "ar", true),
                ["general_campaign"] = new("lumar_general_campaign", "ar", true),
            };

        public static MetaTemplateDefinition? Resolve(string? templateKey, string? eventType = null)
        {
            var candidates = new[]
            {
                templateKey,
                eventType
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (Templates.TryGetValue(candidate, out var definition))
                    return definition;

                var normalized = candidate.Trim();
                if (normalized.Contains("OrderCreated", StringComparison.OrdinalIgnoreCase))
                {
                    if (normalized.Contains("WithReferrer", StringComparison.OrdinalIgnoreCase) || normalized.Contains("referrer", StringComparison.OrdinalIgnoreCase))
                        return Templates["lumar_order_created_with_referrer"];
                    return Templates["lumar_order_created"];
                }
                if (normalized.Contains("OrderReady", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_order_ready"];
                if (normalized.Contains("OrderDelivered", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_order_delivered"];
                if (normalized.Contains("PieceCancelled", StringComparison.OrdinalIgnoreCase) || normalized.Contains("PieceCanceled", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_piece_cancelled"];
                if (normalized.Contains("OrderCancelled", StringComparison.OrdinalIgnoreCase) || normalized.Contains("OrderCanceled", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_order_cancelled"];
                if (normalized.Contains("ReferrerReward", StringComparison.OrdinalIgnoreCase) || normalized.Contains("referrer", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_referrer_reward"];
                if (normalized.Contains("Friday", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_friday_greeting"];
                if (normalized.Contains("Campaign", StringComparison.OrdinalIgnoreCase))
                    return Templates["lumar_general_campaign"];
            }

            return null;
        }

        public static object? TryCreateHeaderComponent(CustomerMessageDto message, MetaTemplateDefinition definition)
            => MetaTemplateDefinition.TryCreateHeaderComponent(message, definition);
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

                    var reason = string.Join(" | ", parts);
                    if (reason.Length > 500)
                    {
                        reason = reason[..497] + "...";
                    }

                    return new OutboundAdapterResult(
                        statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429
                            ? OutboundDispatchOutcome.TransientFailure
                            : OutboundDispatchOutcome.PermanentFailure,
                        FailureReason: reason,
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

        var fallbackReason = safeReason + " | Meta rejected the request.";
        if (fallbackReason.Length > 500)
        {
            fallbackReason = fallbackReason[..497] + "...";
        }

        return new OutboundAdapterResult(
            statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.RequestTimeout || statusCode == (HttpStatusCode)429
                ? OutboundDispatchOutcome.TransientFailure
                : OutboundDispatchOutcome.PermanentFailure,
            FailureReason: fallbackReason,
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
        var sensitivePatterns = new[]
        {
            @"(?i)\bAccessToken\s*(?:=|:)\s*[A-Za-z0-9._:-]+",
            @"(?i)\bAppSecret\s*(?:=|:)\s*[A-Za-z0-9._:-]+",
            @"(?i)\bVerifyToken\s*(?:=|:)\s*[A-Za-z0-9._:-]+",
            @"(?i)\bAuthorization\s*:\s*Bearer\s+[A-Za-z0-9._:-]+",
            @"(?i)\bBearer\s+EAA[0-9A-Za-z]+",
            @"(?i)\bBearer\s+[A-Za-z0-9._:-]+",
            @"(?i)\btoken\s*(?:=|:)\s*[A-Za-z0-9._:-]+",
            @"(?i)\bsecret\s*(?:=|:)\s*[A-Za-z0-9._:-]+",
            @"(?i)\bAccessToken\b",
            @"(?i)\bAppSecret\b",
            @"(?i)\bVerifyToken\b",
            @"(?i)\bAuthorization\b",
            @"(?i)\bBearer\b",
            @"(?i)\bEAA[0-9A-Za-z]+\b",
            @"(?i)\btoken\b",
            @"(?i)\bsecret\b"
        };

        foreach (var pattern in sensitivePatterns)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                pattern,
                "[redacted]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        return sanitized.Length > 220 ? sanitized[..217] + "..." : sanitized;
    }
}
