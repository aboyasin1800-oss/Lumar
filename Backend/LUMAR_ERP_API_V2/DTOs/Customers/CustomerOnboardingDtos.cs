using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Customers;

public sealed class CreateCustomerWithReferralDto
{
    [Required, StringLength(100)]
    public string? CustomerName { get; init; }

    [Required, StringLength(20)]
    public string? PhoneNumber { get; init; }

    [StringLength(250)]
    public string? Address { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    public int? ReferrerCustomerId { get; init; }

    [StringLength(100)]
    public string? RelationshipType { get; init; }
}

public sealed record CustomerReferralCandidateDto(
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    string? PhoneNumber,
    string? ReferralCode);

public sealed record CustomerCreationResultDto(
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    string PhoneNumber,
    string? Address,
    string? Notes,
    string? RelationshipType,
    decimal? TotalPoints,
    decimal? TotalDebts,
    int ReferralCount,
    decimal ReferralRewardsAmount,
    decimal ReferralRewardPoints,
    long? RegistrationTransactionId,
    int? ReferrerCustomerId,
    string? ReferrerCustomerCode,
    string? ReferrerCustomerName,
    string? ReferrerPhoneNumber,
    string? ReferrerReferralCode);