using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using System.Net.Http.Headers;

namespace LUMAR_ERP_API_V2.Authorization;

public interface IAuthenticatedUserContext
{
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthenticatedUserContext(IHttpContextAccessor httpContextAccessor, IAuthService authService) : IAuthenticatedUserContext
{
    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header) || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            return Task.FromResult<CurrentUserDto?>(null);
        return authService.GetCurrentUserAsync(header.Parameter, cancellationToken);
    }
}