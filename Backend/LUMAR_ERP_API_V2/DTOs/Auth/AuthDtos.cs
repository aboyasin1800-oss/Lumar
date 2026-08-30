namespace LUMAR_ERP_API_V2.DTOs.Auth;

public sealed record LoginDto(string Username, string Password, bool RememberMe);
public sealed record ChangeUsernameDto(string CurrentPassword, string Username);
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record CurrentUserDto(int UserId, string Username, string FullName, string? Role, bool IsActive, DateTime? LastLoginUtc);
public sealed record SessionDto(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);