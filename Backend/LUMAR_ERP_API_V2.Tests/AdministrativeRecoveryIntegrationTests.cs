using System.Data;
using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class AdministrativeRecoveryIntegrationTests
{
    [Fact]
    public async Task CustomerAdministrativeRecovery_EndToEnd_PreservesIdentityAndReplacesPassword()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"customer_recovery_{suffix}";
        var oldPassword = "OldPassword!2026";
        var newPassword = "NewPassword!2026";

        var adminUserId = await CreateAdminUserAsync(connectionString, $"admin_recovery_{suffix}", "AdminP@ssword1");
        var adminToken = CreateToken("admin-recovery-");
        await InsertSessionAsync(connectionString, adminUserId, adminToken, DateTime.UtcNow.AddHours(8));

        var customerId = await CreateCustomerAsync(connectionString, suffix, $"Customer Recovery {suffix}");
        var userId = await CreateUserAsync(connectionString, username, "Customer", oldPassword, $"Customer User {suffix}");
        var mobileAccountId = await CreateMobileAccountAsync(connectionString, username, "Customer", customerId, userId, oldPassword);
        var oldToken = CreateToken("customer-token-");
        await InsertSessionAsync(connectionString, userId, oldToken, DateTime.UtcNow.AddHours(8));

        try
        {
            var beforeUserId = await ScalarAsync<int>(connectionString, "SELECT UserID FROM dbo.Users WHERE Username = @username", ("@username", username));
            var beforeMobileAccountId = await ScalarAsync<int>(connectionString, "SELECT MobileAccountId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var beforeCustomerId = await ScalarAsync<int>(connectionString, "SELECT CustomerId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var beforeCustomerCode = await ScalarAsync<string>(connectionString, "SELECT CustomerCode FROM dbo.Customers WHERE CustomerID = @customerId", ("@customerId", customerId));

            Assert.Equal(userId, beforeUserId);
            Assert.Equal(mobileAccountId, beforeMobileAccountId);
            Assert.Equal(customerId, beforeCustomerId);
            Assert.Equal(beforeCustomerId, customerId);

            var start = await repository.BeginAdministrativeRecoveryAsync(adminToken, username, CancellationToken.None);
            Assert.True(start.Success);
            Assert.False(string.IsNullOrWhiteSpace(start.ResetToken));

            var complete = await repository.CompleteAdministrativeRecoveryAsync(start.ResetToken!, newPassword, newPassword, CancellationToken.None);
            Assert.True(complete.Success, complete.Message);

            var oldPasswordRejected = await repository.LoginAsync(new LoginDto(username, oldPassword, false), CancellationToken.None);
            Assert.Null(oldPasswordRejected);

            var newPasswordAccepted = await repository.LoginAsync(new LoginDto(username, newPassword, false), CancellationToken.None);
            Assert.NotNull(newPasswordAccepted);

            var currentUserWithOldToken = await repository.GetCurrentUserAsync(oldToken, CancellationToken.None);
            Assert.Null(currentUserWithOldToken);

            var oldSessionRevoked = await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM dbo.UserSessions WHERE UserId = @userId AND TokenHash = @tokenHash AND RevokedAtUtc IS NOT NULL", ("@userId", userId), ("@tokenHash", HashToken(oldToken)));
            Assert.Equal(1, oldSessionRevoked);

            var userCountAfter = await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.Users WHERE Username = @username", ("@username", username));
            var mobileCountAfter = await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var customerCountAfter = await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.Customers WHERE CustomerID = @customerId", ("@customerId", customerId));

            Assert.Equal(1, userCountAfter);
            Assert.Equal(1, mobileCountAfter);
            Assert.Equal(1, customerCountAfter);

            var afterUserId = await ScalarAsync<int>(connectionString, "SELECT UserID FROM dbo.Users WHERE Username = @username", ("@username", username));
            var afterMobileAccountId = await ScalarAsync<int>(connectionString, "SELECT MobileAccountId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var afterCustomerId = await ScalarAsync<int>(connectionString, "SELECT CustomerId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var afterCustomerCode = await ScalarAsync<string>(connectionString, "SELECT CustomerCode FROM dbo.Customers WHERE CustomerID = @customerId", ("@customerId", customerId));

            Assert.Equal(beforeUserId, afterUserId);
            Assert.Equal(beforeMobileAccountId, afterMobileAccountId);
            Assert.Equal(beforeCustomerId, afterCustomerId);
            Assert.Equal(beforeCustomerCode, afterCustomerCode);
            Assert.Equal(username, await ScalarAsync<string>(connectionString, "SELECT Username FROM dbo.Users WHERE UserID = @userId", ("@userId", userId)));
            Assert.Equal(0, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.Users WHERE Username = @username AND UserID <> @userId", ("@username", username), ("@userId", userId)));
        }
        finally
        {
            await CleanupCustomerRecoveryFixtureAsync(connectionString, customerId, mobileAccountId, userId, adminUserId);
        }
    }

    [Fact]
    public async Task EmployeeAdministrativeRecovery_EndToEnd_PreservesIdentityAndReplacesPassword()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"employee_recovery_{suffix}";
        var oldPassword = "OldEmployee!2026";
        var newPassword = "NewEmployee!2026";

        var adminUserId = await CreateAdminUserAsync(connectionString, $"admin_emp_recovery_{suffix}", "AdminP@ssword1");
        var adminToken = CreateToken("admin-employee-");
        await InsertSessionAsync(connectionString, adminUserId, adminToken, DateTime.UtcNow.AddHours(8));

        var employeeId = await CreateEmployeeAsync(connectionString, suffix, $"Employee Recovery {suffix}");
        var userId = await CreateUserAsync(connectionString, username, "Employee", oldPassword, $"Employee User {suffix}");
        var mobileAccountId = await CreateMobileAccountAsync(connectionString, username, "Employee", null, userId, oldPassword, employeeId: employeeId);
        var oldToken = CreateToken("employee-token-");
        await InsertSessionAsync(connectionString, userId, oldToken, DateTime.UtcNow.AddHours(8));

        try
        {
            var beforeEmployeeId = await ScalarAsync<int>(connectionString, "SELECT EmployeeId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var beforeEmployeeCode = await ScalarAsync<string>(connectionString, "SELECT EmployeeCode FROM dbo.Employees WHERE EmployeeID = @employeeId", ("@employeeId", employeeId));

            var start = await repository.BeginAdministrativeRecoveryAsync(adminToken, username, CancellationToken.None);
            Assert.True(start.Success);

            var complete = await repository.CompleteAdministrativeRecoveryAsync(start.ResetToken!, newPassword, newPassword, CancellationToken.None);
            Assert.True(complete.Success, complete.Message);

            Assert.Null(await repository.LoginAsync(new LoginDto(username, oldPassword, false), CancellationToken.None));
            var login = await repository.LoginAsync(new LoginDto(username, newPassword, false), CancellationToken.None);
            Assert.NotNull(login);
            Assert.Null(await repository.GetCurrentUserAsync(oldToken, CancellationToken.None));

            var afterEmployeeId = await ScalarAsync<int>(connectionString, "SELECT EmployeeId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var afterEmployeeCode = await ScalarAsync<string>(connectionString, "SELECT EmployeeCode FROM dbo.Employees WHERE EmployeeID = @employeeId", ("@employeeId", employeeId));

            Assert.Equal(beforeEmployeeId, afterEmployeeId);
            Assert.Equal(beforeEmployeeCode, afterEmployeeCode);
            Assert.Equal(beforeEmployeeId, employeeId);
            Assert.Equal(username, await ScalarAsync<string>(connectionString, "SELECT Username FROM dbo.Users WHERE UserID = @userId", ("@userId", userId)));
        }
        finally
        {
            await CleanupEmployeeRecoveryFixtureAsync(connectionString, employeeId, mobileAccountId, userId, adminUserId);
        }
    }

    [Fact]
    public async Task SupplierAdministrativeRecovery_EndToEnd_PreservesIdentityAndReplacesPassword()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"supplier_recovery_{suffix}";
        var oldPassword = "OldSupplier!2026";
        var newPassword = "NewSupplier!2026";

        var adminUserId = await CreateAdminUserAsync(connectionString, $"admin_supplier_recovery_{suffix}", "AdminP@ssword1");
        var adminToken = CreateToken("admin-supplier-");
        await InsertSessionAsync(connectionString, adminUserId, adminToken, DateTime.UtcNow.AddHours(8));

        var supplierId = await CreateSupplierAsync(connectionString, suffix, $"Supplier Recovery {suffix}");
        var userId = await CreateUserAsync(connectionString, username, "Supplier", oldPassword, $"Supplier User {suffix}");
        var mobileAccountId = await CreateMobileAccountAsync(connectionString, username, "Supplier", null, userId, oldPassword, supplierId: supplierId);
        var oldToken = CreateToken("supplier-token-");
        await InsertSessionAsync(connectionString, userId, oldToken, DateTime.UtcNow.AddHours(8));

        try
        {
            var beforeSupplierId = await ScalarAsync<int>(connectionString, "SELECT SupplierId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var beforeSupplierCode = await ScalarAsync<string>(connectionString, "SELECT SupplierCode FROM dbo.Suppliers WHERE SupplierID = @supplierId", ("@supplierId", supplierId));

            var start = await repository.BeginAdministrativeRecoveryAsync(adminToken, username, CancellationToken.None);
            Assert.True(start.Success);

            var complete = await repository.CompleteAdministrativeRecoveryAsync(start.ResetToken!, newPassword, newPassword, CancellationToken.None);
            Assert.True(complete.Success, complete.Message);

            Assert.Null(await repository.LoginAsync(new LoginDto(username, oldPassword, false), CancellationToken.None));
            var login = await repository.LoginAsync(new LoginDto(username, newPassword, false), CancellationToken.None);
            Assert.NotNull(login);
            Assert.Null(await repository.GetCurrentUserAsync(oldToken, CancellationToken.None));

            var afterSupplierId = await ScalarAsync<int>(connectionString, "SELECT SupplierId FROM dbo.MobileAccounts WHERE Username = @username", ("@username", username));
            var afterSupplierCode = await ScalarAsync<string>(connectionString, "SELECT SupplierCode FROM dbo.Suppliers WHERE SupplierID = @supplierId", ("@supplierId", supplierId));

            Assert.Equal(beforeSupplierId, afterSupplierId);
            Assert.Equal(beforeSupplierCode, afterSupplierCode);
            Assert.Equal(beforeSupplierId, supplierId);
            Assert.Equal(username, await ScalarAsync<string>(connectionString, "SELECT Username FROM dbo.Users WHERE UserID = @userId", ("@userId", userId)));
        }
        finally
        {
            await CleanupSupplierRecoveryFixtureAsync(connectionString, supplierId, mobileAccountId, userId, adminUserId);
        }
    }

    [Fact]
    public async Task AdministrativeRecovery_SecurityNegativeCases_RejectUnauthorizedExpiredReplayAndCrossAccount()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"security_recovery_{suffix}";
        var otherUsername = $"security_recovery_other_{suffix}";
        var password = "SecurityPassword!2026";
        var newPassword = "NewSecurityPassword!2026";

        var adminUserId = await CreateAdminUserAsync(connectionString, $"admin_secure_{suffix}", "AdminP@ssword1");
        var adminToken = CreateToken("admin-security-");
        await InsertSessionAsync(connectionString, adminUserId, adminToken, DateTime.UtcNow.AddHours(8));

        var targetUserId = await CreateUserAsync(connectionString, username, "Customer", password, $"Security Target {suffix}");
        var customerId = await CreateCustomerAsync(connectionString, suffix, $"Security Customer {suffix}");
        var mobileAccountId = await CreateMobileAccountAsync(connectionString, username, "Customer", customerId, targetUserId, password);

        var otherUserId = await CreateUserAsync(connectionString, otherUsername, "Customer", password, $"Other Security {suffix}");
        var otherCustomerId = await CreateCustomerAsync(connectionString, $"{suffix}2", $"Other Security Customer {suffix}");
        var otherMobileAccountId = await CreateMobileAccountAsync(connectionString, otherUsername, "Customer", otherCustomerId, otherUserId, password);

        try
        {
            var unauthorized = await repository.BeginAdministrativeRecoveryAsync("not-a-valid-admin-session", username, CancellationToken.None);
            Assert.False(unauthorized.Success);

            var start = await repository.BeginAdministrativeRecoveryAsync(adminToken, username, CancellationToken.None);
            Assert.True(start.Success);

            var expiredToken = CreateToken("expired-");
            await InsertSessionAsync(connectionString, targetUserId, expiredToken, DateTime.UtcNow.AddMinutes(-5), reason: "PasswordRecovery");
            var expiredResult = await repository.CompleteAdministrativeRecoveryAsync(expiredToken, newPassword, newPassword, CancellationToken.None);
            Assert.False(expiredResult.Success);

            var replay = await repository.CompleteAdministrativeRecoveryAsync(start.ResetToken!, newPassword, newPassword, CancellationToken.None);
            Assert.True(replay.Success);
            var replaySecond = await repository.CompleteAdministrativeRecoveryAsync(start.ResetToken!, "AnotherPassword!2026", "AnotherPassword!2026", CancellationToken.None);
            Assert.False(replaySecond.Success);

            var originalOtherPasswordLogin = await repository.LoginAsync(new LoginDto(otherUsername, password, false), CancellationToken.None);
            Assert.NotNull(originalOtherPasswordLogin);
            Assert.Null(await repository.LoginAsync(new LoginDto(otherUsername, newPassword, false), CancellationToken.None));

            var matchingUser = await repository.LoginAsync(new LoginDto(username, newPassword, false), CancellationToken.None);
            Assert.NotNull(matchingUser);

            var initialBoundCheck = await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.MobileAccounts WHERE Username = @username AND CustomerId = @customerId", ("@username", username), ("@customerId", customerId));
            Assert.Equal(1, initialBoundCheck);
            Assert.Equal(1, await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.MobileAccounts WHERE Username = @username AND CustomerId = @customerId", ("@username", otherUsername), ("@customerId", otherCustomerId)));
            Assert.True(await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM dbo.UserSessions WHERE UserId = @userId AND RevocationReason = 'PasswordReset'", ("@userId", targetUserId)) >= 1);
        }
        finally
        {
            await CleanupSecurityRecoveryFixtureAsync(connectionString, customerId, mobileAccountId, userId: targetUserId, otherCustomerId, otherMobileAccountId, otherUserId, adminUserId);
        }
    }

    private static AuthRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var customerRepository = new CustomerRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
        return new AuthRepository(new OperationalSqlConnectionFactory(options), customerRepository);
    }

    private static async Task<int> CreateAdminUserAsync(string connectionString, string username, string password)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "INSERT INTO dbo.Users (Username, FullName, UserRole, IsActive, PasswordHash, UserPassword, SecurityStamp, LastLoginUtc) OUTPUT INSERTED.UserID VALUES (@username, @fullName, 'Admin', 1, @hash, '', NEWID(), SYSUTCDATETIME());",
            connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@fullName", $"Admin {username}");
        command.Parameters.AddWithValue("@hash", HashPassword(password));
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateCustomerAsync(string connectionString, string suffix, string customerName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, Address, Notes, IsActive, ParentCustomerId, RelationshipType) OUTPUT INSERTED.CustomerID VALUES (@code, @name, @phone, NULL, NULL, 1, NULL, N'Family');",
            connection);
        var code = $"RC{Guid.NewGuid():N}"[..10];
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@name", customerName);
        command.Parameters.AddWithValue("@phone", $"777{DateTime.UtcNow:HHmmssfff}");
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateEmployeeAsync(string connectionString, string suffix, string fullName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var department = new SqlCommand("INSERT INTO dbo.Departments (DepartmentCode, DepartmentName, IsActive, CreatedAt) OUTPUT INSERTED.DepartmentId VALUES (@code, @name, 1, SYSUTCDATETIME());", connection);
        department.Parameters.AddWithValue("@code", $"D{suffix}");
        department.Parameters.AddWithValue("@name", $"Department {suffix}");
        var departmentId = (int)(await department.ExecuteScalarAsync());

        await using var command = new SqlCommand(
            "INSERT INTO dbo.Employees (EmployeeCode, EmployeeName, FullName, DepartmentId, BasicSalary, SalaryType, HireDate, Status, IsActive, CreatedAt, PieceWageRate, OvertimeHourlyRate) OUTPUT INSERTED.EmployeeID VALUES (@code, @employeeName, @fullName, @departmentId, 1000, N'BasicSalary', CAST(SYSUTCDATETIME() AS date), N'Active', 1, SYSUTCDATETIME(), 0, 0);",
            connection);
        command.Parameters.AddWithValue("@code", $"EMP-{suffix}");
        command.Parameters.AddWithValue("@employeeName", fullName);
        command.Parameters.AddWithValue("@fullName", fullName);
        command.Parameters.AddWithValue("@departmentId", departmentId);
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateSupplierAsync(string connectionString, string suffix, string supplierName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.Suppliers (SupplierCode, SupplierName, Phone, Email, Address, IsActive, CreatedAt) OUTPUT INSERTED.SupplierId VALUES (@code, @name, @phone, NULL, NULL, 1, SYSUTCDATETIME());",
            connection);
        command.Parameters.AddWithValue("@code", $"SUP-{suffix}");
        command.Parameters.AddWithValue("@name", supplierName);
        command.Parameters.AddWithValue("@phone", $"750{DateTime.UtcNow:HHmmssfff}");
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateUserAsync(string connectionString, string username, string role, string password, string fullName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.Users (Username, FullName, UserRole, IsActive, PasswordHash, UserPassword, SecurityStamp, LastLoginUtc) OUTPUT INSERTED.UserID VALUES (@username, @fullName, @role, 1, @hash, '', NEWID(), SYSUTCDATETIME());",
            connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@fullName", fullName);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@hash", HashPassword(password));
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateMobileAccountAsync(string connectionString, string username, string accountType, int? customerId, int userId, string password, int? employeeId = null, int? supplierId = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, EmployeeId, SupplierId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, LockoutEndUtc, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc) OUTPUT INSERTED.MobileAccountId VALUES (@accountType, @customerId, @employeeId, @supplierId, @username, @normalizedUsername, @hash, 1, 0, NULL, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME());",
            connection);
        command.Parameters.AddWithValue("@accountType", accountType);
        command.Parameters.AddWithValue("@customerId", (object?)customerId ?? DBNull.Value);
        command.Parameters.AddWithValue("@employeeId", (object?)employeeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@supplierId", (object?)supplierId ?? DBNull.Value);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@normalizedUsername", username.ToUpperInvariant());
        command.Parameters.AddWithValue("@hash", HashPassword(password));
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task InsertSessionAsync(string connectionString, int userId, string token, DateTime expiresAtUtc, string? reason = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.UserSessions (SessionId, UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, RevocationReason) VALUES (NEWID(), @userId, @tokenHash, SYSUTCDATETIME(), @expiresAtUtc, NULL, @reason);",
            connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.Add("@tokenHash", SqlDbType.VarBinary, 32).Value = HashToken(token);
        command.Parameters.AddWithValue("@expiresAtUtc", expiresAtUtc);
        command.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupCustomerRecoveryFixtureAsync(string connectionString, int customerId, int mobileAccountId, int userId, int adminUserId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var sessionDelete = new SqlCommand("DELETE FROM dbo.UserSessions WHERE UserId IN (@userId, @adminUserId);", connection))
        {
            sessionDelete.Parameters.AddWithValue("@userId", userId);
            sessionDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await sessionDelete.ExecuteNonQueryAsync();
        }

        await using (var mobileDelete = new SqlCommand("DELETE FROM dbo.MobileAccounts WHERE MobileAccountId = @mobileAccountId;", connection))
        {
            mobileDelete.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
            await mobileDelete.ExecuteNonQueryAsync();
        }

        await using (var userDelete = new SqlCommand("DELETE FROM dbo.Users WHERE UserID IN (@userId, @adminUserId);", connection))
        {
            userDelete.Parameters.AddWithValue("@userId", userId);
            userDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await userDelete.ExecuteNonQueryAsync();
        }

        await using (var customerDelete = new SqlCommand("DELETE FROM dbo.Customers WHERE CustomerID = @customerId;", connection))
        {
            customerDelete.Parameters.AddWithValue("@customerId", customerId);
            await customerDelete.ExecuteNonQueryAsync();
        }
    }

    private static async Task CleanupEmployeeRecoveryFixtureAsync(string connectionString, int employeeId, int mobileAccountId, int userId, int adminUserId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var sessionDelete = new SqlCommand("DELETE FROM dbo.UserSessions WHERE UserId IN (@userId, @adminUserId);", connection))
        {
            sessionDelete.Parameters.AddWithValue("@userId", userId);
            sessionDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await sessionDelete.ExecuteNonQueryAsync();
        }

        await using (var mobileDelete = new SqlCommand("DELETE FROM dbo.MobileAccounts WHERE MobileAccountId = @mobileAccountId;", connection))
        {
            mobileDelete.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
            await mobileDelete.ExecuteNonQueryAsync();
        }

        await using (var userDelete = new SqlCommand("DELETE FROM dbo.Users WHERE UserID IN (@userId, @adminUserId);", connection))
        {
            userDelete.Parameters.AddWithValue("@userId", userId);
            userDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await userDelete.ExecuteNonQueryAsync();
        }

        await using (var employeeDelete = new SqlCommand("DELETE FROM dbo.Employees WHERE EmployeeID = @employeeId;", connection))
        {
            employeeDelete.Parameters.AddWithValue("@employeeId", employeeId);
            await employeeDelete.ExecuteNonQueryAsync();
        }
    }

    private static async Task CleanupSupplierRecoveryFixtureAsync(string connectionString, int supplierId, int mobileAccountId, int userId, int adminUserId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var sessionDelete = new SqlCommand("DELETE FROM dbo.UserSessions WHERE UserId IN (@userId, @adminUserId);", connection))
        {
            sessionDelete.Parameters.AddWithValue("@userId", userId);
            sessionDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await sessionDelete.ExecuteNonQueryAsync();
        }

        await using (var mobileDelete = new SqlCommand("DELETE FROM dbo.MobileAccounts WHERE MobileAccountId = @mobileAccountId;", connection))
        {
            mobileDelete.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
            await mobileDelete.ExecuteNonQueryAsync();
        }

        await using (var userDelete = new SqlCommand("DELETE FROM dbo.Users WHERE UserID IN (@userId, @adminUserId);", connection))
        {
            userDelete.Parameters.AddWithValue("@userId", userId);
            userDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await userDelete.ExecuteNonQueryAsync();
        }

        await using (var supplierDelete = new SqlCommand("DELETE FROM dbo.Suppliers WHERE SupplierID = @supplierId;", connection))
        {
            supplierDelete.Parameters.AddWithValue("@supplierId", supplierId);
            await supplierDelete.ExecuteNonQueryAsync();
        }
    }

    private static async Task CleanupSecurityRecoveryFixtureAsync(string connectionString, int customerId, int mobileAccountId, int userId, int otherCustomerId, int otherMobileAccountId, int otherUserId, int adminUserId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var sessionDelete = new SqlCommand("DELETE FROM dbo.UserSessions WHERE UserId IN (@userId, @otherUserId, @adminUserId);", connection))
        {
            sessionDelete.Parameters.AddWithValue("@userId", userId);
            sessionDelete.Parameters.AddWithValue("@otherUserId", otherUserId);
            sessionDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await sessionDelete.ExecuteNonQueryAsync();
        }

        await using (var mobileDelete = new SqlCommand("DELETE FROM dbo.MobileAccounts WHERE MobileAccountId IN (@mobileAccountId, @otherMobileAccountId);", connection))
        {
            mobileDelete.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
            mobileDelete.Parameters.AddWithValue("@otherMobileAccountId", otherMobileAccountId);
            await mobileDelete.ExecuteNonQueryAsync();
        }

        await using (var userDelete = new SqlCommand("DELETE FROM dbo.Users WHERE UserID IN (@userId, @otherUserId, @adminUserId);", connection))
        {
            userDelete.Parameters.AddWithValue("@userId", userId);
            userDelete.Parameters.AddWithValue("@otherUserId", otherUserId);
            userDelete.Parameters.AddWithValue("@adminUserId", adminUserId);
            await userDelete.ExecuteNonQueryAsync();
        }

        await using (var customerDelete = new SqlCommand("DELETE FROM dbo.Customers WHERE CustomerID IN (@customerId, @otherCustomerId);", connection))
        {
            customerDelete.Parameters.AddWithValue("@customerId", customerId);
            customerDelete.Parameters.AddWithValue("@otherCustomerId", otherCustomerId);
            await customerDelete.ExecuteNonQueryAsync();
        }
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
            return default!;

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static string CreateToken(string prefix)
    {
        return prefix + Guid.NewGuid().ToString("N");
    }

    private static byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static string GetValidationConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AdministrativeRecovery integration tests require LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }
}
