namespace LUMAR_ERP_API_V2.DTOs.Auth;

public sealed record ProtectedAreasStatusDto(bool PasswordConfigured, string DurationMode, int DurationMinutes);
public sealed record ProtectedAreasVerifyDto(string Password);
public sealed record ProtectedAreasPasswordSetupDto(string Password, string ConfirmPassword);
public sealed record ProtectedAreasDurationDto(bool ManualOnly, int DurationMinutes);
public sealed record ProtectedAreasVerificationResultDto(bool Verified);
public sealed record ProtectedAreasDurationUpdateRequest(bool ManualOnly, int DurationMinutes, string CurrentProtectionPassword);
