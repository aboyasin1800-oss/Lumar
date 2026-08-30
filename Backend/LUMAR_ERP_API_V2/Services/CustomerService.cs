using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken) => repository.GetListAsync(cancellationToken);
    public Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken) => repository.GetByIdAsync(customerId, cancellationToken);
    public Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken) => repository.GetMeasurementsAsync(customerId, cancellationToken);
    public Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken) => repository.UpsertMeasurementsAsync(customerId, measurements, cancellationToken);
    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken) => repository.GetLedgerAsync(customerId, cancellationToken);
    public Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken) => repository.GetLoyaltyAsync(customerId, cancellationToken);
    public Task<CustomerReferralDto?> GetReferralsAsync(int customerId, CancellationToken cancellationToken) => repository.GetReferralsAsync(customerId, cancellationToken);
    public Task<CustomerDetailsDto> CreateAsync(CreateCustomerDto customer, CancellationToken cancellationToken) => repository.CreateAsync(customer, cancellationToken);
    public Task<CustomerDetailsDto?> UpdateAsync(int customerId, UpdateCustomerDto customer, CancellationToken cancellationToken) => repository.UpdateAsync(customerId, customer, cancellationToken);
    public Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken) => repository.SearchAsync(term, cancellationToken);
    public async Task<ReferralHierarchyDto?> GetReferralHierarchyAsync(int customerId, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(customerId, cancellationToken) is null) return null;
        return new ReferralHierarchyDto(customerId, await repository.GetAncestorsAsync(customerId, cancellationToken), await repository.GetDescendantsAsync(customerId, cancellationToken));
    }
}