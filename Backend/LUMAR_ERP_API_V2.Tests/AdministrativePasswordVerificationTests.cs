using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class AdministrativePasswordVerificationTests
{
    [Fact]
    public async Task WithoutBearerSession_IsRejected()
    {
        var controller = new AuthController(new AdministrativePasswordVerificationAuthService(), NullLogger<AuthController>.Instance, new MissingUserContext());

        var result = await controller.VerifyAdministrativePassword(
            new AdministrativePasswordVerificationDto("admin", "wrong-password"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task WrongPassword_IsRejectedWithoutReturningPassword()
    {
        var controller = new AuthController(new AdministrativePasswordVerificationAuthService(false), NullLogger<AuthController>.Instance, new FixedUserContext(new CurrentUserDto(7, "employee", "Employee", "Employee", true, null)));

        var result = await controller.VerifyAdministrativePassword(
            new AdministrativePasswordVerificationDto("admin", "wrong-password"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ActiveAdminPassword_IsAcceptedWithoutEchoingCredentials()
    {
        var controller = new AuthController(new AdministrativePasswordVerificationAuthService(true), NullLogger<AuthController>.Instance, new FixedUserContext(new CurrentUserDto(1, "admin", "Admin", "Admin", true, null)));

        var result = await controller.VerifyAdministrativePassword(
            new AdministrativePasswordVerificationDto("admin", "correct-password"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain("password", ok.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InactiveAdministrativeAccount_IsRejected()
    {
        var controller = new AuthController(new AdministrativePasswordVerificationAuthService(false), NullLogger<AuthController>.Instance, new FixedUserContext(new CurrentUserDto(3, "inactive-admin", "Inactive Admin", "Admin", false, null)));

        var result = await controller.VerifyAdministrativePassword(
            new AdministrativePasswordVerificationDto("inactive-admin", "password"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task NonAdministrativeCredentials_AreRejected()
    {
        var service = new AdministrativePasswordVerificationAuthService(false);
        var controller = new AuthController(service, NullLogger<AuthController>.Instance, new FixedUserContext(new CurrentUserDto(2, "employee", "Employee", "Employee", true, null)));

        var result = await controller.VerifyAdministrativePassword(
            new AdministrativePasswordVerificationDto("employee", "employee-password"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private sealed class MissingUserContext : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrentUserDto?>(null);
    }

    private sealed class FixedUserContext(CurrentUserDto user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrentUserDto?>(user);
    }

    private sealed class AdministrativePasswordVerificationAuthService(bool verified = false) : IAuthService
    {
        public Task<bool> VerifyAdministrativePasswordAsync(string username, string password, CancellationToken ct)
            => Task.FromResult(verified);
        public Task<ProtectedAreasStatusDto> GetProtectedAreasStatusAsync(CancellationToken ct)
            => Task.FromResult(new ProtectedAreasStatusDto(false, "timed", 30));
        public Task<bool> VerifyProtectedAreasPasswordAsync(string password, CancellationToken ct)
            => Task.FromResult(verified);
        public Task<bool> SetProtectedAreasPasswordAsync(ProtectedAreasPasswordSetupDto request, CancellationToken ct)
            => Task.FromResult(verified);
        public Task<bool> UpdateProtectedAreasDurationAsync(ProtectedAreasDurationDto request, string currentProtectionPassword, CancellationToken ct)
            => Task.FromResult(verified);

        public Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct) => Task.FromResult<SessionDto?>(null);
        public Task<SessionDto> RegisterMobileCustomerAsync(MobileCustomerRegistrationDto request, CancellationToken ct) => throw new NotSupportedException();
        public Task<EmployeeProvisioningResult?> ProvisionOfficialEmployeeAccountAsync(EmployeeProvisioningRequest request, CancellationToken ct) => Task.FromResult<EmployeeProvisioningResult?>(null);
        public Task<SupplierProvisioningResult?> ProvisionOfficialSupplierAccountAsync(SupplierProvisioningRequest request, CancellationToken ct) => Task.FromResult<SupplierProvisioningResult?>(null);
        public Task<bool> CompleteEmployeePasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> CompleteSupplierPasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct) => Task.FromResult(false);
        public Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct) => Task.FromResult<CurrentUserDto?>(null);
        public Task<bool> LogoutAsync(string token, CancellationToken ct) => Task.FromResult(false);
        public Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct) => Task.FromResult<SessionDto?>(null);
        public Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct) => Task.FromResult(false);
        public Task<LUMAR_ERP_API_V2.DTOs.Auth.RecoveryStartResultDto> BeginAdministrativeRecoveryAsync(string adminToken, string username, CancellationToken ct) => throw new NotSupportedException();
        public Task<LUMAR_ERP_API_V2.DTOs.Auth.RecoveryCompleteResultDto> CompleteAdministrativeRecoveryAsync(string resetToken, string newPassword, string confirmPassword, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountRecoverySubmitResultDto> SubmitAccountRecoveryAsync(AccountRecoverySubmitDto request, CancellationToken ct) => Task.FromResult(new AccountRecoverySubmitResultDto(0, Guid.Empty, "Pending", ""));
        public Task<IReadOnlyList<AccountRecoveryCandidateDto>> ListAccountRecoveryRequestsAsync(string adminToken, CancellationToken ct) => Task.FromResult<IReadOnlyList<AccountRecoveryCandidateDto>>(Array.Empty<AccountRecoveryCandidateDto>());
        public Task<bool> ApproveAccountRecoveryAsync(string adminToken, AccountRecoveryApproveDto request, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> RejectAccountRecoveryAsync(string adminToken, AccountRecoveryRejectDto request, CancellationToken ct) => Task.FromResult(false);
        public Task<RecoveryCompleteResultDto> CompleteAccountRecoveryAsync(AccountRecoveryCompleteDto request, CancellationToken ct) => Task.FromResult(new RecoveryCompleteResultDto(false, ""));
        public Task<AccountRecoveryStatusDto?> GetAccountRecoveryStatusAsync(Guid publicRequestKey, CancellationToken ct) => Task.FromResult<AccountRecoveryStatusDto?>(null);
        public Task<AccountTypeDto?> GetAccountTypeAsync(string token, CancellationToken ct) => Task.FromResult<AccountTypeDto?>(null);
    }
}
