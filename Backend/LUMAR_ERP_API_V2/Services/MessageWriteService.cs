using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class MessageWriteService(
    IMessageRepository repository,
    IMessageTemplateRenderer renderer,
    ISettingsRepository? settingsRepository = null) : IMessageWriteService
{
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

            int? messageId = null;
            int? notificationId = null;

            if (runtimeSettings.WhatsAppEnabled && IsValidYemenMobileNumber(route.PhoneNumber))
            {
                var createdPair = await repository.CreateCustomerMessageAndNotificationAsync(
                    route.CustomerId,
                    request.OrderId,
                    request.EventType,
                    template.TemplateKey,
                    "WhatsApp",
                    title,
                    body,
                    route.PhoneNumber,
                    request.ReferenceId,
                    request.ReferenceType,
                    idempotencyKey,
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
        var customerPhone = NormalizePhone(GetContextValue(ctx, "CustomerPhoneNumber", "PhoneNumber", request.PhoneNumber));
        var referrerPhone = NormalizePhone(GetContextValue(ctx, "ReferrerPhoneNumber", "ReferrerPhone", "ReferrerMobile", "ReferrerMobileNumber"));
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
            routePlans.Add(new MessageRoute(customerTemplate, request.CustomerId, request.PhoneNumber, "customer", customerValues));
            return routePlans;
        }

        if (!string.IsNullOrWhiteSpace(customerPhone) && !string.IsNullOrWhiteSpace(referrerPhone) && string.Equals(customerPhone, referrerPhone, StringComparison.OrdinalIgnoreCase))
        {
            if (!runtimeSettings.DirectReferralDefaultOn)
                return routePlans;

            var combinedTemplate = ResolveLegacyTemplateKey(normalizedKey, "order-referrer-combined-smart", "OrderCreated");
            var combinedValues = ToValueDictionary(ctx, allowReferrerValues: true);
            routePlans.Add(new MessageRoute(combinedTemplate, request.CustomerId, request.PhoneNumber, "combined", combinedValues));
            return routePlans;
        }

        var customerTemplateKeyForDifferentPhone = ResolveLegacyTemplateKey(normalizedKey, "order-created-customer-smart", "OrderCreated");
        var customerValuesForDifferentPhone = ToValueDictionary(ctx, allowReferrerValues: false);
        routePlans.Add(new MessageRoute(customerTemplateKeyForDifferentPhone, request.CustomerId, request.PhoneNumber, "customer", customerValuesForDifferentPhone));

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

        if (normalized is "ordercreated" or "order-created" or "order-created-customer-smart" or "order-referrer-combined-smart" or "referrer-points-earned-smart")
            return smartTemplateKey;

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

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = new string(value.Where(char.IsDigit).ToArray());
        if (cleaned.Length >= 10)
            return cleaned;

        return value.Trim();
    }

    private static bool IsValidYemenMobileNumber(string? phoneNumber)
    {
        var normalized = NormalizePhone(phoneNumber);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (normalized.StartsWith("967", StringComparison.Ordinal))
        {
            var local = normalized[3..];
            return local.Length == 9 && local.StartsWith("7", StringComparison.Ordinal);
        }

        return normalized.Length == 9 && normalized.StartsWith("7", StringComparison.Ordinal);
    }

    private static string BuildIdempotencyKey(MessageWriteRequest request, string templateKey, string targetKind)
    {
        var reference = string.IsNullOrWhiteSpace(request.ReferenceId) ? request.OrderId?.ToString() ?? "0" : request.ReferenceId.Trim();
        return $"{request.EventType}:{request.ReferenceType}:{reference}:{request.CustomerId}:{request.OrderId ?? 0}:{targetKind}:{templateKey}";
    }

    private sealed record MessageRoute(string TemplateKey, int CustomerId, string PhoneNumber, string TargetKind, IReadOnlyDictionary<string, object?> Values);
}
