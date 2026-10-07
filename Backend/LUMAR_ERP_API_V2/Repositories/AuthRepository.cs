using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class AuthRepository(OperationalSqlConnectionFactory connections) : IAuthRepository
{
    private sealed record MobileAccountIdentity(
        string AccountType,
        int? CustomerId,
        int? EmployeeId,
        int? SupplierId);

    public async Task<SessionDto?> LoginAsync(LoginDto login, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT u.UserID, u.Username, u.UserPassword, u.PasswordHash, u.FullName, u.UserRole, u.IsActive, u.LastLoginUtc FROM dbo.Users u WHERE u.Username = @username",
            connection);
        command.Parameters.AddWithValue("@username", login.Username.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var isActive = reader.GetBoolean(6);
        if (!isActive)
            return null;

        var userId = reader.GetInt32(0);
        var username = reader.GetString(1);
        var legacyPassword = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        var passwordHash = reader.IsDBNull(3) ? null : reader.GetString(3);
        var fullName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        var role = reader.IsDBNull(5) ? null : reader.GetString(5);
        DateTime? lastLoginUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7);

        if (!PasswordsMatch(login.Password, passwordHash, legacyPassword))
            return null;

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            await using var update = new SqlCommand(
                "UPDATE dbo.Users SET PasswordHash = @hash, UserPassword = '' WHERE UserID = @id",
                connection);
            update.Parameters.AddWithValue("@hash", HashPassword(login.Password));
            update.Parameters.AddWithValue("@id", userId);
            await update.ExecuteNonQueryAsync(ct);
        }

        var isAdmin = IsAdministrativeRole(role);
        var mobileAccount = await ResolveMobileAccountAsync(connection, userId, ct);
        if (mobileAccount is null && !isAdmin)
            return null;

        var currentUser = new CurrentUserDto(
            userId,
            username,
            fullName,
            role,
            true,
            lastLoginUtc,
            mobileAccount?.AccountType,
            mobileAccount?.CustomerId,
            mobileAccount?.EmployeeId,
            mobileAccount?.SupplierId);
        return await CreateSessionAsync(connection, currentUser, login.RememberMe, ct);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(string token, CancellationToken ct)
    {
        return await GetUserAsync(token, ct);
    }

    public async Task<bool> LogoutAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'Logout' WHERE TokenHash = @hash AND RevokedAtUtc IS NULL",
            connection);
        command.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(token);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<SessionDto?> ChangeUsernameAsync(string token, ChangeUsernameDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(request.Username))
            return null;

        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null || !await VerifyCurrentPasswordAsync(currentUser.UserId, request.CurrentPassword, ct))
            return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        try
        {
            await using var command = new SqlCommand(
                "UPDATE dbo.Users SET Username = @username WHERE UserID = @id",
                connection);
            command.Parameters.AddWithValue("@username", request.Username.Trim());
            command.Parameters.AddWithValue("@id", currentUser.UserId);
            var rows = await command.ExecuteNonQueryAsync(ct);
            if (rows != 1)
                return null;

            await LogoutAsync(token, ct);
            var updatedUser = currentUser with { Username = request.Username.Trim() };
            return await CreateSessionAsync(connection, updatedUser, true, ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new ArgumentException("Username already exists.", ex);
        }
    }

    public async Task<bool> ChangePasswordAsync(string token, ChangePasswordDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return false;

        if (request.NewPassword != request.ConfirmPassword || request.NewPassword == request.CurrentPassword)
            return false;

        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null || !await VerifyCurrentPasswordAsync(currentUser.UserId, request.CurrentPassword, ct))
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "UPDATE dbo.Users SET PasswordHash = @hash, UserPassword = '', SecurityStamp = NEWID() WHERE UserID = @id; UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'PasswordChanged' WHERE UserId = @id AND RevokedAtUtc IS NULL",
            connection);
        command.Parameters.AddWithValue("@hash", HashPassword(request.NewPassword));
        command.Parameters.AddWithValue("@id", currentUser.UserId);
        await command.ExecuteNonQueryAsync(ct);

        return true;
    }

    public async Task<RefreshResultDto?> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        var tokenHash = TokenHash(refreshToken);
        await using var query = new SqlCommand(
            "SELECT s.UserId, s.ExpiresAtUtc FROM dbo.UserSessions s JOIN dbo.Users u ON u.UserID = s.UserId WHERE s.TokenHash = @hash AND s.RevokedAtUtc IS NULL AND s.ExpiresAtUtc > SYSUTCDATETIME() AND u.IsActive = 1",
            connection);
        query.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = tokenHash;

        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var userId = reader.GetInt32(0);
        var newToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var newExpiry = DateTime.UtcNow.AddHours(8);
        await reader.CloseAsync();

        await using var rotate = new SqlCommand(
            "UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'TokenRefreshed' WHERE TokenHash = @old AND RevokedAtUtc IS NULL; INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc) VALUES (NEWID(), @uid, @newhash, SYSUTCDATETIME(), @newexp)",
            connection);
        rotate.Parameters.Add("@old", System.Data.SqlDbType.VarBinary, 32).Value = tokenHash;
        rotate.Parameters.AddWithValue("@uid", userId);
        rotate.Parameters.Add("@newhash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(newToken);
        rotate.Parameters.AddWithValue("@newexp", newExpiry);
        await rotate.ExecuteNonQueryAsync(ct);

        return new RefreshResultDto(newToken, newExpiry, null);
    }

    public async Task<bool> LogoutAllAsync(string token, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null)
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(
            "UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'LogoutAll' WHERE UserId = @id AND RevokedAtUtc IS NULL",
            connection);
        command.Parameters.AddWithValue("@id", currentUser.UserId);
        await command.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<RecoveryStartResultDto> RecoveryStartAsync(string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new RecoveryStartResultDto(false, "اسم المستخدم مطلوب.");

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT 1 FROM dbo.Users WHERE Username = @username AND IsActive = 1",
            connection);
        command.Parameters.AddWithValue("@username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        var exists = await reader.ReadAsync(ct);
        return new RecoveryStartResultDto(exists, exists ? "إذا كان الحساب موجودًا، سيتم إرسال تعليمات الاستعادة." : "إذا كان الحساب موجودًا، سيتم إرسال تعليمات الاستعادة.");
    }

    public async Task<RecoveryVerifyResultDto> RecoveryVerifyAsync(string username, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(code))
            return new RecoveryVerifyResultDto(false, "بيانات الاستعادة غير كاملة.", null);

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT 1 FROM dbo.Users WHERE Username = @username AND IsActive = 1",
            connection);
        command.Parameters.AddWithValue("@username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        var exists = await reader.ReadAsync(ct);
        if (!exists)
            return new RecoveryVerifyResultDto(false, "المستخدم غير موجود أو غير نشط.", null);

        return new RecoveryVerifyResultDto(true, "تم التحقق بنجاح.", Guid.NewGuid().ToString("N"));
    }

    public async Task<RecoveryCompleteResultDto> RecoveryCompleteAsync(string resetToken, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resetToken) || string.IsNullOrWhiteSpace(newPassword))
            return new RecoveryCompleteResultDto(false, "بيانات الاستعادة غير كاملة.");

        if (newPassword.Length < 8)
            return new RecoveryCompleteResultDto(false, "كلمة المرور قصيرة.");

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT UserID FROM dbo.UserSessions WHERE TokenHash = @hash AND RevokedAtUtc IS NULL AND ExpiresAtUtc > SYSUTCDATETIME()",
            connection);
        command.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(resetToken);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new RecoveryCompleteResultDto(false, "رمز الاستعادة غير صالح.");

        var userId = reader.GetInt32(0);
        await reader.CloseAsync();

        await using var update = new SqlCommand(
            "UPDATE dbo.Users SET PasswordHash = @hash, UserPassword = '', SecurityStamp = NEWID() WHERE UserID = @id",
            connection);
        update.Parameters.AddWithValue("@hash", HashPassword(newPassword));
        update.Parameters.AddWithValue("@id", userId);
        await update.ExecuteNonQueryAsync(ct);

        return new RecoveryCompleteResultDto(true, "تمت استعادة كلمة المرور بنجاح.");
    }

    public async Task<List<SessionInfoDto>> GetSessionsAsync(string token, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null)
            return [];

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT SessionId, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevocationReason, DeviceInfo, CASE WHEN SessionId = (SELECT TOP 1 SessionId FROM dbo.UserSessions WHERE UserId = @id AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC) THEN 1 ELSE 0 END FROM dbo.UserSessions WHERE UserId = @id ORDER BY CreatedAtUtc DESC",
            connection);
        command.Parameters.AddWithValue("@id", currentUser.UserId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var sessions = new List<SessionInfoDto>();
        while (await reader.ReadAsync(ct))
        {
            sessions.Add(new SessionInfoDto(
                reader.GetGuid(0),
                reader.GetDateTime(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetBoolean(6)));
        }

        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(string token, Guid sessionId, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null)
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'RevokedByUser' WHERE UserId = @userId AND SessionId = @sessionId AND RevokedAtUtc IS NULL",
            connection);
        command.Parameters.AddWithValue("@userId", currentUser.UserId);
        command.Parameters.AddWithValue("@sessionId", sessionId);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<AccountTypeDto?> GetAccountTypeAsync(string token, CancellationToken ct)
    {
        var currentUser = await GetUserAsync(token, ct);
        if (currentUser is null)
            return null;

        return new AccountTypeDto(currentUser.AccountType ?? string.Empty, currentUser.CustomerId, currentUser.EmployeeId, currentUser.SupplierId);
    }

    private async Task<CurrentUserDto?> GetUserAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT u.UserID, u.Username, u.FullName, u.UserRole, u.IsActive, u.LastLoginUtc FROM dbo.UserSessions s JOIN dbo.Users u ON u.UserID = s.UserId WHERE s.TokenHash = @hash AND s.RevokedAtUtc IS NULL AND s.ExpiresAtUtc > SYSUTCDATETIME() AND u.IsActive = 1",
            connection);
        command.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(token);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var userId = reader.GetInt32(0);
        var username = reader.GetString(1);
        var fullName = reader.GetString(2);
        var role = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateTime? lastLoginUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
        var mobileAccount = await ResolveMobileAccountAsync(connection, userId, ct);
        if (mobileAccount is null && !IsAdministrativeRole(role))
            return null;

        return new CurrentUserDto(
            userId,
            username,
            fullName,
            role,
            true,
            lastLoginUtc,
            mobileAccount?.AccountType,
            mobileAccount?.CustomerId,
            mobileAccount?.EmployeeId,
            mobileAccount?.SupplierId);
    }

    internal static bool IsAdministrativeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var normalized = role.Trim();
        return normalized.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("System Administrator", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Admin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Administrator", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> VerifyCurrentPasswordAsync(int userId, string password, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(
            "SELECT UserPassword, PasswordHash FROM dbo.Users WHERE UserID = @id",
            connection);
        command.Parameters.AddWithValue("@id", userId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return false;

        var legacy = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var hash = reader.IsDBNull(1) ? null : reader.GetString(1);
        return PasswordsMatch(password, hash, legacy);
    }

    private static async Task<SessionDto> CreateSessionAsync(SqlConnection connection, CurrentUserDto user, bool rememberMe, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.Add(rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8));

        await using var command = new SqlCommand(
            "INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc) VALUES (NEWID(), @id, @hash, SYSUTCDATETIME(), @expiry); UPDATE dbo.Users SET LastLoginUtc = SYSUTCDATETIME() WHERE UserID = @id",
            connection);
        command.Parameters.AddWithValue("@id", user.UserId);
        command.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(token);
        command.Parameters.AddWithValue("@expiry", expiresAt);
        await command.ExecuteNonQueryAsync(ct);

        return new SessionDto(token, expiresAt, user with { LastLoginUtc = DateTime.UtcNow });
    }

    private async Task<MobileAccountIdentity?> ResolveMobileAccountAsync(SqlConnection connection, int userId, CancellationToken ct)
    {
        if (userId <= 0)
            return null;

        await using var resolve = new SqlCommand(
            "SELECT TOP 1 AccountType, CustomerId, EmployeeId, SupplierId, IsActive FROM dbo.MobileAccounts WHERE UserId = @userId AND IsActive = 1 ORDER BY MobileAccountId",
            connection);
        resolve.Parameters.AddWithValue("@userId", userId);

        await using var reader = await resolve.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var accountType = reader.IsDBNull(0) ? null : reader.GetString(0);
        int? customerId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        int? employeeId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        int? supplierId = reader.IsDBNull(3) ? null : reader.GetInt32(3);

        if (string.IsNullOrWhiteSpace(accountType) || !MobileAccountIdentityValidator.IsValid(accountType, customerId, employeeId, supplierId))
            return null;

        return new MobileAccountIdentity(accountType, customerId, employeeId, supplierId);
    }

    private static byte[] TokenHash(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool PasswordsMatch(string password, string? storedHash, string legacyPassword)
    {
        if (string.IsNullOrEmpty(storedHash))
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(password),
                Encoding.UTF8.GetBytes(legacyPassword));

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations))
            return false;

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(parts[2]),
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromBase64String(parts[3]));
    }
}
