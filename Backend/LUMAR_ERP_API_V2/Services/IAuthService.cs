using LUMAR_ERP_API_V2.DTOs.Auth;
namespace LUMAR_ERP_API_V2.Services;
public interface IAuthService
{
    Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct);
    Task<bool> VerifyAdministrativePasswordAsync(string username, string password, CancellationToken ct);
    Task<ProtectedAreasStatusDto> GetProtectedAreasStatusAsync(CancellationToken ct);
    Task<bool> VerifyProtectedAreasPasswordAsync(string password, CancellationToken ct);
    Task<bool> SetProtectedAreasPasswordAsync(ProtectedAreasPasswordSetupDto request, CancellationToken ct);
    Task<bool> UpdateProtectedAreasDurationAsync(ProtectedAreasDurationDto request, string currentProtectionPassword, CancellationToken ct);
    Task<SessionDto> RegisterMobileCustomerAsync(MobileCustomerRegistrationDto request, CancellationToken ct);
    Task<EmployeeProvisioningResult?> ProvisionOfficialEmployeeAccountAsync(EmployeeProvisioningRequest request, CancellationToken ct);
    Task<SupplierProvisioningResult?> ProvisionOfficialSupplierAccountAsync(SupplierProvisioningRequest request, CancellationToken ct);
    Task<bool> CompleteEmployeePasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct);
    Task<bool> CompleteSupplierPasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct);
    Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct);
    Task<bool> LogoutAsync(string token, CancellationToken ct);
    Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct);
    Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct);
    Task<RecoveryStartResultDto> BeginAdministrativeRecoveryAsync(string adminToken, string username, CancellationToken ct);
    Task<RecoveryCompleteResultDto> CompleteAdministrativeRecoveryAsync(string resetToken, string newPassword, string confirmPassword, CancellationToken ct);
    Task<AccountRecoverySubmitResultDto> SubmitAccountRecoveryAsync(AccountRecoverySubmitDto request, CancellationToken ct);
    Task<IReadOnlyList<AccountRecoveryCandidateDto>> ListAccountRecoveryRequestsAsync(string adminToken, CancellationToken ct);
    Task<bool> ApproveAccountRecoveryAsync(string adminToken, AccountRecoveryApproveDto request, CancellationToken ct);
    Task<bool> RejectAccountRecoveryAsync(string adminToken, AccountRecoveryRejectDto request, CancellationToken ct);
    Task<RecoveryCompleteResultDto> CompleteAccountRecoveryAsync(AccountRecoveryCompleteDto request, CancellationToken ct);
    Task<AccountRecoveryStatusDto?> GetAccountRecoveryStatusAsync(Guid publicRequestKey, CancellationToken ct);
    Task<IReadOnlyList<OfficialRecoveryMatchDto>> SearchOfficialRecoveryMatchesAsync(string accountType, string term, CancellationToken ct);
    Task<AccountTypeDto?> GetAccountTypeAsync(string token, CancellationToken ct);
}