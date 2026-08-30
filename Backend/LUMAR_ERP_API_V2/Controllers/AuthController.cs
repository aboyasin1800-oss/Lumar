using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
namespace LUMAR_ERP_API_V2.Controllers;
[ApiController, Route("auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("login")] public async Task<ActionResult<SessionDto>> Login(LoginDto request,CancellationToken ct){var result=await service.LoginAsync(request,ct);return result is null?Unauthorized("Invalid username or password."):Ok(result);}
    [HttpGet("me")] public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct){var user=await service.GetCurrentUserAsync(Token,ct);return user is null?Unauthorized():Ok(user);}
    [HttpPost("logout")] public async Task<IActionResult> Logout(CancellationToken ct)=>await service.LogoutAsync(Token,ct)?NoContent():Unauthorized();
    [HttpPut("username")] public async Task<ActionResult<SessionDto>> Username(ChangeUsernameDto request,CancellationToken ct){try{var result=await service.ChangeUsernameAsync(Token,request,ct);return result is null?BadRequest("Unable to change username."):Ok(result);}catch(ArgumentException){return Conflict("Username already exists.");}}
    [HttpPut("password")] public async Task<IActionResult> Password(ChangePasswordDto request,CancellationToken ct)=>await service.ChangePasswordAsync(Token,request,ct)?NoContent():BadRequest("Unable to change password.");
    private string Token => Request.Headers.Authorization.ToString().StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)?Request.Headers.Authorization.ToString()[7..]:string.Empty;
}