using System.Security.Cryptography;
using System.Diagnostics;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using Microsoft.Data.SqlClient;
namespace LUMAR_ERP_API_V2.Repositories;
public sealed class AuthRepository(OperationalSqlConnectionFactory connections, ILogger<AuthRepository> logger) : IAuthRepository
{
    public async Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password)) return null;
        try
        {
            await using var connection = connections.Create(); await connection.OpenAsync(ct);
            logger.LogInformation("Authentication database connection opened in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            await using var command = new SqlCommand("SELECT UserID, Username, UserPassword, PasswordHash, FullName, IsActive, LastLoginUtc FROM dbo.Users WHERE Username=@username", connection);
            command.Parameters.AddWithValue("@username", login.Username.Trim()); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct) || !reader.GetBoolean(5)) return null;
            var legacy = reader.GetString(2); var hash = reader.IsDBNull(3) ? null : reader.GetString(3); var user = new CurrentUserDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(4), null, true, reader.IsDBNull(6) ? null : reader.GetDateTime(6));
            if (!Verify(login.Password, hash, legacy)) return null;
            logger.LogInformation("Authentication password verification completed in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            await reader.CloseAsync();
            if (hash is null) { await using var upgrade = new SqlCommand("UPDATE dbo.Users SET PasswordHash=@hash, UserPassword='' WHERE UserID=@id", connection); upgrade.Parameters.AddWithValue("@hash", HashPassword(login.Password)); upgrade.Parameters.AddWithValue("@id", user.UserId); await upgrade.ExecuteNonQueryAsync(ct); }
            var session = await CreateSessionAsync(connection, user, login.RememberMe, ct);
            logger.LogInformation("Authentication session creation and login update completed in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            return session;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Authentication login failed after {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
    public Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct) => GetUserAsync(token, ct);
    public async Task<bool> LogoutAsync(string token, CancellationToken ct) { await using var c=connections.Create(); await c.OpenAsync(ct); await using var cmd=new SqlCommand("UPDATE dbo.UserSessions SET RevokedAtUtc=SYSUTCDATETIME(), RevocationReason='Logout' WHERE TokenHash=@hash AND RevokedAtUtc IS NULL",c); cmd.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=TokenHash(token); return await cmd.ExecuteNonQueryAsync(ct)>0; }
    public async Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct) { if(string.IsNullOrWhiteSpace(request.Username)) return null; var user=await GetUserAsync(token,ct); if(user is null || !await VerifyCurrentAsync(user.UserId,request.CurrentPassword,ct)) return null; await using var c=connections.Create(); await c.OpenAsync(ct); try { await using var cmd=new SqlCommand("UPDATE dbo.Users SET Username=@name WHERE UserID=@id",c);cmd.Parameters.AddWithValue("@name",request.Username.Trim());cmd.Parameters.AddWithValue("@id",user.UserId);if(await cmd.ExecuteNonQueryAsync(ct)!=1)return null;}catch(SqlException e) when(e.Number is 2601 or 2627){throw new ArgumentException("Username already exists.");} await LogoutAsync(token,ct); return await CreateSessionAsync(c,user with { Username=request.Username.Trim() },true,ct); }
    public async Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct) { if(request.NewPassword.Length<8 || request.NewPassword!=request.ConfirmPassword || request.NewPassword==request.CurrentPassword) return false; var user=await GetUserAsync(token,ct);if(user is null || !await VerifyCurrentAsync(user.UserId,request.CurrentPassword,ct))return false;await using var c=connections.Create();await c.OpenAsync(ct);await using var cmd=new SqlCommand("UPDATE dbo.Users SET PasswordHash=@hash, UserPassword='', SecurityStamp=NEWID() WHERE UserID=@id; UPDATE dbo.UserSessions SET RevokedAtUtc=SYSUTCDATETIME(), RevocationReason='PasswordChanged' WHERE UserId=@id AND RevokedAtUtc IS NULL",c);cmd.Parameters.AddWithValue("@hash",HashPassword(request.NewPassword));cmd.Parameters.AddWithValue("@id",user.UserId);await cmd.ExecuteNonQueryAsync(ct);return true; }
    private async Task<CurrentUserDto?> GetUserAsync(string token,CancellationToken ct){if(string.IsNullOrWhiteSpace(token))return null;await using var c=connections.Create();await c.OpenAsync(ct);await using var cmd=new SqlCommand("SELECT u.UserID,u.Username,u.FullName,u.IsActive,u.LastLoginUtc FROM dbo.UserSessions s JOIN dbo.Users u ON u.UserID=s.UserId WHERE s.TokenHash=@hash AND s.RevokedAtUtc IS NULL AND s.ExpiresAtUtc>SYSUTCDATETIME() AND u.IsActive=1",c);cmd.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=TokenHash(token);await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new CurrentUserDto(r.GetInt32(0),r.GetString(1),r.GetString(2),null,true,r.IsDBNull(4)?null:r.GetDateTime(4)):null;}
    private async Task<bool> VerifyCurrentAsync(int id,string password,CancellationToken ct){await using var c=connections.Create();await c.OpenAsync(ct);await using var cmd=new SqlCommand("SELECT UserPassword,PasswordHash FROM dbo.Users WHERE UserID=@id",c);cmd.Parameters.AddWithValue("@id",id);await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)&&Verify(password,r.IsDBNull(1)?null:r.GetString(1),r.GetString(0));}
    private static async Task<SessionDto> CreateSessionAsync(SqlConnection c,CurrentUserDto user,bool remember,CancellationToken ct){var token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var expiry=DateTime.UtcNow.Add(remember?TimeSpan.FromDays(30):TimeSpan.FromHours(8));await using var cmd=new SqlCommand("INSERT dbo.UserSessions(SessionId,UserId,TokenHash,CreatedAtUtc,ExpiresAtUtc) VALUES(NEWID(),@id,@hash,SYSUTCDATETIME(),@expiry); UPDATE dbo.Users SET LastLoginUtc=SYSUTCDATETIME() WHERE UserID=@id",c);cmd.Parameters.AddWithValue("@id",user.UserId);cmd.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=TokenHash(token);cmd.Parameters.AddWithValue("@expiry",expiry);await cmd.ExecuteNonQueryAsync(ct);return new SessionDto(token,expiry,user with{LastLoginUtc=DateTime.UtcNow});}
    private static byte[] TokenHash(string token)=>SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
    private static string HashPassword(string password){var salt=RandomNumberGenerator.GetBytes(16);var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,210000,HashAlgorithmName.SHA256,32);return $"PBKDF2$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";}
    private static bool Verify(string password,string? hash,string legacy){if(string.IsNullOrEmpty(hash))return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(password),System.Text.Encoding.UTF8.GetBytes(legacy));var p=hash.Split('$');if(p.Length!=4||p[0]!="PBKDF2"||!int.TryParse(p[1],out var iterations))return false;var actual=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(p[2]),iterations,HashAlgorithmName.SHA256,32);return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(p[3]));}
}