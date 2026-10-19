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

public sealed record ManualCampaignRequest(
    string CampaignReference,
    ManualCampaignRecipientMode RecipientMode,
    int? CustomerId,
    string MessageText,
    string TemplateKey,
    string Title,
    CancellationToken CancellationToken = default);

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
