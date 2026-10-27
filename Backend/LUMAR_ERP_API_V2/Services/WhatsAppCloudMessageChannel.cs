using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Extensions.Options;
using System.Linq;

namespace LUMAR_ERP_API_V2.Services;

public sealed class WhatsAppCloudMessageChannel(
    IHttpClientFactory httpClientFactory,
    IWhatsAppConnectionSettingsStore settingsStore,
    ILogger<WhatsAppCloudMessageChannel>? logger = null,
    IMetaWhatsAppTemplateCatalogClient? templateCatalogClient = null) : IOutboundMessageChannel
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

        var definition = MetaTemplateCatalog.Resolve(message.TemplateKey, message.EventType);
        if (definition is not null && definition.MetaName != "lumar_general_campaign")
        {
            var catalog = templateCatalogClient ?? new MetaWhatsAppTemplateCatalogClient(httpClientFactory, settingsStore);
            try
            {
                var official = await catalog.GetTemplateAsync(definition.MetaName, cancellationToken);
                if (official is null
                    || !string.Equals(official.Language, definition.LanguageCode, StringComparison.Ordinal)
                    || MetaTemplateCatalog.FromOfficial(official) is not { } officialDefinition)
                {
                    return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure,
                        FailureReason: $"Official Meta template contract unavailable: {definition.MetaName}", Retryable: false);
                }

                definition = officialDefinition;
            }
            catch (HttpRequestException exception)
            {
                var retryable = exception.StatusCode is null or HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout
                    || (int)exception.StatusCode.Value >= 500;
                return new OutboundAdapterResult(retryable ? OutboundDispatchOutcome.TransientFailure : OutboundDispatchOutcome.PermanentFailure,
                    FailureReason: "Official Meta template catalog request failed.", Retryable: retryable,
                    HttpStatusCode: exception.StatusCode is null ? null : (int)exception.StatusCode.Value);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure,
                    FailureReason: "Official Meta template catalog request timed out.", Retryable: true);
            }
        }

        var payloadResult = CreateOutboundPayload(message, destination, definition);
        if (payloadResult is null)
        {
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "Missing required WhatsApp template payload data.", Retryable: false);
        }

        var (payload, missingRequired) = payloadResult.Value;
        if (!string.IsNullOrWhiteSpace(missingRequired))
        {
            var failure = new OutboundAdapterResult(
                OutboundDispatchOutcome.PermanentFailure,
                FailureReason: $"Missing Required Meta Parameter: {missingRequired}",
                Retryable: false);

            logger?.LogWarning(
                "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} Outcome={Outcome} Retryable={Retryable} RecipientPresent={RecipientPresent} MissingParameter={MissingParameter}",
                message.CustomerMessageId,
                OutboundDispatchOutcome.PermanentFailure,
                false,
                !string.IsNullOrWhiteSpace(message.PhoneNumber),
                missingRequired);

            return failure;
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl.TrimEnd('/');
        var endpoint = $"{baseUrl}/{options.ApiVersion.Trim('/')}/{options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient("WhatsAppCloud");
        var requestTimeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));

        try
        {
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .WaitAsync(requestTimeout, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            var isRetryable = response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == (HttpStatusCode)429;

            if (!response.IsSuccessStatusCode)
            {
                var safeResult = BuildSafeMetaFailure(response.StatusCode, raw, isRetryable);
                logger?.LogWarning(
                    "OutboundWhatsAppResult Channel=WhatsApp CustomerMessageId={CustomerMessageId} HttpStatusCode={HttpStatusCode} Outcome={Outcome} ProviderErrorCode={ProviderErrorCode} ProviderErrorSubcode={ProviderErrorSubcode} ProviderErrorDetails={ProviderErrorDetails} Retryable={Retryable} ProviderMessageIdPresent={ProviderMessageIdPresent} RecipientPresent={RecipientPresent}",
                    message.CustomerMessageId,
                    (int)response.StatusCode,
                    safeResult.Outcome,
                    safeResult.ProviderErrorCode,
                    safeResult.ProviderErrorSubcode,
                    safeResult.ProviderErrorDetails,
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

    private static (object? Payload, string? MissingRequiredParameter)? CreateOutboundPayload(
        CustomerMessageDto message, string destination, MetaTemplateDefinition? templateDefinition)
    {
        if (templateDefinition is not null)
        {
            var missingRequired = templateDefinition.Parameters
                .Select(parameter => new { parameter.Name, Value = ResolveTemplateParameterValue(message, parameter.Name) })
                .FirstOrDefault(item => item.Value is null || string.IsNullOrWhiteSpace(Convert.ToString(item.Value, CultureInfo.InvariantCulture)));

            if (missingRequired is not null)
            {
                return (null, $"{missingRequired.Name}");
            }

            var components = new List<object>();
            if (templateDefinition.RequiresHeader)
            {
                var headerComponent = MetaTemplateCatalog.TryCreateHeaderComponent(message, templateDefinition);
                if (headerComponent is not null)
                {
                    components.Add(headerComponent);
                }
            }

            var bodyParameters = BuildBodyParameters(message, templateDefinition);
            if (bodyParameters.Count > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = bodyParameters.ToArray()
                });
            }

            return (new
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
            }, null);
        }

        return (new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "text",
            text = new { body = message.Body ?? string.Empty }
        }, null);
    }

    private static List<object> BuildBodyParameters(CustomerMessageDto message, MetaTemplateDefinition definition)
    {
        var output = new List<object>();
        foreach (var parameter in definition.Parameters)
        {
            var value = ResolveTemplateParameterValue(message, parameter.Name);
            if (value is null)
                continue;

            output.Add(new
            {
                type = "text",
                parameter_name = parameter.Name,
                text = value
            });
        }

        return output;
    }

    private static string? ResolveTemplateParameterValue(CustomerMessageDto message, string parameterName)
    {
        var key = parameterName.Trim();
        if (message.Context is not null)
        {
            return TryResolveFromContext(message.Context, key);
        }

        return null;
    }

    private static string? TryResolveFromContext(IReadOnlyDictionary<string, object?> context, string parameterName)
    {
        var aliasMap = parameterName switch
        {
            "customer_name" => new[] { "CustomerName", "CustomerFullName", "Name" },
            "customer_code" => new[] { "CustomerCode", "CustomerIdentifier" },
            "order_number" => new[] { "OrderNumber", "OrderCode", "OrderReference" },
            "order_details" => new[] { "OrderDetails", "OrderItemSummary", "ItemSummary" },
            "total_amount" => new[] { "TotalAmount" },
            "order_total" => new[] { "OrderTotal" },
            "paid_amount" => new[] { "PaidAmount" },
            "remaining_amount" => new[] { "RemainingAmount" },
            "delivery_date" => new[] { "DeliveryDate" },
            "points_earned" or "customer_points_earned" => new[] { "PointsEarned", "CustomerPointsEarned" },
            "points_balance" or "customer_points_balance" => new[] { "PointsBalance", "CustomerPointsBalance" },
            "store_name" => new[] { "StoreName" },
            "piece_name" => new[] { "PieceName" },
            "cancelled_amount" => new[] { "CancelledAmount" },
            "refund_amount" => new[] { "RefundAmount" },
            "referrer_name" => new[] { "ReferrerName" },
            "referrer_code" => new[] { "ReferrerCode" },
            "referred_customer_name" => new[] { "ReferredCustomerName" },
            "referral_points" or "referral_points_earned" => new[] { "ReferralPointsEarned" },
            "referral_balance" => new[] { "ReferralBalance" },
            "referrer_points_balance" => new[] { "ReferrerPointsBalance" },
            "relationship" => new[] { "Relationship", "Relation", "RelationshipType" },
            "relation_welcome" => new[] { "RelationWelcome", "RelationshipType" },
            "campaign_text" => new[] { "RawCampaignText", "CampaignText" },
            _ => Array.Empty<string>()
        };

        foreach (var alias in aliasMap)
        {
            if (!context.TryGetValue(alias, out var value) || value is null)
                continue;

            if (string.Equals(parameterName, "relation_welcome", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(alias, "RelationWelcome", StringComparison.OrdinalIgnoreCase))
                    return FormatMetaValue(value);

                if (string.Equals(alias, "RelationshipType", StringComparison.OrdinalIgnoreCase))
                {
                    var mapped = RelationshipWelcomeMapper.TryMap(Convert.ToString(value, CultureInfo.InvariantCulture));
                    if (!string.IsNullOrWhiteSpace(mapped))
                        return mapped;
                }

                return null;
            }

            return FormatMetaValue(value);
        }

        return null;
    }

    private static string FormatMetaValue(object value)
    {
        if (value is DateOnly dateOnly)
            return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is DateTime date)
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is DateTimeOffset offset)
            return offset.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (value is decimal decimalValue)
            return decimalValue.ToString(CultureInfo.InvariantCulture);

        if (value is double doubleValue)
            return doubleValue.ToString(CultureInfo.InvariantCulture);

        if (value is float floatValue)
            return floatValue.ToString(CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
        => Normalizer.TryNormalizeMobileNumber(phoneNumber);

    public sealed record MetaTemplateParameter(string Name, string Source, string ValueType, string? NullPolicy = null);

    public sealed record MetaTemplateDefinition(string MetaName, string LanguageCode, bool RequiresHeader, IReadOnlyList<MetaTemplateParameter> Parameters)
    {
        public static object? TryCreateHeaderComponent(CustomerMessageDto message, MetaTemplateDefinition definition)
        {
            if (!definition.RequiresHeader)
                return null;

            var imageUrl = TryResolveHeaderImage(message);
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            return new
            {
                type = "header",
                parameters = new[]
                {
                    new { type = "image", image = new { link = imageUrl } }
                }
            };
        }

        private static string? TryResolveHeaderImage(CustomerMessageDto message)
        {
            if (string.IsNullOrWhiteSpace(message.CampaignImageUrl))
                return null;

            if (!Uri.TryCreate(message.CampaignImageUrl, UriKind.Absolute, out var uri))
                return null;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
        }
    }

    public static class MetaTemplateCatalog
    {
        public static MetaTemplateDefinition? FromOfficial(MetaWhatsAppTemplateDto template)
        {
            if (!string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(template.Name)
                || string.IsNullOrWhiteSpace(template.Language))
                return null;

            var bodies = template.Components
                .Where(component => string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (bodies.Length != 1 || bodies[0].ParameterNames.Count == 0
                || bodies[0].ParameterNames.Any(string.IsNullOrWhiteSpace)
                || bodies[0].ParameterNames.Distinct(StringComparer.Ordinal).Count() != bodies[0].ParameterNames.Count)
                return null;

            if (template.Components.Any(component =>
                !string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase)
                && component.ParameterNames.Count != 0))
                return null;

            var requiresHeader = template.Components.Any(component =>
                string.Equals(component.Type, "HEADER", StringComparison.OrdinalIgnoreCase)
                && string.Equals(component.Format, "IMAGE", StringComparison.OrdinalIgnoreCase));
            return CreateDefinition(template.Name, template.Language, requiresHeader, bodies[0].ParameterNames.ToArray());
        }

        private static MetaTemplateDefinition CreateDefinition(string metaName, string languageCode, bool requiresHeader, params string[] parameterNames)
        {
            var parameters = parameterNames
                .Select(name => new MetaTemplateParameter(name, name, "string", "required"))
                .ToArray();

            return new MetaTemplateDefinition(metaName, languageCode, requiresHeader, parameters);
        }

        private static readonly IReadOnlyDictionary<string, MetaTemplateDefinition> Templates =
            new Dictionary<string, MetaTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["lumar_order_created"] = CreateDefinition("lumar_order_created", "ar", false),
                ["lumar_order_ready"] = CreateDefinition("lumar_order_ready", "ar", false),
                ["lumar_order_delivered"] = CreateDefinition("lumar_order_delivered", "ar", false),
                ["lumar_piece_cancelled"] = CreateDefinition("lumar_piece_cancelled", "ar", false),
                ["lumar_order_cancelled"] = CreateDefinition("lumar_order_cancelled", "ar", false),
                ["lumar_referrer_reward"] = CreateDefinition("lumar_referrer_reward", "ar", false),
                ["lumar_referrer_reward_relation"] = CreateDefinition("lumar_referrer_reward_relation", "ar", false),
                ["lumar_order_created_with_referrer"] = CreateDefinition("lumar_order_created_with_referrer", "ar", false),
                ["lumar_order_created_with_referrer_relation"] = CreateDefinition("lumar_order_created_with_referrer_relation", "ar", false),
                ["lumar_friday_greeting"] = CreateDefinition("lumar_friday_greeting", "ar", false),
                ["lumar_general_campaign"] = CreateDefinition("lumar_general_campaign", "ar", true, "customer_name", "campaign_text"),

                ["order-created-customer-smart"] = CreateDefinition("lumar_order_created", "ar", false),
                ["order-referrer-combined-smart"] = CreateDefinition("lumar_order_created_with_referrer", "ar", false),
                ["referrer-points-earned-smart"] = CreateDefinition("lumar_referrer_reward", "ar", false),
                ["ReferralPointsEarned"] = CreateDefinition("lumar_referrer_reward", "ar", false),
                ["ordercreated"] = CreateDefinition("lumar_order_created", "ar", false),
                ["order_created"] = CreateDefinition("lumar_order_created", "ar", false),
                ["orderready"] = CreateDefinition("lumar_order_ready", "ar", false),
                ["order_ready"] = CreateDefinition("lumar_order_ready", "ar", false),
                ["orderdelivered"] = CreateDefinition("lumar_order_delivered", "ar", false),
                ["order_delivered"] = CreateDefinition("lumar_order_delivered", "ar", false),
                ["piececancelled"] = CreateDefinition("lumar_piece_cancelled", "ar", false),
                ["piece_cancelled"] = CreateDefinition("lumar_piece_cancelled", "ar", false),
                ["ordercancelled"] = CreateDefinition("lumar_order_cancelled", "ar", false),
                ["order_cancelled"] = CreateDefinition("lumar_order_cancelled", "ar", false),
                ["referrerreward"] = CreateDefinition("lumar_referrer_reward", "ar", false),
                ["referrer_reward"] = CreateDefinition("lumar_referrer_reward", "ar", false),
                ["referrerrewardrelation"] = CreateDefinition("lumar_referrer_reward_relation", "ar", false),
                ["referrer_reward_relation"] = CreateDefinition("lumar_referrer_reward_relation", "ar", false),
                ["ordercreatedwithreferrer"] = CreateDefinition("lumar_order_created_with_referrer", "ar", false),
                ["order_created_with_referrer"] = CreateDefinition("lumar_order_created_with_referrer", "ar", false),
                ["withreferrer"] = CreateDefinition("lumar_order_created_with_referrer", "ar", false),
                ["ordercreatedwithreferrerrelation"] = CreateDefinition("lumar_order_created_with_referrer_relation", "ar", false),
                ["order_created_with_referrer_relation"] = CreateDefinition("lumar_order_created_with_referrer_relation", "ar", false),
                ["withreferrerrelation"] = CreateDefinition("lumar_order_created_with_referrer_relation", "ar", false),
                ["fridaygreeting"] = CreateDefinition("lumar_friday_greeting", "ar", false),
                ["friday_greeting"] = CreateDefinition("lumar_friday_greeting", "ar", false),
                ["generalcampaign"] = CreateDefinition("lumar_general_campaign", "ar", true, "customer_name", "campaign_text"),
                ["general_campaign"] = CreateDefinition("lumar_general_campaign", "ar", true, "customer_name", "campaign_text"),
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

                var normalized = candidate.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
                if (normalized.Contains("OrderCreated", StringComparison.OrdinalIgnoreCase))
                {
                    if (normalized.Contains("WithReferrerRelation", StringComparison.OrdinalIgnoreCase))
                        return Templates["lumar_order_created_with_referrer_relation"];
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
        var providerErrorDetails = default(string?);

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

                    if (error.TryGetProperty("error_data", out var errorData)
                        && errorData.ValueKind == JsonValueKind.Object
                        && errorData.TryGetProperty("details", out var detailsElement))
                    {
                        providerErrorDetails = ExtractSafeDetails(detailsElement);
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
                        SafeProviderErrorType: safeType,
                        ProviderErrorDetails: providerErrorDetails);
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
            SafeProviderErrorType: safeType,
            ProviderErrorDetails: providerErrorDetails);
    }

    private static string? ExtractSafeDetails(JsonElement detailsElement)
    {
        if (detailsElement.ValueKind == JsonValueKind.String)
        {
            return SanitizeSensitiveMessage(detailsElement.GetString() ?? string.Empty);
        }

        if (detailsElement.ValueKind == JsonValueKind.Array)
        {
            var fragments = new List<string>();
            foreach (var item in detailsElement.EnumerateArray())
            {
                var text = item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString(),
                    JsonValueKind.Object => item.ToString(),
                    _ => item.ToString()
                };
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var sanitized = SanitizeSensitiveMessage(text);
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        fragments.Add(sanitized);
                    }
                }
            }

            return fragments.Count == 0 ? null : string.Join("; ", fragments);
        }

        if (detailsElement.ValueKind == JsonValueKind.Object)
        {
            var serialized = detailsElement.ToString();
            return string.IsNullOrWhiteSpace(serialized) ? null : SanitizeSensitiveMessage(serialized);
        }

        return null;
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
