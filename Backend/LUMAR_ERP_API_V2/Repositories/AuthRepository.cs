using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class AuthRepository(OperationalSqlConnectionFactory connections, ICustomerRepository customerRepository) : IAuthRepository
{
    private sealed record MobileAccountIdentity(
        int MobileAccountId,
        string AccountType,
        int? CustomerId,
        int? EmployeeId,
        int? SupplierId,
        IReadOnlyList<int> ManagedCustomerIds)
    {
        public MobileAccountIdentity(string accountType, int? customerId, int? employeeId, int? supplierId)
            : this(0, accountType, customerId, employeeId, supplierId, Array.Empty<int>())
        {
        }
    }

    public async Task<SessionDto> RegisterMobileCustomerAsync(MobileCustomerRegistrationDto request, CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentException("Registration request is required.");

        if (!ValidateMobileCustomerRegistrationRequest(request.Username, request.Password, request.ConfirmPassword, request.FullName, request.CustomerName, request.PhoneNumber))
            throw new ArgumentException("Invalid customer registration request.");

        var username = NormalizeUsername(request.Username);
        var normalizedUsername = username.ToUpperInvariant();
        var fullName = request.FullName.Trim();
        var customerName = request.CustomerName.Trim();
        var phoneNumber = NormalizeCustomerPhone(request.PhoneNumber);

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            await using (var usernameCheck = new SqlCommand("SELECT TOP (1) UserID FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username", connection, transaction))
            {
                usernameCheck.Parameters.AddWithValue("@username", username);
                if (await usernameCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("Username already exists.");
            }

            await using (var mobileUsernameCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username OR NormalizedUsername = @normalizedUsername", connection, transaction))
            {
                mobileUsernameCheck.Parameters.AddWithValue("@username", username);
                mobileUsernameCheck.Parameters.AddWithValue("@normalizedUsername", normalizedUsername);
                if (await mobileUsernameCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("Username already exists.");
            }

            var officialCustomer = await customerRepository.CreateWithReferralInTransactionAsync(
                connection,
                transaction,
                new CreateCustomerWithReferralDto
                {
                    CustomerName = customerName,
                    PhoneNumber = phoneNumber,
                    Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    ReferrerCustomerId = request.ReferrerCustomerId,
                    RelationshipType = string.IsNullOrWhiteSpace(request.RelationshipType) ? null : request.RelationshipType.Trim()
                },
                ct);

            var customerId = officialCustomer.CustomerId;
            var passwordHash = HashPassword(request.Password);
            int userId;
            const string createUserSql = "INSERT INTO dbo.Users (Username, FullName, UserRole, IsActive, PasswordHash, UserPassword, SecurityStamp, LastLoginUtc) OUTPUT INSERTED.UserID VALUES (@username, @fullName, 'Customer', 1, @passwordHash, '', NEWID(), SYSUTCDATETIME())";
            await using (var createUser = new SqlCommand(createUserSql, connection, transaction))
            {
                createUser.Parameters.AddWithValue("@username", username);
                createUser.Parameters.AddWithValue("@fullName", fullName);
                createUser.Parameters.AddWithValue("@passwordHash", passwordHash);
                userId = (int)(await createUser.ExecuteScalarAsync(ct))!;
            }

            const string createMobileAccountSql = "INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, EmployeeId, SupplierId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, LockoutEndUtc, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc) VALUES (@accountType, @customerId, NULL, NULL, @username, @normalizedUsername, @passwordHash, 1, 0, NULL, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME())";
            await using (var createMobileAccount = new SqlCommand(createMobileAccountSql, connection, transaction))
            {
                createMobileAccount.Parameters.AddWithValue("@accountType", "Customer");
                createMobileAccount.Parameters.AddWithValue("@customerId", customerId);
                createMobileAccount.Parameters.AddWithValue("@username", username);
                createMobileAccount.Parameters.AddWithValue("@normalizedUsername", normalizedUsername);
                createMobileAccount.Parameters.AddWithValue("@passwordHash", passwordHash);
                await createMobileAccount.ExecuteNonQueryAsync(ct);
            }

            var currentUser = new CurrentUserDto(userId, username, fullName, "Customer", true, DateTime.UtcNow, "Customer", customerId, null, null);
            var session = await CreateSessionAsync(connection, currentUser, request.RememberMe, transaction, ct);
            await transaction.CommitAsync(ct);
            return session;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

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
        MobileAccountIdentity? mobileAccount = null;
        if (!isAdmin)
        {
            mobileAccount = await ResolveMobileAccountAsync(connection, username, ct);
            if (mobileAccount is null)
                return null;
        }

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
            mobileAccount?.SupplierId)
        {
            ManagedCustomerIds = mobileAccount?.ManagedCustomerIds ?? Array.Empty<int>()
        };
        return await CreateSessionAsync(connection, currentUser, login.RememberMe, null, ct);
    }

    public async Task<EmployeeProvisioningResult?> ProvisionOfficialEmployeeAccountAsync(EmployeeProvisioningRequest request, CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentException("Provisioning request is required.");

        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim();
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new ArgumentException("EmployeeCode is required.");

        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("رقم الهاتف مطلوب للتحقق من هوية الموظف.");

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            int employeeId;
            string? officialEmployeeCode;
            string fullName;
            string? employeePhone;
            bool isActive;

            await using (var employeeQuery = new SqlCommand(
                "SELECT EmployeeID, EmployeeCode, FullName, PhoneNumber, IsActive FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeCode = @employeeCode",
                connection,
                transaction))
            {
                employeeQuery.Parameters.AddWithValue("@employeeCode", employeeCode);

                await using var employeeReader = await employeeQuery.ExecuteReaderAsync(ct);
                if (!await employeeReader.ReadAsync(ct))
                    throw new KeyNotFoundException("Employee code was not found.");

                employeeId = employeeReader.GetInt32(0);
                officialEmployeeCode = employeeReader.IsDBNull(1) ? null : employeeReader.GetString(1);
                fullName = employeeReader.IsDBNull(2) ? string.Empty : employeeReader.GetString(2);
                employeePhone = employeeReader.IsDBNull(3) ? null : employeeReader.GetString(3);
                isActive = employeeReader.GetBoolean(4);
            }

            if (!isActive)
                throw new ArgumentException("Employee is inactive.");

            if (!ValidateEmployeeProvisioningPhone(phoneNumber, employeePhone))
                throw new ArgumentException("رقم الهاتف لا يطابق سجل الموظف الرسمي.");

            var username = ResolveEmployeeProvisioningUsername(officialEmployeeCode ?? employeeCode, request.PreferredUsername, fullName);
            if (!ValidateEmployeeProvisioningRequest(employeeId, officialEmployeeCode ?? employeeCode, username))
                throw new ArgumentException("Employee provisioning request is invalid.");

            await using (var userCheck = new SqlCommand("SELECT TOP (1) UserID FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username", connection, transaction))
            {
                userCheck.Parameters.AddWithValue("@username", username);
                if (await userCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("Username already exists.");
            }

            await using (var mobileUsernameCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username OR NormalizedUsername = @normalizedUsername", connection, transaction))
            {
                mobileUsernameCheck.Parameters.AddWithValue("@username", username);
                mobileUsernameCheck.Parameters.AddWithValue("@normalizedUsername", username.ToUpperInvariant());
                if (await mobileUsernameCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("Username already exists.");
            }

            await using (var mobileAccountCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeId = @employeeId AND AccountType = 'Employee'", connection, transaction))
            {
                mobileAccountCheck.Parameters.AddWithValue("@employeeId", employeeId);
                if (await mobileAccountCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("لدى هذا الموظف حساب مسبقاً. استخدم تسجيل الدخول.");
            }

            var normalizedUsername = username.ToUpperInvariant();
            var userId = await CreateUserRecordAsync(connection, transaction, username, fullName, "Employee", ct);
            var mobileAccountId = await CreateEmployeeMobileAccountAsync(connection, transaction, employeeId, username, normalizedUsername, ct);

            var setupToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var setupExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            await using (var setupTokenInsert = new SqlCommand(
                "INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, RevocationReason) VALUES (NEWID(), @userId, @tokenHash, SYSUTCDATETIME(), @expiresAt, 'PasswordSetup')",
                connection,
                transaction))
            {
                setupTokenInsert.Parameters.AddWithValue("@userId", userId);
                setupTokenInsert.Parameters.Add("@tokenHash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(setupToken);
                setupTokenInsert.Parameters.AddWithValue("@expiresAt", setupExpiresAtUtc);
                await setupTokenInsert.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new EmployeeProvisioningResult(employeeId, userId, mobileAccountId, username, "Employee", setupToken, setupExpiresAtUtc, true);
        }
        catch
        {
            try
            {
                if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
            catch (InvalidOperationException)
            {
                // The transaction may already be completed by the underlying DB operation.
                // Preserve the original exception and do not mask it with a second rollback failure.
            }

            throw;
        }
    }

    public async Task<SupplierProvisioningResult?> ProvisionOfficialSupplierAccountAsync(SupplierProvisioningRequest request, CancellationToken ct)
    {
        if (request is null)
            throw new ArgumentException("Provisioning request is required.");

        var supplierCode = string.IsNullOrWhiteSpace(request.SupplierCode) ? null : request.SupplierCode.Trim();
        if (string.IsNullOrWhiteSpace(supplierCode))
            throw new ArgumentException("SupplierCode is required.");

        var phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("رقم الهاتف مطلوب للتحقق من هوية المورد.");

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        try
        {
            int supplierId;
            string? officialSupplierCode;
            string officialSupplierName;
            string? officialPhone;
            bool isActive;

            await using (var supplierQuery = new SqlCommand(
                "SELECT SupplierId, SupplierCode, SupplierName, Phone, IsActive FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK) WHERE SupplierCode = @supplierCode",
                connection,
                transaction))
            {
                supplierQuery.Parameters.AddWithValue("@supplierCode", supplierCode);

                await using var supplierReader = await supplierQuery.ExecuteReaderAsync(ct);
                if (!await supplierReader.ReadAsync(ct))
                    throw new KeyNotFoundException("Supplier code was not found.");

                supplierId = supplierReader.GetInt32(0);
                officialSupplierCode = supplierReader.IsDBNull(1) ? null : supplierReader.GetString(1);
                officialSupplierName = supplierReader.IsDBNull(2) ? string.Empty : supplierReader.GetString(2);
                officialPhone = supplierReader.IsDBNull(3) ? null : supplierReader.GetString(3);
                isActive = supplierReader.GetBoolean(4);
            }

            if (!isActive)
                throw new ArgumentException("حساب المورد غير نشط في المؤسسة.");

            if (!ValidateSupplierProvisioningPhone(phoneNumber, officialPhone))
                throw new ArgumentException("رقم الهاتف لا يطابق سجل المورد الرسمي.");

            var username = ResolveSupplierProvisioningUsername(officialSupplierCode ?? supplierCode, request.PreferredUsername, officialSupplierName);
            if (!ValidateSupplierProvisioningRequest(supplierId, officialSupplierCode ?? supplierCode, username))
                throw new ArgumentException("Supplier provisioning request is invalid.");

            await using (var userCheck = new SqlCommand("SELECT TOP (1) UserID FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username", connection, transaction))
            {
                userCheck.Parameters.AddWithValue("@username", username);
                if (await userCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("اسم المستخدم مستخدم مسبقاً. اختر اسماً آخر.");
            }

            await using (var mobileUsernameCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username OR NormalizedUsername = @normalizedUsername", connection, transaction))
            {
                mobileUsernameCheck.Parameters.AddWithValue("@username", username);
                mobileUsernameCheck.Parameters.AddWithValue("@normalizedUsername", username.ToUpperInvariant());
                if (await mobileUsernameCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("اسم المستخدم مستخدم مسبقاً. اختر اسماً آخر.");
            }

            await using (var existingSupplierAccount = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = @supplierId AND AccountType = 'Supplier'", connection, transaction))
            {
                existingSupplierAccount.Parameters.AddWithValue("@supplierId", supplierId);
                if (await existingSupplierAccount.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("لدى هذا المورد حساب مسبقاً. استخدم تسجيل الدخول.");
            }

            var userId = await CreateUserRecordAsync(connection, transaction, username, officialSupplierName, "Supplier", ct);
            var mobileAccountId = await CreateSupplierMobileAccountAsync(connection, transaction, supplierId, username, username.ToUpperInvariant(), ct);

            var setupToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var setupExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            await using (var setupTokenInsert = new SqlCommand(
                "INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, RevocationReason) VALUES (NEWID(), @userId, @tokenHash, SYSUTCDATETIME(), @expiresAt, 'PasswordSetup')",
                connection,
                transaction))
            {
                setupTokenInsert.Parameters.AddWithValue("@userId", userId);
                setupTokenInsert.Parameters.Add("@tokenHash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(setupToken);
                setupTokenInsert.Parameters.AddWithValue("@expiresAt", setupExpiresAtUtc);
                await setupTokenInsert.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new SupplierProvisioningResult(supplierId, userId, mobileAccountId, username, "Supplier", setupToken, setupExpiresAtUtc, true);
        }
        catch
        {
            try
            {
                if (transaction.Connection is not null && transaction.Connection.State == System.Data.ConnectionState.Open)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
    }

    public async Task<bool> CompleteEmployeePasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct)
        => await CompletePasswordSetupAsync(setupToken, newPassword, confirmPassword, "Employee", ct);

    public async Task<bool> CompleteSupplierPasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, CancellationToken ct)
        => await CompletePasswordSetupAsync(setupToken, newPassword, confirmPassword, "Supplier", ct);

    private async Task<bool> CompletePasswordSetupAsync(string setupToken, string newPassword, string confirmPassword, string expectedRole, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(setupToken) || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 || !string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        var tokenHash = TokenHash(setupToken);
        await using (var query = new SqlCommand(
            PasswordSetupJoinSql,
            connection))
        {
            query.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = tokenHash;

            await using var reader = await query.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return false;

            var userId = reader.GetInt32(0);
            var role = reader.IsDBNull(3) ? null : reader.GetString(3);
            var accountType = reader.IsDBNull(4) ? null : reader.GetString(4);
            var employeeId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
            var supplierId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);

            if (!string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(accountType, expectedRole, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (expectedRole.Equals("Employee", StringComparison.OrdinalIgnoreCase) && employeeId is null)
                return false;

            if (expectedRole.Equals("Supplier", StringComparison.OrdinalIgnoreCase) && supplierId is null)
                return false;

            await using var update = new SqlCommand(
                "UPDATE dbo.Users SET PasswordHash = @passwordHash, UserPassword = '', SecurityStamp = NEWID() WHERE UserID = @id; UPDATE dbo.UserSessions SET RevokedAtUtc = SYSUTCDATETIME(), RevocationReason = 'PasswordSetupCompleted' WHERE TokenHash = @tokenHash AND RevokedAtUtc IS NULL",
                connection);
            update.Parameters.AddWithValue("@passwordHash", HashPassword(newPassword));
            update.Parameters.AddWithValue("@id", userId);
            update.Parameters.Add("@tokenHash", System.Data.SqlDbType.VarBinary, 32).Value = tokenHash;

            await update.ExecuteNonQueryAsync(ct);

            return true;
        }
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
            return await CreateSessionAsync(connection, updatedUser, true, null, ct);
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
        var isAdmin = IsAdministrativeRole(role);
        MobileAccountIdentity? mobileAccount = null;
        if (!isAdmin)
        {
            mobileAccount = await ResolveMobileAccountAsync(connection, username, ct);
            if (mobileAccount is null)
                return null;
        }

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
            mobileAccount?.SupplierId)
        {
            ManagedCustomerIds = mobileAccount?.ManagedCustomerIds ?? Array.Empty<int>()
        };
    }

    internal static bool IsAdministrativeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var normalized = role.Trim();
        return string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "System Administrator", StringComparison.OrdinalIgnoreCase);
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

    internal static SqlCommand BuildSessionCommand(SqlConnection connection, SqlTransaction? transaction, CurrentUserDto user, bool rememberMe, string token)
    {
        var expiresAt = DateTime.UtcNow.Add(rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8));
        var sql = "INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc) VALUES (NEWID(), @id, @hash, SYSUTCDATETIME(), @expiry); UPDATE dbo.Users SET LastLoginUtc = SYSUTCDATETIME() WHERE UserID = @id";

        var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("@id", user.UserId);
        command.Parameters.Add("@hash", System.Data.SqlDbType.VarBinary, 32).Value = TokenHash(token);
        command.Parameters.AddWithValue("@expiry", expiresAt);
        return command;
    }

    private static async Task<SessionDto> CreateSessionAsync(SqlConnection connection, CurrentUserDto user, bool rememberMe, SqlTransaction? transaction, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var command = BuildSessionCommand(connection, transaction, user, rememberMe, token);
        await command.ExecuteNonQueryAsync(ct);

        var expiresAt = DateTime.UtcNow.Add(rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8));
        return new SessionDto(token, expiresAt, user with { LastLoginUtc = DateTime.UtcNow });
    }

    internal static readonly string MobileAccountLookupSql =
        @"SELECT TOP 1 MobileAccountId, AccountType, CustomerId, EmployeeId, SupplierId, IsActive
          FROM dbo.MobileAccounts
          WHERE (Username = @username OR NormalizedUsername = @username OR NormalizedUsername = @normalizedUsername)
            AND IsActive = 1
          ORDER BY MobileAccountId";

    private async Task<MobileAccountIdentity?> ResolveMobileAccountAsync(SqlConnection connection, string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var normalizedUsername = username.Trim();
        await using var resolve = new SqlCommand(MobileAccountLookupSql, connection);
        resolve.Parameters.AddWithValue("@username", normalizedUsername);
        resolve.Parameters.AddWithValue("@normalizedUsername", normalizedUsername.ToUpperInvariant());

        await using var reader = await resolve.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var mobileAccountId = reader.GetInt32(0);
        var accountType = reader.IsDBNull(1) ? null : reader.GetString(1);
        int? customerId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        int? employeeId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        int? supplierId = reader.IsDBNull(4) ? null : reader.GetInt32(4);

        if (string.IsNullOrWhiteSpace(accountType) || !MobileAccountIdentityValidator.IsValid(accountType, customerId, employeeId, supplierId))
            return null;

        var managedCustomerIds = await GetManagedCustomerIdsAsync(connection, mobileAccountId, ct);
        return new MobileAccountIdentity(mobileAccountId, accountType, customerId, employeeId, supplierId, managedCustomerIds);
    }

    private static async Task<IReadOnlyList<int>> GetManagedCustomerIdsAsync(SqlConnection connection, int mobileAccountId, CancellationToken ct)
    {
        var ids = new List<int>();
        await using var command = new SqlCommand(
            "SELECT CustomerId FROM dbo.MobileAccountCustomerAccess WITH (NOLOCK) WHERE MobileAccountId = @mobileAccountId AND AccessRole = N'Managed' AND IsActive = 1 AND (RevokedAtUtc IS NULL OR RevokedAtUtc > SYSUTCDATETIME()) ORDER BY CustomerId",
            connection);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var customerId = reader.GetInt32(0);
            if (customerId > 0)
                ids.Add(customerId);
        }

        return ids;
    }

    internal static string NormalizeUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.");

        return username.Trim().ToUpperInvariant();
    }

    internal static bool ValidateMobileCustomerRegistrationRequest(string? username, string? password, string? confirmPassword, string? fullName, string? customerName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        if (string.IsNullOrWhiteSpace(confirmPassword) || !string.Equals(password, confirmPassword, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrWhiteSpace(fullName))
            return false;

        if (string.IsNullOrWhiteSpace(customerName))
            return false;

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        return true;
    }

    private static string GenerateCustomerCode()
    {
        var stamp = DateTime.UtcNow.ToString("yyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var suffix = RandomNumberGenerator.GetInt32(100, 999);
        return $"C{stamp}{suffix}";
    }

    private static string? NormalizeCustomerPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var cleaned = Regex.Replace(phone.Trim(), @"[\s\-()]+", string.Empty);
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return null;

        if (digits.StartsWith("00967", StringComparison.Ordinal))
            return digits.Substring(5);

        if (digits.StartsWith("967", StringComparison.Ordinal))
            return digits.Substring(3);

        if (digits.StartsWith("+967", StringComparison.Ordinal))
            return digits.Substring(4);

        return digits;
    }

    private static async Task<int> CreateUserRecordAsync(SqlConnection connection, SqlTransaction transaction, string username, string fullName, string role, CancellationToken ct)
    {
        const string createUserSql = "INSERT INTO dbo.Users (Username, FullName, UserRole, IsActive, PasswordHash, UserPassword, SecurityStamp, LastLoginUtc) OUTPUT INSERTED.UserID VALUES (@username, @fullName, @role, 1, '', '', NEWID(), SYSUTCDATETIME())";
        await using var createUser = new SqlCommand(createUserSql, connection, transaction);
        createUser.Parameters.AddWithValue("@username", username);
        createUser.Parameters.AddWithValue("@fullName", fullName);
        createUser.Parameters.AddWithValue("@role", role);
        return (int)(await createUser.ExecuteScalarAsync(ct))!;
    }

    internal const string EmployeeMobileAccountInsertSql =
        "INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, EmployeeId, SupplierId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, LockoutEndUtc, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc) OUTPUT INSERTED.MobileAccountId VALUES (@accountType, NULL, @employeeId, NULL, @username, @normalizedUsername, '', 1, 0, NULL, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME())";

    internal const string PasswordSetupJoinSql =
        "SELECT s.UserId, u.Username, u.FullName, u.UserRole, ma.AccountType, ma.EmployeeId, ma.CustomerId, ma.SupplierId FROM dbo.UserSessions s JOIN dbo.Users u ON u.UserID = s.UserId LEFT JOIN dbo.MobileAccounts ma ON (ma.Username = u.Username OR ma.NormalizedUsername = u.Username) WHERE s.TokenHash = @hash AND s.RevokedAtUtc IS NULL AND s.ExpiresAtUtc > SYSUTCDATETIME() AND s.RevocationReason = 'PasswordSetup' AND u.IsActive = 1";

    internal const string EmployeePasswordSetupJoinSql = PasswordSetupJoinSql;

    private static async Task<int> CreateEmployeeMobileAccountAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string username, string normalizedUsername, CancellationToken ct)
    {
        await using var createMobileAccount = new SqlCommand(EmployeeMobileAccountInsertSql, connection, transaction);
        createMobileAccount.Parameters.AddWithValue("@accountType", "Employee");
        createMobileAccount.Parameters.AddWithValue("@employeeId", employeeId);
        createMobileAccount.Parameters.AddWithValue("@username", username);
        createMobileAccount.Parameters.AddWithValue("@normalizedUsername", normalizedUsername);
        return (int)(await createMobileAccount.ExecuteScalarAsync(ct))!;
    }

    private static async Task<int> CreateSupplierMobileAccountAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, string username, string normalizedUsername, CancellationToken ct)
    {
        const string insertSql = "INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, EmployeeId, SupplierId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, LockoutEndUtc, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc) OUTPUT INSERTED.MobileAccountId VALUES (@accountType, NULL, NULL, @supplierId, @username, @normalizedUsername, '', 1, 0, NULL, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME())";
        await using var createMobileAccount = new SqlCommand(insertSql, connection, transaction);
        createMobileAccount.Parameters.AddWithValue("@accountType", "Supplier");
        createMobileAccount.Parameters.AddWithValue("@supplierId", supplierId);
        createMobileAccount.Parameters.AddWithValue("@username", username);
        createMobileAccount.Parameters.AddWithValue("@normalizedUsername", normalizedUsername);
        return (int)(await createMobileAccount.ExecuteScalarAsync(ct))!;
    }

    internal static string ResolveEmployeeProvisioningUsername(string? employeeCode, string? preferredUsername, string? officialFullName = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredUsername))
            return NormalizeEmployeeProvisioningUsername(preferredUsername);

        if (!string.IsNullOrWhiteSpace(officialFullName))
            return DeriveEmployeeProvisioningUsernameFromFullName(officialFullName);

        throw new ArgumentException("The employee username could not be resolved from the official full name.");
    }

    internal static bool ValidateEmployeeProvisioningRequest(int employeeId, string? employeeCode, string? username)
    {
        if (employeeId <= 0 || string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(username))
            return false;

        try
        {
            var normalizedUsername = NormalizeEmployeeProvisioningUsername(username);
            return normalizedUsername.Length >= 4;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static string ResolveSupplierProvisioningUsername(string? supplierCode, string? preferredUsername, string? officialSupplierName = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredUsername))
            return NormalizeSupplierProvisioningUsername(preferredUsername);

        if (!string.IsNullOrWhiteSpace(officialSupplierName))
            return NormalizeSupplierProvisioningUsername(officialSupplierName);

        throw new ArgumentException("The supplier username could not be resolved from the official supplier name.");
    }

    internal static bool ValidateSupplierProvisioningRequest(int supplierId, string? supplierCode, string? username)
    {
        if (supplierId <= 0 || string.IsNullOrWhiteSpace(supplierCode) || string.IsNullOrWhiteSpace(username))
            return false;

        try
        {
            var normalizedUsername = NormalizeSupplierProvisioningUsername(username);
            return normalizedUsername.Length >= 3;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool ValidateEmployeeProvisioningPhone(string? providedPhoneNumber, string? officialPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(providedPhoneNumber))
            return false;

        var normalizedProvidedPhone = NormalizePhoneForComparison(providedPhoneNumber);
        var normalizedOfficialPhone = NormalizePhoneForComparison(officialPhoneNumber);

        if (string.IsNullOrWhiteSpace(normalizedProvidedPhone) || string.IsNullOrWhiteSpace(normalizedOfficialPhone))
            return false;

        return string.Equals(normalizedProvidedPhone, normalizedOfficialPhone, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmployeeProvisioningUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Employee username is required.");

        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Employee username is required.");

        if (normalized.Length < 4)
            throw new ArgumentException("Employee username must contain at least four characters.");

        return normalized;
    }

    private static string NormalizeSupplierProvisioningUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Supplier username is required.");

        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Supplier username is required.");

        if (normalized.Length < 3)
            throw new ArgumentException("Supplier username must contain at least three characters.");

        return normalized;
    }

    internal static bool ValidateSupplierProvisioningPhone(string? providedPhoneNumber, string? officialPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(providedPhoneNumber))
            return false;

        var normalizedProvidedPhone = NormalizePhoneForComparison(providedPhoneNumber);
        var normalizedOfficialPhone = NormalizePhoneForComparison(officialPhoneNumber);

        if (string.IsNullOrWhiteSpace(normalizedProvidedPhone) || string.IsNullOrWhiteSpace(normalizedOfficialPhone))
            return false;

        return string.Equals(normalizedProvidedPhone, normalizedOfficialPhone, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePhoneForComparison(string? phone)
    {
        var normalized = NormalizeCustomerPhone(phone);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (normalized.StartsWith("0", StringComparison.Ordinal))
            normalized = normalized[1..];

        if (normalized.StartsWith("966", StringComparison.Ordinal))
            normalized = normalized[3..];

        return normalized;
    }

    private static string DeriveEmployeeProvisioningUsernameFromFullName(string fullName)
    {
        var parts = fullName
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
            throw new ArgumentException("Official employee full name is required to derive the default username.");

        return parts.Length == 1
            ? parts[0]
            : $"{parts[0]} {parts[^1]}";
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
