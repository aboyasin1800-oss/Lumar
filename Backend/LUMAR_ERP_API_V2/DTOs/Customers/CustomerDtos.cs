using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Customers;

public sealed record CustomerListDto(int CustomerId, string? CustomerCode, string? CustomerName, string? PhoneNumber, decimal? TotalPoints, int? TotalPieces, decimal? TotalDebts, string? RelationshipType);

public sealed record CustomerDetailsDto(int CustomerId, string? CustomerCode, string? CustomerName, string? PhoneNumber, string? ParentCustomerCode, int? ParentCustomerId, string? ParentCustomerName, string? Address, string? Notes, bool IsActive, decimal? TotalPoints, int? TotalPieces, decimal? TotalDebts, string? RelationshipType);

public sealed class CreateCustomerDto
{
    [Required, StringLength(20)] public string? CustomerCode { get; init; }
    [Required, StringLength(100)] public string? CustomerName { get; init; }
    [StringLength(20)] public string? PhoneNumber { get; init; }
    [StringLength(250)] public string? Address { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
    public int? ParentCustomerId { get; init; }
    [StringLength(100)] public string? ReferralCode { get; init; }
    [StringLength(100)] public string? RelationshipType { get; init; }
}

public sealed class UpdateCustomerDto
{
    [Required, StringLength(20)] public string? CustomerCode { get; init; }
    [Required, StringLength(100)] public string? CustomerName { get; init; }
    [StringLength(20)] public string? PhoneNumber { get; init; }
    [StringLength(250)] public string? Address { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public int? ParentCustomerId { get; init; }
    [StringLength(100)] public string? RelationshipType { get; init; }
}

public sealed record ReferralTreeNodeDto(int CustomerId, string? CustomerCode, string? CustomerName, int? ParentCustomerId, IReadOnlyList<ReferralTreeNodeDto> Children);
public sealed record ReferralHierarchyDto(int CustomerId, IReadOnlyList<CustomerDetailsDto> Ancestors, IReadOnlyList<ReferralTreeNodeDto> Descendants);

public sealed record CustomerMeasurementDto(int Id, int CustomerId, string PieceType, string MeasurementName, decimal MeasurementValue, DateTime CreatedAtUtc, int RevisionNumber);

public sealed class UpsertCustomerMeasurementsDto
{
    [Required, StringLength(100)] public string? PieceType { get; init; }
    [Required, MinLength(1)] public IReadOnlyList<CustomerMeasurementValueDto> Measurements { get; init; } = [];
}

public sealed class CustomerMeasurementValueDto
{
    [Required, StringLength(100)] public string? MeasurementName { get; init; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal MeasurementValue { get; init; }
}

public sealed record CustomerLedgerEntryDto(int CustomerLedgerEntryId, int CustomerId, string ReferenceNumber, decimal DebitAmount, decimal CreditAmount, decimal BalanceAfterTransaction, DateTime CreatedAt);
public sealed record CustomerLoyaltyDto(int LoyaltyAccountId, int CustomerId, decimal CurrentPoints, decimal LifetimeEarnedPoints, decimal LifetimeRedeemedPoints, decimal PendingExpirePoints, int? VipLevelId, string? VipLevelCode, string? VipLevelDisplayName, DateTime CreatedAt, DateTime UpdatedAt, DateTime? LastActivityAt, string LoyaltyAccountStatus = "Active", DateTime? WarningStartedAtUtc = null, DateTime? FrozenAtUtc = null, DateTime? ReactivatedAtUtc = null, string? FreezeReason = null, DateTime? LastQualifyingActivityAtUtc = null);
public sealed record CustomerReferralDto(int ReferralAccountId, int CustomerId, int? ReferralCodeId, string? ReferralCode, bool? ReferralCodeIsActive, int TotalReferrals, int SuccessfulReferrals, decimal TotalRewardsAmount, decimal TotalRewardPoints, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ReferralCodeCreatedAt, DateTime? ReferralCodeLastUsedAt);