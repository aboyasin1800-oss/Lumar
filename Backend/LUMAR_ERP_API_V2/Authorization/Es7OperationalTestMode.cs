using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.DTOs.Auth;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Authorization;

public sealed class Es7OperationalTestModeOptions
{
    public const string SectionName = "Es7OperationalTestMode";
    public bool Enabled { get; init; }
    public int GrantMinutes { get; init; } = 30;
}

public sealed class Es7OperationalTestMode(IOptions<Es7OperationalTestModeOptions> options)
{
    public const string GrantHeaderName = "X-ES7-Operational-Grant";
    private readonly ConcurrentDictionary<string, Grant> _grants = new(StringComparer.Ordinal);
    private readonly Es7OperationalTestModeOptions _options = options.Value;

    public bool Enabled => _options.Enabled;

    public OperationalTestGrantDto Activate(CurrentUserDto user, string bearerToken)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(bearerToken))
            throw new InvalidOperationException("وضع التنفيذ والاختبار غير متاح.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.GrantMinutes, 1, 120));
        _grants[token] = new Grant(user.UserId, Fingerprint(bearerToken), expiresAtUtc);
        RemoveExpiredGrants();
        return new OperationalTestGrantDto(token, expiresAtUtc);
    }

    public bool HasActiveGrant(CurrentUserDto user, string bearerToken, string grantToken)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(grantToken)) return false;
        if (!_grants.TryGetValue(grantToken, out var grant)) return false;
        if (grant.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _grants.TryRemove(grantToken, out _);
            return false;
        }
        return grant.UserId == user.UserId
            && CryptographicOperations.FixedTimeEquals(grant.SessionFingerprint, Fingerprint(bearerToken));
    }

    private void RemoveExpiredGrants()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var grant in _grants.Where(entry => entry.Value.ExpiresAtUtc <= now))
            _grants.TryRemove(grant.Key, out _);
    }

    private static byte[] Fingerprint(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private sealed record Grant(int UserId, byte[] SessionFingerprint, DateTimeOffset ExpiresAtUtc);
}

public static class Es7OperationalAuthorization
{
    public static bool HasPermission(
        CurrentUserDto user,
        string permission,
        Es7OperationalTestMode? testMode,
        HttpRequest? request)
    {
        if (Es7PermissionPolicy.HasPermission(user, permission)) return true;
        if (testMode is null || request is null) return false;
        var authorization = request.Headers.Authorization.ToString();
        var bearerToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : string.Empty;
        return testMode.HasActiveGrant(user, bearerToken, request.Headers[Es7OperationalTestMode.GrantHeaderName].ToString());
    }
}