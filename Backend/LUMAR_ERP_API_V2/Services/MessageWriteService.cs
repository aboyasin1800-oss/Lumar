using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using LUMAR_ERP_API_V2.Repositories;
using System.Globalization;

namespace LUMAR_ERP_API_V2.Services;

public sealed class MessageWriteService(
    IMessageRepository repository,
    IMessageTemplateRenderer renderer,
    ISettingsRepository? settingsRepository = null,
    IYemenPhoneNumberNormalizer? phoneNormalizer = null,
    IWhatsAppProviderStore? providerStore = null,
    ILogger<MessageWriteService>? logger = null) : IMessageWriteService
{
    private readonly IYemenPhoneNumberNormalizer _phoneNormalizer = phoneNormalizer ?? new YemenPhoneNumberNormalizer();

    public async Task<MessageWriteResult> TryCreateAsync(MessageWriteRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0)
            return new MessageWriteResult(false, null, null, request.TemplateKey, string.Empty, string.Empty);

        var runtimeSettings = await GetRuntimeSettingsAsync(cancellationToken);
        if (!runtimeSettings.MessageMasterEnabled)
        {
            var legacyTemplateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "OrderCreated" : request.TemplateKey.Trim();
            return new MessageWriteResult(false, null, null, legacyTemplateKey, string.Empty, string.Empty);
        }

        if (IsFridayGreetingRequest(request) && !runtimeSettings.FridayGreetingEnabled)
        {
            var legacyTemplateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "friday-greeting" : request.TemplateKey.Trim();
            return new MessageWriteResult(false, null, null, legacyTemplateKey, string.Empty, string.Empty);
        }

        if (IsCampaignRequest(request) && !runtimeSettings.CampaignEnabled)
        {
            var legacyTemplateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "general-campaign-smart" : request.TemplateKey.Trim();
            return new MessageWriteResult(false, null, null, legacyTemplateKey, string.Empty, string.Empty);
        }

        var routes = await ResolveRoutesAsync(request, runtimeSettings, cancellationToken);
        if (routes.Count == 0)
        {
            var legacyTemplateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "OrderCreated" : request.TemplateKey.Trim();
            return new MessageWriteResult(false, null, null, legacyTemplateKey, string.Empty, string.Empty);
        }

        await using var providerLease = runtimeSettings.WhatsAppEnabled && providerStore is not null
            ? await providerStore.AcquireAsync(false, cancellationToken) : null;
        var whatsAppChannel = providerLease is null ? "WhatsApp" : providerLease.Provider.Channel();
        var createdResults = new List<MessageWriteResult>();
        foreach (var route in routes)
        {
            var template = await repository.GetActiveTemplateAsync(route.TemplateKey, cancellationToken);
            if (template is null)
                continue;

            var idempotencyKey = BuildIdempotencyKey(request, route.TemplateKey, route.TargetKind);
            if (await repository.HasProcessedAsync(idempotencyKey, cancellationToken))
                continue;

            var values = route.Values;
            var title = renderer.Render(template.Title, values);
            var body = renderer.Render(template.Body, values);
            var titleUnresolved = ContainsKnownUnresolvedVariable(title);
            var bodyUnresolved = ContainsKnownUnresolvedVariable(body);
            var missingRequiredValue = IsMissingRequiredSmartReferralValue(route.TemplateKey, values);
            if (titleUnresolved || bodyUnresolved || missingRequiredValue)
            {
                var unresolvedNames = GetUnresolvedVariableNames(title, body);
                var requiredValueState = GetReferrerRequiredValueState(route.TemplateKey, values);
                var exactBlockReason = titleUnresolved ? "TITLE_UNRESOLVED"
                    : bodyUnresolved ? "BODY_UNRESOLVED"
                    : "MISSING_REQUIRED_VALUE";
                logger?.LogWarning(
                    "SMART_TEMPLATE_BLOCKED_BEFORE_INSERT EventType={EventType} TemplateKey={TemplateKey} TARGET_CUSTOMER_ID={CustomerId} TITLE_UNRESOLVED={TitleUnresolved} BODY_UNRESOLVED={BodyUnresolved} MISSING_REQUIRED_VALUE={MissingRequiredValue} CONTEXT_KEY_NAMES={ContextKeys} ReferrerName_PRESENT={ReferrerNamePresent} ReferrerName_EMPTY={ReferrerNameEmpty} ReferralPointsEarned_PRESENT={ReferralPointsEarnedPresent} ReferralPointsEarned_EMPTY={ReferralPointsEarnedEmpty} ReferrerPointsBalance_PRESENT={ReferrerPointsBalancePresent} ReferrerPointsBalance_EMPTY={ReferrerPointsBalanceEmpty} ReferrerCode_PRESENT={ReferrerCodePresent} ReferrerCode_EMPTY={ReferrerCodeEmpty} UNRESOLVED_VARIABLE_NAMES={UnresolvedVariables} EXACT_BLOCK_REASON={ExactBlockReason}",
                    request.EventType,
                    route.TemplateKey,
                    route.CustomerId,
                    titleUnresolved ? "YES" : "NO",
                    bodyUnresolved ? "YES" : "NO",
                    missingRequiredValue ? "YES" : "NO",
                    string.Join(',', values.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)),
                    requiredValueState.ReferrerNamePresent,
                    requiredValueState.ReferrerNameEmpty,
                    requiredValueState.ReferralPointsEarnedPresent,
                    requiredValueState.ReferralPointsEarnedEmpty,
                    requiredValueState.ReferrerPointsBalancePresent,
                    requiredValueState.ReferrerPointsBalanceEmpty,
                    requiredValueState.ReferrerCodePresent,
                    requiredValueState.ReferrerCodeEmpty,
                    unresolvedNames.Count == 0 ? "NONE" : string.Join(',', unresolvedNames),
                    exactBlockReason);
                continue;
            }

            int? messageId = null;
            int? notificationId = null;
            var normalizedPhone = _phoneNormalizer.TryNormalizeMobileNumber(route.PhoneNumber);

            if (runtimeSettings.WhatsAppEnabled && whatsAppChannel is not null && !string.IsNullOrWhiteSpace(normalizedPhone))
            {
                var rawCampaignText = GetContextStringValue(request.Context, "RawCampaignText")
                    ?? GetContextStringValue(request.Context, "CampaignText");

                if (string.Equals(request.EventType, "ManualCampaign", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(rawCampaignText))
                {
                    notificationId = await repository.CreateCustomerNotificationAsync(
                        route.CustomerId,
                        request.OrderId,
                        request.EventType,
                        title,
                        body,
                        null,
                        cancellationToken);

                    if (notificationId is null or <= 0)
                    {
                        continue;
                    }

                    createdResults.Add(new MessageWriteResult(true, null, notificationId, template.TemplateKey, title, body));
                    continue;
                }

                var createdPair = await repository.CreateCustomerMessageAndNotificationAsync(
                    route.CustomerId,
                    request.OrderId,
                    request.EventType,
                    template.TemplateKey,
                    whatsAppChannel,
                    title,
                    body,
                    normalizedPhone,
                    request.ReferenceId,
                    request.ReferenceType,
                    idempotencyKey,
                    GetContextStringValue(request.Context, "CampaignImageUrl"),
                    rawCampaignText,
                    cancellationToken);

                messageId = createdPair.MessageId;
                notificationId = createdPair.NotificationId;
            }
            else
            {
                notificationId = await repository.CreateCustomerNotificationAsync(
                    route.CustomerId,
                    request.OrderId,
                    request.EventType,
                    title,
                    body,
                    null,
                    cancellationToken);
            }

            if (notificationId is null or <= 0)
                continue;

            createdResults.Add(new MessageWriteResult(true, messageId, notificationId, template.TemplateKey, title, body));
        }

        return createdResults.Count > 0
            ? createdResults[0]
            : new MessageWriteResult(false, null, null, request.TemplateKey, string.Empty, string.Empty);
    }

    private async Task<List<MessageRoute>> ResolveRoutesAsync(MessageWriteRequest request, MessageSettingsDto runtimeSettings, CancellationToken cancellationToken)
    {
        var ctx = request.Context ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var requestedCustomerPhone = _phoneNormalizer.TryNormalizeMobileNumber(request.PhoneNumber) ?? string.Empty;
        var customerPhone = _phoneNormalizer.TryNormalizeMobileNumber(GetContextValue(ctx, "CustomerPhoneNumber", "PhoneNumber", request.PhoneNumber)) ?? string.Empty;
        var referrerPhone = _phoneNormalizer.TryNormalizeMobileNumber(GetContextValue(ctx, "ReferrerPhoneNumber", "ReferrerPhone", "ReferrerMobile", "ReferrerMobileNumber")) ?? string.Empty;
        var referrerCustomerId = GetIntValue(ctx, "ReferrerCustomerId", "ReferrerId");
        var referrerName = GetStringValue(ctx, "ReferrerName");
        var referrerCode = GetStringValue(ctx, "ReferrerCode");
        var hasReferrer = referrerCustomerId is > 0 || !string.IsNullOrWhiteSpace(referrerName) || !string.IsNullOrWhiteSpace(referrerCode) || !string.IsNullOrWhiteSpace(referrerPhone);

        var normalizedKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "OrderCreated" : request.TemplateKey.Trim();
        var routePlans = new List<MessageRoute>();

        if (!hasReferrer)
        {
            var customerTemplate = ResolveLegacyTemplateKey(normalizedKey, "order-created-customer-smart", "OrderCreated");
            var customerValues = ToValueDictionary(ctx, allowReferrerValues: false);
            routePlans.Add(new MessageRoute(customerTemplate, request.CustomerId, requestedCustomerPhone, "customer", customerValues));
            return routePlans;
        }

        if (!string.IsNullOrWhiteSpace(customerPhone) && !string.IsNullOrWhiteSpace(referrerPhone) && string.Equals(customerPhone, referrerPhone, StringComparison.OrdinalIgnoreCase))
        {
            if (!runtimeSettings.DirectReferralDefaultOn)
                return routePlans;

            var combinedTemplate = ResolveLegacyTemplateKey(normalizedKey, "order-referrer-combined-smart", "OrderCreated");
            var combinedValues = ToValueDictionary(ctx, allowReferrerValues: true);
            routePlans.Add(new MessageRoute(combinedTemplate, request.CustomerId, requestedCustomerPhone, "combined", combinedValues));
            return routePlans;
        }

        var customerTemplateKeyForDifferentPhone = ResolveLegacyTemplateKey(normalizedKey, "order-created-customer-smart", "OrderCreated");
        var customerValuesForDifferentPhone = ToValueDictionary(ctx, allowReferrerValues: false);
        routePlans.Add(new MessageRoute(customerTemplateKeyForDifferentPhone, request.CustomerId, requestedCustomerPhone, "customer", customerValuesForDifferentPhone));

        if (runtimeSettings.OtherReferralDefaultOff)
            return routePlans;

        var referrerTemplateKeyForDifferentPhone = ResolveLegacyTemplateKey(normalizedKey, "referrer-points-earned-smart", "ReferralPointsEarned");
        var referrerValuesForDifferentPhone = ToValueDictionary(ctx, allowReferrerValues: true);
        var referrerCustomerTargetId = referrerCustomerId ?? request.CustomerId;
        routePlans.Add(new MessageRoute(referrerTemplateKeyForDifferentPhone, referrerCustomerTargetId, referrerPhone, "referrer", referrerValuesForDifferentPhone));

        return routePlans;
    }

    private async Task<MessageSettingsDto> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
            return new MessageSettingsDto(true, true, false, true, true);

        return await settingsRepository.GetMessageSettingsAsync(cancellationToken);
    }

    private static bool IsFridayGreetingRequest(MessageWriteRequest request)
    {
        var templateText = request.TemplateKey ?? string.Empty;
        var eventText = request.EventType ?? string.Empty;
        return templateText.Contains("friday", StringComparison.OrdinalIgnoreCase)
            || eventText.Contains("friday", StringComparison.OrdinalIgnoreCase)
            || templateText.Contains("greeting", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetUnresolvedVariableNames(string title, string body)
    {
        var names = new List<string>();
        foreach (var text in new[] { title, body })
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, @"\{\{([^{}]+)\}\}"))
            {
                var name = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(name);
            }
        }
        return names;
    }

    private sealed record ReferralRequiredValueState(
        string ReferrerNamePresent,
        string ReferrerNameEmpty,
        string ReferralPointsEarnedPresent,
        string ReferralPointsEarnedEmpty,
        string ReferrerPointsBalancePresent,
        string ReferrerPointsBalanceEmpty,
        string ReferrerCodePresent,
        string ReferrerCodeEmpty);

    private static ReferralRequiredValueState GetReferrerRequiredValueState(string templateKey, IReadOnlyDictionary<string, object?> values)
    {
        (string present, string empty) State(string key)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
                return ("NO", "YES");
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return ("YES", string.IsNullOrWhiteSpace(text) ? "YES" : "NO");
        }

        if (!string.Equals(templateKey, "referrer-points-earned-smart", StringComparison.OrdinalIgnoreCase))
            return new ReferralRequiredValueState("N/A", "N/A", "N/A", "N/A", "N/A", "N/A", "N/A", "N/A");

        var name = State("ReferrerName");
        var points = State("ReferralPointsEarned");
        var balance = State("ReferrerPointsBalance");
        var code = State("ReferrerCode");
        return new ReferralRequiredValueState(name.present, name.empty, points.present, points.empty, balance.present, balance.empty, code.present, code.empty);
    }

    private static bool IsMissingRequiredSmartReferralValue(string templateKey, IReadOnlyDictionary<string, object?> values)
    {
        if (!string.Equals(templateKey, "referrer-points-earned-smart", StringComparison.OrdinalIgnoreCase))
            return false;

        var requiredKeys = new[]
        {
            "ReferrerName", "ReferralPointsEarned", "ReferrerPointsBalance", "ReferrerCode"
        };
        return requiredKeys.Any(key =>
            !values.TryGetValue(key, out var value)
            || value is null
            || string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)));
    }

    private static bool ContainsKnownUnresolvedVariable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var officialVariables = new[]
        {
            "customer_name", "CustomerName", "HijriDate", "OrderNumber", "OrderDetails",
            "TotalAmount", "CurrencyName", "PaidAmount", "RemainingAmount", "DeliveryDate",
            "PointsEarned", "CustomerPointsEarned", "PointsBalance", "CustomerPointsBalance",
            "CustomerCode", "ReferrerName", "ReferrerCode", "ReferralIntroduction",
            "ReferralPointsEarned", "ReferrerPointsBalance", "RelationLine"
        };
        return officialVariables.Any(variable =>
            text.Contains("{{" + variable + "}}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCampaignRequest(MessageWriteRequest request)
    {
        var templateText = request.TemplateKey ?? string.Empty;
        var eventText = request.EventType ?? string.Empty;
        return templateText.Contains("campaign", StringComparison.OrdinalIgnoreCase)
            || eventText.Contains("campaign", StringComparison.OrdinalIgnoreCase)
            || templateText.Contains("promotion", StringComparison.OrdinalIgnoreCase)
            || eventText.Contains("promotion", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLegacyTemplateKey(string requestedTemplateKey, string smartTemplateKey, string fallbackTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(requestedTemplateKey))
            return smartTemplateKey;

        var trimmed = requestedTemplateKey.Trim();
        var normalized = trimmed.ToLowerInvariant();

        if (normalized is "loyaltyearned" or "loyalty-earned" or "loyaltypointsearned" or "loyalty-points-earned" or "loyalty_points_earned")
            return "loyalty-earned";

        if (normalized is "referralearned" or "referral-earned")
            return "referral-earned";

        if (normalized is "referralpointsearned" or "referralpoints-earned" or "referral-points-earned" or "referrer-points-earned-smart")
            return "referrer-points-earned-smart";

        if (normalized is "ordercreated" or "order-created" or "order-created-customer-smart" or "order-referrer-combined-smart")
            return smartTemplateKey;

        if (normalized is "referralearned" or "referral-earned")
            return "referral-earned";


        if (normalized is "readymadesalecompleted" or "ready-made-sale" or "readymadesale")
            return "ready-made-sale";

        if (normalized is "orderready" or "order-ready")
            return "order-ready";

        if (normalized is "orderdelivered" or "order-delivered")
            return "order-delivered";

        return trimmed;
    }

    private static Dictionary<string, object?> ToValueDictionary(IReadOnlyDictionary<string, object?> context, bool allowReferrerValues)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in context)
        {
            if (!allowReferrerValues && IsReferrerField(pair.Key))
                continue;

            values[pair.Key] = pair.Value;
        }

        return values;
    }

    private static bool IsReferrerField(string key)
    {
        var normalized = key.Trim();
        return normalized.Contains("Referrer", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Referral", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Relation", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Referrer", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Referral", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Relation", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetStringValue(IReadOnlyDictionary<string, object?> context, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (context.TryGetValue(key, out var value) && value is not null)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static int? GetIntValue(IReadOnlyDictionary<string, object?> context, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (context.TryGetValue(key, out var value) && value is not null)
            {
                return value switch
                {
                    int i => i,
                    long l => checked((int)l),
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => null,
                };
            }
        }

        return null;
    }

    private static string? GetContextValue(IReadOnlyDictionary<string, object?> context, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (context.TryGetValue(key, out var value) && value is not null)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? GetContextStringValue(IReadOnlyDictionary<string, object?>? context, string key)
    {
        if (context is null)
            return null;

        if (!context.TryGetValue(key, out var value) || value is null)
            return null;

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildIdempotencyKey(MessageWriteRequest request, string templateKey, string targetKind)
    {
        var reference = string.IsNullOrWhiteSpace(request.ReferenceId) ? request.OrderId?.ToString() ?? "0" : request.ReferenceId.Trim();
        return $"{request.EventType}:{request.ReferenceType}:{reference}:{request.CustomerId}:{request.OrderId ?? 0}:{targetKind}:{templateKey}";
    }

    private sealed record MessageRoute(string TemplateKey, int CustomerId, string PhoneNumber, string TargetKind, IReadOnlyDictionary<string, object?> Values);
}
