using LUMAR_ERP_API_V2.DTOs.Auth;
namespace LUMAR_ERP_API_V2.Repositories;
public interface IAuthRepository
{
    Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct);
    Task<SessionDto> RegisterMobileCustomerAsync(MobileCustomerRegistrationDto request, CancellationToken ct);
    Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct);
    Task<bool> LogoutAsync(string token, CancellationToken ct);
    Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct);
    Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct);
    Task<RefreshResultDto?> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<bool> LogoutAllAsync(string token, CancellationToken ct);
    Task<RecoveryStartResultDto> RecoveryStartAsync(string username, CancellationToken ct);
    Task<RecoveryVerifyResultDto> RecoveryVerifyAsync(string username, string code, CancellationToken ct);
    Task<RecoveryCompleteResultDto> RecoveryCompleteAsync(string resetToken, string newPassword, CancellationToken ct);
    Task<List<SessionInfoDto>> GetSessionsAsync(string token, CancellationToken ct);
    Task<bool> RevokeSessionAsync(string token, Guid sessionId, CancellationToken ct);
    Task<AccountTypeDto?> GetAccountTypeAsync(string token, CancellationToken ct);
}