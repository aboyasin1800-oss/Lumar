using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Repositories;
namespace LUMAR_ERP_API_V2.Services;
public sealed class AuthService(IAuthRepository repository) : IAuthService
{
    public Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct) => repository.LoginAsync(login, ct);
    public Task<bool> VerifyAdministrativePasswordAsync(string username, string password, CancellationToken ct) => repository.VerifyAdministrativePasswordAsync(username, password, ct);
    public Task<ProtectedAreasStatusDto> GetProtectedAreasStatusAsync(CancellationToken ct) => repository.GetProtectedAreasStatusAsync(ct);
    public Task<bool> VerifyProtectedAreasPasswordAsync(string password, CancellationToken ct) => repository.VerifyProtectedAreasPasswordAsync(password, ct);
    public Task<bool> SetProtectedAreasPasswordAsync(ProtectedAreasPasswordSetupDto request, CancellationToken ct) => repository.SetProtectedAreasPasswordAsync(request, ct);
    public Task<bool> UpdateProtectedAreasDurationAsync(ProtectedAreasDurationDto request, string currentProtectionPassword, CancellationToken ct) => repository.UpdateProtectedAreasDurationAsync(request, currentProtectionPassword, ct);
    public Task<SessionDto> RegisterMobileCustomerAsync(MobileCustomerRegistrationDto request, CancellationToken ct) => repository.RegisterMobileCustomerAsync(request, ct);
    public Task<EmployeeProvisioningResult?> ProvisionOfficialEmployeeAccountAsync(EmployeeProvisioningRequest request, CancellationToken ct) => repository.ProvisionOfficialEmployeeAccountAsync(request, ct);
    public Task<SupplierProvisioningResult?> ProvisionOfficialSupplierAccountAsync(SupplierProvisioningRequest request, CancellationToken ct) => repository.ProvisionOfficialSupplierAccountAsync(request, ct);
    public Task<bool> CompleteEmployeePasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct) => repository.CompleteEmployeePasswordSetupAsync(setupToken, newPassword, confirmPassword, ct);
    public Task<bool> CompleteSupplierPasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct) => repository.CompleteSupplierPasswordSetupAsync(setupToken, newPassword, confirmPassword, ct);
    public Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct) => repository.GetCurrentUserAsync(token, ct);
    public Task<bool> LogoutAsync(string token, CancellationToken ct) => repository.LogoutAsync(token, ct);
    public Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct) => repository.ChangeUsernameAsync(token, request, ct);
    public Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct) => repository.ChangePasswordAsync(token, request, ct);
    public Task<RecoveryStartResultDto> BeginAdministrativeRecoveryAsync(string adminToken, string username, CancellationToken ct) => repository.BeginAdministrativeRecoveryAsync(adminToken, username, ct);
    public Task<RecoveryCompleteResultDto> CompleteAdministrativeRecoveryAsync(string resetToken, string newPassword, string confirmPassword, CancellationToken ct) => repository.CompleteAdministrativeRecoveryAsync(resetToken, newPassword, confirmPassword, ct);
    public Task<AccountRecoverySubmitResultDto> SubmitAccountRecoveryAsync(AccountRecoverySubmitDto request, CancellationToken ct) => repository.SubmitAccountRecoveryAsync(request, ct);
    public Task<IReadOnlyList<AccountRecoveryCandidateDto>> ListAccountRecoveryRequestsAsync(string adminToken, CancellationToken ct) => repository.ListAccountRecoveryRequestsAsync(adminToken, ct);
    public Task<bool> ApproveAccountRecoveryAsync(string adminToken, AccountRecoveryApproveDto request, CancellationToken ct) => repository.ApproveAccountRecoveryAsync(adminToken, request, ct);
    public Task<bool> RejectAccountRecoveryAsync(string adminToken, AccountRecoveryRejectDto request, CancellationToken ct) => repository.RejectAccountRecoveryAsync(adminToken, request, ct);
    public Task<RecoveryCompleteResultDto> CompleteAccountRecoveryAsync(AccountRecoveryCompleteDto request, CancellationToken ct) => repository.CompleteAccountRecoveryAsync(request, ct);
    public Task<AccountRecoveryStatusDto?> GetAccountRecoveryStatusAsync(Guid publicRequestKey, CancellationToken ct) => repository.GetAccountRecoveryStatusAsync(publicRequestKey, ct);
    public Task<AccountTypeDto?> GetAccountTypeAsync(string token, CancellationToken ct) => repository.GetAccountTypeAsync(token, ct);
}