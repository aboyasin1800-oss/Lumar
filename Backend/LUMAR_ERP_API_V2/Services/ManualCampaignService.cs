using System.Text.Json;
using System.Text.Json.Serialization;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public enum ManualCampaignRecipientMode
{
    SingleCustomer,
    AllEligibleCustomers,
}

public sealed class ManualCampaignRecipientModeJsonConverter : JsonConverter<ManualCampaignRecipientMode>
{
    public override ManualCampaignRecipientMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
        {
            return numericValue switch
            {
                0 => ManualCampaignRecipientMode.SingleCustomer,
                1 => ManualCampaignRecipientMode.AllEligibleCustomers,
                _ => throw new JsonException("RecipientMode must be 0 (SingleCustomer) or 1 (AllEligibleCustomers).")
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase))
                return ManualCampaignRecipientMode.SingleCustomer;
            if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase))
                return ManualCampaignRecipientMode.AllEligibleCustomers;
            if (Enum.TryParse<ManualCampaignRecipientMode>(text, true, out var parsed) && Enum.IsDefined(parsed))
                return parsed;
        }

        throw new JsonException("RecipientMode must be 0 (SingleCustomer) or 1 (AllEligibleCustomers).");
    }

    public override void Write(Utf8JsonWriter writer, ManualCampaignRecipientMode value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }
}

[JsonConverter(typeof(ManualCampaignRequestJsonConverter))]
public sealed record ManualCampaignRequest(
    string CampaignReference,
    ManualCampaignRecipientMode RecipientMode,
    int? CustomerId,
    string MessageText,
    string TemplateKey,
    string Title,
    string? CampaignImageUrl = null,
    CancellationToken CancellationToken = default);

public sealed class ManualCampaignRequestJsonConverter : JsonConverter<ManualCampaignRequest>
{
    public override ManualCampaignRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Manual campaign request payload must be an object.");

        var campaignReference = root.TryGetProperty("campaignReference", out var campaignReferenceElement)
            ? campaignReferenceElement.GetString() ?? string.Empty
            : string.Empty;

        var recipientMode = root.TryGetProperty("recipientMode", out var recipientModeElement)
            ? recipientModeElement.ValueKind switch
            {
                JsonValueKind.Number when recipientModeElement.TryGetInt32(out var numericValue) => numericValue,
                JsonValueKind.String when int.TryParse(recipientModeElement.GetString(), out var numericValue) => numericValue,
                _ => throw new JsonException("RecipientMode must be 0 (SingleCustomer) or 1 (AllEligibleCustomers).")
            }
            : throw new JsonException("RecipientMode is required.");

        int? customerId = null;
        if (root.TryGetProperty("customerId", out var customerIdElement) && customerIdElement.ValueKind != JsonValueKind.Null)
        {
            customerId = customerIdElement.GetInt32();
        }

        var messageText = root.TryGetProperty("messageText", out var messageTextElement)
            ? messageTextElement.GetString() ?? string.Empty
            : string.Empty;

        var templateKey = root.TryGetProperty("templateKey", out var templateKeyElement)
            ? templateKeyElement.GetString() ?? string.Empty
            : string.Empty;

        var title = root.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString() ?? string.Empty
            : string.Empty;

        var campaignImageUrl = root.TryGetProperty("campaignImageUrl", out var campaignImageUrlElement)
            ? campaignImageUrlElement.ValueKind == JsonValueKind.Null ? null : campaignImageUrlElement.GetString()
            : null;

        return new ManualCampaignRequest(
            CampaignReference: campaignReference,
            RecipientMode: recipientMode switch
            {
                0 => ManualCampaignRecipientMode.SingleCustomer,
                1 => ManualCampaignRecipientMode.AllEligibleCustomers,
                _ => throw new JsonException("RecipientMode must be 0 (SingleCustomer) or 1 (AllEligibleCustomers).")
            },
            CustomerId: customerId,
            MessageText: messageText,
            TemplateKey: templateKey,
            Title: title,
            CampaignImageUrl: campaignImageUrl);
    }

    public override void Write(Utf8JsonWriter writer, ManualCampaignRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("campaignReference", value.CampaignReference);
        writer.WriteNumber("recipientMode", (int)value.RecipientMode);
        if (value.CustomerId.HasValue) writer.WriteNumber("customerId", value.CustomerId.Value);
        writer.WriteString("messageText", value.MessageText);
        writer.WriteString("templateKey", value.TemplateKey);
        writer.WriteString("title", value.Title);
        if (!string.IsNullOrWhiteSpace(value.CampaignImageUrl))
            writer.WriteString("campaignImageUrl", value.CampaignImageUrl);
        else
            writer.WriteNull("campaignImageUrl");
        writer.WriteEndObject();
    }
}

