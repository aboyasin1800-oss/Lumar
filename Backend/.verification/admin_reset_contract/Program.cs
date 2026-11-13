using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var sb = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return sb.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
            {
                sb.Length--;
                Console.Write("\b \b");
            }
            continue;
        }

        if (key.Key == ConsoleKey.Escape)
        {
            Console.WriteLine();
            throw new InvalidOperationException("Password entry cancelled.");
        }

        sb.Append(key.KeyChar);
    }
}

var repoType = typeof(LUMAR_ERP_API_V2.Repositories.AuthRepository);
var hashMethod = repoType.GetMethod("HashPassword", BindingFlags.NonPublic | BindingFlags.Static);
var matchMethod = repoType.GetMethod("PasswordsMatch", BindingFlags.NonPublic | BindingFlags.Static);
if (hashMethod is null || matchMethod is null)
    throw new InvalidOperationException("AuthRepository contract methods not found.");

Console.WriteLine("Enter the new admin password in the hidden prompt below.");
var newPassword = ReadSecret("New password: ");
var confirmPassword = ReadSecret("Confirm password: ");

if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
    throw new InvalidOperationException("Password must be at least 8 characters.");
if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
    throw new InvalidOperationException("Passwords do not match.");

var generatedHash = (string)hashMethod.Invoke(null, new object[] { newPassword })!;
var localMatch = (bool)matchMethod.Invoke(null, new object[] { newPassword, generatedHash, string.Empty })!;
if (!localMatch)
    throw new InvalidOperationException("Local contract verification failed.");

Console.WriteLine("OFFICIAL_HASH_CONTRACT_REUSED=YES");
Console.WriteLine("LOCAL_PASSWORDS_MATCH_BEFORE_UPDATE=PASS");

var connString = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R3;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Timeout=30;";
await using var conn = new SqlConnection(connString);
await conn.OpenAsync();
await using var transaction =
    (Microsoft.Data.SqlClient.SqlTransaction)
    await conn.BeginTransactionAsync();

try
{
    var check = new SqlCommand(@"SELECT UserID, Username, UserRole, IsActive FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username", conn, transaction);
    check.Parameters.AddWithValue("@username", "admin");
    await using var reader = await check.ExecuteReaderAsync();
    var rows = new List<(int UserId, string Username, string Role, bool IsActive)>();
    while (await reader.ReadAsync())
    {
        rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
    }
    await reader.CloseAsync();

    if (rows.Count != 1)
        throw new InvalidOperationException($"Expected exactly 1 admin row; found {rows.Count}.");
    var admin = rows[0];
    if (!admin.IsActive)
        throw new InvalidOperationException("Admin account is not active.");
    if (!string.Equals(admin.Username, "admin", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Target username mismatch.");
    if (!(admin.Role == "Admin" || admin.Role == "System Administrator"))
        throw new InvalidOperationException($"Admin role invalid: {admin.Role}");

    var userUpdate = new SqlCommand(@"
UPDATE dbo.Users
SET PasswordHash = @hash,
    UserPassword = '',
    SecurityStamp = NEWID()
WHERE UserID = @userId AND Username = @username AND IsActive = 1;", conn, transaction);
    userUpdate.Parameters.AddWithValue("@hash", generatedHash);
    userUpdate.Parameters.AddWithValue("@userId", admin.UserId);
    userUpdate.Parameters.AddWithValue("@username", "admin");
    var userRowsUpdated = await userUpdate.ExecuteNonQueryAsync();
    if (userRowsUpdated != 1)
        throw new InvalidOperationException($"User update must affect exactly 1 row; affected {userRowsUpdated}.");

    var sessionRevoke = new SqlCommand(@"
UPDATE dbo.UserSessions
SET RevokedAtUtc = SYSUTCDATETIME(),
    RevocationReason = 'AdminPasswordReset'
WHERE UserId = @userId AND RevokedAtUtc IS NULL;", conn, transaction);
    sessionRevoke.Parameters.AddWithValue("@userId", admin.UserId);
    var revokedSessionCount = await sessionRevoke.ExecuteNonQueryAsync();

    await transaction.CommitAsync();

    var verify = new SqlCommand(@"SELECT LEN(COALESCE(UserPassword, '')) AS UserPasswordLength, CASE WHEN PasswordHash LIKE 'PBKDF2%' THEN 'PBKDF2' ELSE 'OTHER' END AS PasswordHashType FROM dbo.Users WHERE Username = @username; SELECT COUNT(*) FROM dbo.UserSessions WHERE UserId = @userId AND RevokedAtUtc IS NULL;", conn);
    verify.Parameters.AddWithValue("@username", "admin");
    verify.Parameters.AddWithValue("@userId", admin.UserId);
    await using var verifyReader = await verify.ExecuteReaderAsync();
    await verifyReader.ReadAsync();
    var userPasswordLength = verifyReader.GetInt32(0);
    var hashType = verifyReader.GetString(1);
    await verifyReader.NextResultAsync();
    await verifyReader.ReadAsync();
    var activeAfter = verifyReader.GetInt32(0);

    Console.WriteLine($"ROWS_UPDATED={userRowsUpdated}");
    Console.WriteLine($"USERPASSWORD_LENGTH={userPasswordLength}");
    Console.WriteLine($"PASSWORDHASH_TYPE={hashType}");
    Console.WriteLine("SECURITY_STAMP_ROTATED=YES");
    Console.WriteLine($"OLD_SESSIONS_REVOKED={revokedSessionCount}");
    Console.WriteLine($"ACTIVE_OLD_ADMIN_SESSIONS_AFTER={activeAfter}");
    Console.WriteLine("PASSWORD_EXPOSED=NO");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
