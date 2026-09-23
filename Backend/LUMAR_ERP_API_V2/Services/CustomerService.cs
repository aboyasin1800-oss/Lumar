using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Repositories;
using System.Text.RegularExpressions;

namespace LUMAR_ERP_API_V2.Services;

public sealed class CustomerService(
    ICustomerRepository repository,
    ILoyaltyRepository loyaltyRepository) : ICustomerService
{
    public Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken) => repository.GetListAsync(cancellationToken);
    public Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken) => repository.GetByIdAsync(customerId, cancellationToken);
    public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken) => repository.GetMeasurementsAsync(customerId, cancellationToken);
    public Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken) => repository.UpsertMeasurementsAsync(customerId, measurements, cancellationToken);
    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken) => repository.GetLedgerAsync(customerId, cancellationToken);
    public Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken) => repository.GetLoyaltyAsync(customerId, cancellationToken);
    public Task<CustomerReferralDto?> GetReferralsAsync(int customerId, CancellationToken cancellationToken) => repository.GetReferralsAsync(customerId, cancellationToken);
    public async Task<CustomerDetailsDto> CreateAsync(CreateCustomerDto customer, CancellationToken cancellationToken)
    {
        ValidateCustomerName(customer.CustomerName);
        var created = await repository.CreateAsync(customer, cancellationToken);
        await loyaltyRepository.EnsureAccountAsync(created.CustomerId, cancellationToken);
        return created;
    }
    public Task<CustomerCreationResultDto> CreateWithReferralAsync(CreateCustomerWithReferralDto customer, CancellationToken cancellationToken)
    {
        ValidateCustomerName(customer.CustomerName);
        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
            throw new ArgumentException("رقم الهاتف مطلوب.");
        if (customer.ReferrerCustomerId is <= 0)
            throw new ArgumentException("المحيل المحدد غير صالح.");
        if (customer.ReferrerCustomerId.HasValue && string.IsNullOrWhiteSpace(customer.RelationshipType))
            throw new ArgumentException("صلة المحيل بالعميل مطلوبة.");

        return repository.CreateWithReferralAsync(customer, cancellationToken);
    }

    private static void ValidateCustomerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("اسم العميل مطلوب.");

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount is < 3 or > 4)
            throw new ArgumentException("يجب أن يكون اسم العميل ثلاثياً أو رباعياً.");
    }

    public Task<CustomerDetailsDto?> UpdateAsync(int customerId, UpdateCustomerDto customer, CancellationToken cancellationToken) => repository.UpdateAsync(customerId, customer, cancellationToken);
    public Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken) => repository.SearchAsync(term, cancellationToken);
    public Task<IReadOnlyList<CustomerReferralCandidateDto>> SearchReferralCandidatesAsync(string term, CancellationToken cancellationToken) => repository.SearchReferralCandidatesAsync(term, cancellationToken);
    public async Task<ReferralHierarchyDto?> GetReferralHierarchyAsync(int customerId, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(customerId, cancellationToken) is null) return null;
        return new ReferralHierarchyDto(customerId, await repository.GetAncestorsAsync(customerId, cancellationToken), await repository.GetDescendantsAsync(customerId, cancellationToken));
    }
}