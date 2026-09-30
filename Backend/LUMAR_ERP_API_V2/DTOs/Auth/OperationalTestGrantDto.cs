namespace LUMAR_ERP_API_V2.DTOs.Auth;

/// <summary>Temporary ES-7B-3 operational grant issued during the approved test period.</summary>
public sealed record OperationalTestGrantDto(string GrantToken, DateTimeOffset ExpiresAtUtc);