public sealed record ManualCampaignResult(
    int CreatedCount,
    int SkippedCount,
    int DuplicateCount,
    IReadOnlyList<int> CustomerIds);

public interface IManualCampaignService
{
    Task<ManualCampaignResult> ProcessAsync(ManualCampaignRequest request, CancellationToken cancellationToken);
}

public sealed class ManualCampaignService(
    ICustomerRepository customerRepository,
    IMessageWriteService messageWriteService,
    IYemenPhoneNumberNormalizer phoneNormalizer,
    ISettingsRepository settingsRepository) : IManualCampaignService
{
    private readonly Dictionary<string, HashSet<int>> _seenCampaignCustomers = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ManualCampaignResult> ProcessAsync(ManualCampaignRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CampaignReference))
            throw new ArgumentException("CampaignReference is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            throw new ArgumentException("TemplateKey is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.MessageText))
            throw new ArgumentException("MessageText is required.", nameof(request));

        var settings = await settingsRepository.GetMessageSettingsAsync(cancellationToken);
        if (!settings.MessageMasterEnabled || !settings.CampaignEnabled)
            return new ManualCampaignResult(0, 0, 0, Array.Empty<int>());

        var targetCustomers = await ResolveTargetCustomersAsync(request, cancellationToken);
        var created = new List<int>();
        var skipped = 0;
        var duplicate = 0;

        foreach (var customer in targetCustomers)
        {
            var normalizedPhone = phoneNormalizer.TryNormalizeMobileNumber(customer.PhoneNumber);
            if (string.IsNullOrWhiteSpace(normalizedPhone))
            {
                skipped++;
                continue;
            }

            var key = $"{request.CampaignReference}:{customer.CustomerId}";
            if (!_seenCampaignCustomers.TryGetValue(request.CampaignReference, out var customerIds))
            {
                customerIds = new HashSet<int>();
                _seenCampaignCustomers[request.CampaignReference] = customerIds;
            }

            if (!customerIds.Add(customer.CustomerId))
            {
                duplicate++;
                continue;
            }

            var createdMessage = await messageWriteService.TryCreateAsync(
                new MessageWriteRequest(
                    CustomerId: customer.CustomerId,
                    OrderId: null,
                    EventType: "ManualCampaign",
                    TemplateKey: request.TemplateKey,
                    PhoneNumber: normalizedPhone,
                    ReferenceId: request.CampaignReference,
                    ReferenceType: "ManualCampaign",
                    Context: new Dictionary<string, object?>
                    {
                        ["CustomerName"] = customer.CustomerName,
                        ["CampaignReference"] = request.CampaignReference,
                        ["MessageText"] = request.MessageText,
                        ["Title"] = request.Title,
                        ["RecipientMode"] = request.RecipientMode.ToString(),
                        ["CampaignBody"] = request.MessageText,
                        ["CampaignText"] = request.MessageText,
                        ["CampaignImageUrl"] = request.CampaignImageUrl,
                    }),
                cancellationToken);

            if (createdMessage.Created)
            {
                created.Add(customer.CustomerId);
            }
            else
            {
                skipped++;
            }
        }

        return new ManualCampaignResult(created.Count, skipped, duplicate, created);
    }

    private async Task<IReadOnlyList<CustomerDetailsDto>> ResolveTargetCustomersAsync(ManualCampaignRequest request, CancellationToken cancellationToken)
    {
        if (request.RecipientMode == ManualCampaignRecipientMode.SingleCustomer)
        {
            if (request.CustomerId is null or <= 0)
                return Array.Empty<CustomerDetailsDto>();

            var customer = await customerRepository.GetByIdAsync(request.CustomerId.Value, cancellationToken);
            return customer is null ? Array.Empty<CustomerDetailsDto>() : [customer];
        }

        var customers = await customerRepository.GetListAsync(cancellationToken);
        return customers
            .Select(item => new CustomerDetailsDto(
                item.CustomerId,
                item.CustomerCode,
                item.CustomerName,
                item.PhoneNumber,
                null,
                null,
                null,
                null,
                null,
                true,
                item.TotalPoints,
                item.TotalPieces,
                item.TotalDebts,
                item.RelationshipType))
            .Where(item => !string.IsNullOrWhiteSpace(item.PhoneNumber))
            .ToList();
    }
}
