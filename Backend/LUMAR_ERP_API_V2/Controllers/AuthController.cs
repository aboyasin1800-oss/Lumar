using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
namespace LUMAR_ERP_API_V2.Controllers;
[ApiController, Route("auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("login")] public async Task<ActionResult<SessionDto>> Login(LoginDto request,CancellationToken ct){var result=await service.LoginAsync(request,ct);return result is null?Unauthorized("Invalid username or password."):Ok(result);}
    [HttpPost("mobile/customer/register")] public async Task<ActionResult<SessionDto>> RegisterMobileCustomer(MobileCustomerRegistrationDto request,CancellationToken ct){try{var result=await service.RegisterMobileCustomerAsync(request,ct);return Ok(result);}catch(ArgumentException ex){return BadRequest(ex.Message);}catch(Microsoft.Data.SqlClient.SqlException ex) when(ex.Number is 2601 or 2627){return Conflict("Username or customer already exists.");}}
    [HttpPost("mobile/employee/provision")] public async Task<ActionResult<EmployeeProvisioningResult>> ProvisionOfficialEmployeeAccount(EmployeeProvisioningRequest request,CancellationToken ct){try{var result=await service.ProvisionOfficialEmployeeAccountAsync(request,ct);return result is null ? NotFound() : Ok(result);}catch(ArgumentException ex){return BadRequest(ex.Message);}catch(Microsoft.Data.SqlClient.SqlException ex) when(ex.Number is 2601 or 2627){return Conflict("Employee username or account already exists.");}}
    [HttpPost("mobile/employee/password-setup")] public async Task<IActionResult> CompleteEmployeePasswordSetup([FromBody] PasswordSetupDto request,CancellationToken ct){if(request is null || string.IsNullOrWhiteSpace(request.SetupToken)) return BadRequest("رمز إعداد كلمة المرور مطلوب.");var ok=await service.CompleteEmployeePasswordSetupAsync(request.SetupToken, request.NewPassword, request.ConfirmPassword, ct);return ok ? NoContent() : BadRequest("تعذر إعداد كلمة المرور أو الرمز غير صالح.");}
    [HttpGet("me")] public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct){var user=await service.GetCurrentUserAsync(Token,ct);return user is null?Unauthorized():Ok(user);}
    [HttpGet("account-type")] public async Task<ActionResult<AccountTypeDto>> AccountType(CancellationToken ct){var account=await service.GetAccountTypeAsync(Token,ct);return account is null?Unauthorized():Ok(account);}
    [HttpPost("logout")] public async Task<IActionResult> Logout(CancellationToken ct)=>await service.LogoutAsync(Token,ct)?NoContent():Unauthorized();
    [HttpPut("username")] public async Task<ActionResult<SessionDto>> Username(ChangeUsernameDto request,CancellationToken ct){try{var result=await service.ChangeUsernameAsync(Token,request,ct);return result is null?BadRequest("Unable to change username."):Ok(result);}catch(ArgumentException){return Conflict("Username already exists.");}}
    [HttpPut("password")] public async Task<IActionResult> Password(ChangePasswordDto request,CancellationToken ct)=>await service.ChangePasswordAsync(Token,request,ct)?NoContent():BadRequest("Unable to change password.");
    private string Token => Request.Headers.Authorization.ToString().StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?Request.Headers.Authorization.ToString()[7..]:string.Empty;
}