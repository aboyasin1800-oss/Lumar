namespace LUMAR_ERP_API_V2.DTOs.Auth;

public sealed record LoginDto(string Username, string Password, bool RememberMe);
public sealed record AdministrativePasswordVerificationDto(string Username, string Password);
public sealed record MobileCustomerRegistrationDto(
    string Username,
    string Password,
    string ConfirmPassword,
    string FullName,
    string? CustomerName,
    string? PhoneNumber = null,
    string? Address = null,
    string? Notes = null,
    bool RememberMe = true,
    int? ReferrerCustomerId = null,
    string? RelationshipType = null,
    string? CustomerCode = null);
public sealed record ChangeUsernameDto(string CurrentPassword, string Username);
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record PasswordSetupDto(string SetupToken, string NewPassword, string ConfirmPassword);
public sealed record EmployeeProvisioningRequest(string EmployeeCode, string? PhoneNumber = null, string? PreferredUsername = null, bool RememberMe = true);
public sealed record EmployeeProvisioningResult(int EmployeeId, int UserId, int MobileAccountId, string Username, string AccountType, string? SetupToken, DateTime ExpiresAtUtc, bool PasswordSetupReady);
public sealed record SupplierProvisioningRequest(string SupplierCode, string? PhoneNumber = null, string? PreferredUsername = null, bool RememberMe = true);
public sealed record SupplierProvisioningResult(int SupplierId, int UserId, int MobileAccountId, string Username, string AccountType, string? SetupToken, DateTime ExpiresAtUtc, bool PasswordSetupReady);
public sealed record CurrentUserDto(
    int UserId,
    string Username,
    string FullName,
    string? Role,
    bool IsActive,
    DateTime? LastLoginUtc,
    string? AccountType = null,
    int? CustomerId = null,
    int? EmployeeId = null,
    int? SupplierId = null)
{
    public IReadOnlyList<int> ManagedCustomerIds { get; init; } = Array.Empty<int>();

    public bool HasCustomerAccess(int customerId)
        => customerId > 0 && (CustomerId == customerId || ManagedCustomerIds.Contains(customerId));
}

public sealed record SessionDto(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);

// Mobile Auth DTOs
public sealed record RefreshTokenDto(string RefreshToken);
public sealed record RefreshResultDto(string AccessToken, DateTime ExpiresAtUtc, string? NewRefreshToken);

public sealed record RecoveryStartDto(string Username);
public sealed record AdminRecoveryStartDto(string Username);
public sealed record RecoveryStartResultDto(bool Success, string? Message, string? ResetToken = null);

public sealed record RecoveryVerifyDto(string Username, string Code);
public sealed record RecoveryVerifyResultDto(bool Success, string? Message, string? ResetToken);

public sealed record RecoveryCompleteDto(string ResetToken, string NewPassword, string ConfirmPassword);
public sealed record RecoveryCompleteResultDto(bool Success, string? Message);

public sealed record AccountRecoverySubmitDto(string AccountType, string PhoneNumber, string? FullName, string? Username, string? CustomerCode, string? EmployeeCode, string? SupplierCode, string? VerificationInfo);
public sealed record AccountRecoverySubmitResultDto(long RecoveryRequestId, Guid PublicRequestKey, string Status, string Message);
public sealed record AccountRecoveryCandidateDto(long RecoveryRequestId, string AccountType, string? SubmittedFullName, string? SubmittedUsername, string? PhoneNumber, string? CustomerCode, string? EmployeeCode, string? SupplierCode, string? VerificationInfo, int? TargetUserId, int? TargetMobileAccountId, int? TargetCustomerId, int? TargetEmployeeId, int? TargetSupplierId, string Status, DateTime CreatedAtUtc);
public sealed record AccountRecoveryApproveDto(long RecoveryRequestId, int TargetUserId);
public sealed record AccountRecoveryRejectDto(long RecoveryRequestId, string? Reason);
public sealed record AccountRecoveryCompleteDto(Guid PublicRequestKey, string NewPassword, string ConfirmPassword);
public sealed record OfficialRecoveryMatchDto(int TargetUserId, int? MobileAccountId, string AccountType, string OfficialId, string OfficialName, string? PhoneNumber, string? Username, bool MobileAccountIsActive);
public sealed record AccountRecoveryStatusDto(long RecoveryRequestId, string Status);

public sealed record SessionInfoDto(Guid SessionId, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc, string? RevocationReason, string? DeviceInfo, bool IsCurrent);

public sealed record AccountTypeDto(string AccountType, int? CustomerId, int? EmployeeId, int? SupplierId);

public sealed record MobileLoginDto(string Username, string Password, bool RememberMe);
public sealed record MobileSessionDto(string Token, DateTime ExpiresAtUtc, CurrentUserDto User, string AccountType, int? CustomerId, int? EmployeeId, int? SupplierId);