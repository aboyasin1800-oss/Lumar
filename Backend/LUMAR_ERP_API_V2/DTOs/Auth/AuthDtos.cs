namespace LUMAR_ERP_API_V2.DTOs.Auth;

public sealed record LoginDto(string Username, string Password, bool RememberMe);
public sealed record ChangeUsernameDto(string CurrentPassword, string Username);
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmPassword);
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
    int? SupplierId = null);
public sealed record SessionDto(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);

// Mobile Auth DTOs
public sealed record RefreshTokenDto(string RefreshToken);
public sealed record RefreshResultDto(string AccessToken, DateTime ExpiresAtUtc, string? NewRefreshToken);

public sealed record RecoveryStartDto(string Username);
public sealed record RecoveryStartResultDto(bool Success, string? Message);

public sealed record RecoveryVerifyDto(string Username, string Code);
public sealed record RecoveryVerifyResultDto(bool Success, string? Message, string? ResetToken);

public sealed record RecoveryCompleteDto(string ResetToken, string NewPassword, string ConfirmPassword);
public sealed record RecoveryCompleteResultDto(bool Success, string? Message);

public sealed record SessionInfoDto(Guid SessionId, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc, string? RevocationReason, string? DeviceInfo, bool IsCurrent);

public sealed record AccountTypeDto(string AccountType, int? CustomerId, int? EmployeeId, int? SupplierId);

public sealed record MobileLoginDto(string Username, string Password, bool RememberMe);
public sealed record MobileSessionDto(string Token, DateTime ExpiresAtUtc, CurrentUserDto User, string AccountType, int? CustomerId, int? EmployeeId, int? SupplierId);