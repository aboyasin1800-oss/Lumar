using LUMAR_ERP_API_V2.DTOs.Customers;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerListDto>> GetListAsync(CancellationToken cancellationToken);
    Task<CustomerDetailsDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerMeasurementDto>> GetMeasurementsAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerMeasurementDto>?> UpsertMeasurementsAsync(int customerId, UpsertCustomerMeasurementsDto measurements, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerLoyaltyDto?> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerReferralDto?> GetReferralsAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerDetailsDto> CreateAsync(CreateCustomerDto customer, CancellationToken cancellationToken);
    Task<CustomerCreationResultDto> CreateWithReferralAsync(CreateCustomerWithReferralDto customer, CancellationToken cancellationToken);
    Task<CustomerDetailsDto?> UpdateAsync(int customerId, UpdateCustomerDto customer, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerListDto>> SearchAsync(string term, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerReferralCandidateDto>> SearchReferralCandidatesAsync(string term, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerDetailsDto>> GetAncestorsAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralTreeNodeDto>> GetDescendantsAsync(int customerId, CancellationToken cancellationToken);
    Task<int?> ResolveReferralCodeAsync(string referralCode, CancellationToken cancellationToken);
    Task<bool> WouldCreateCycleAsync(int customerId, int parentCustomerId, CancellationToken cancellationToken);
}