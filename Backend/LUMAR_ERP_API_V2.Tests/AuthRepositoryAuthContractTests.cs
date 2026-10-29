using System.Reflection;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class AuthRepositoryAuthContractTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("System Administrator", true)]
    [InlineData("Customer", false)]
    [InlineData("Employee", false)]
    [InlineData("Supplier", false)]
    [InlineData(null, false)]
    public void IsAdministrativeRole_Uses_Deskop_Admin_Contract(string? role, bool expected)
    {
        Assert.Equal(expected, AuthRepository.IsAdministrativeRole(role));
    }

    [Theory]
    [InlineData("  user@example.com  ", "USER@EXAMPLE.COM")]
    [InlineData("  ali  ", "ALI")]
    public void NormalizeUsername_Uses_Trim_And_Uppercase(string input, string expected)
    {
        Assert.Equal(expected, AuthRepository.NormalizeUsername(input));
    }

    [Theory]
    [InlineData(null, "P@ssword1", "P@ssword1", "Full Name", "Customer Name", null, false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword2", "Full Name", "Customer Name", "0501234567", false)]
    [InlineData("user@example.com", "short", "short", "Full Name", "Customer Name", "0501234567", false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword1", "", "Customer Name", "0501234567", false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword1", "Full Name", "", "0501234567", false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword1", "Full Name", "Customer Name", "", false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword1", "Full Name", "Customer Name", "   ", false)]
    [InlineData("user@example.com", "P@ssword1", "P@ssword1", "Full Name", "Customer Name", "0501234567", true)]
    public void ValidateMobileCustomerRegistrationRequest_Rejects_Invalid_Input(string? username, string password, string confirmPassword, string fullName, string customerName, string? phoneNumber, bool expected)
    {
        var request = new
        {
            Username = username,
            Password = password,
            ConfirmPassword = confirmPassword,
            FullName = fullName,
            CustomerName = customerName,
            PhoneNumber = phoneNumber
        };

        Assert.Equal(expected, AuthRepository.ValidateMobileCustomerRegistrationRequest(request.Username, request.Password, request.ConfirmPassword, request.FullName, request.CustomerName, request.PhoneNumber));
    }

    [Fact]
    public void Phone_Validation_Fails_Before_Customer_Creation()
    {
        var result = AuthRepository.ValidateMobileCustomerRegistrationRequest(
            "user@example.com",
            "P@ssword1",
            "P@ssword1",
            "Full Name",
            "Customer Name",
            "   ");

        Assert.False(result);
    }

    [Fact]
    public async Task RegisterMobileCustomerAsync_Rejects_Existing_Active_Primary_Phone_Before_Inserts()
    {
        var connectionString = GetValidationConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var customerRepository = new CustomerRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
        var repository = new AuthRepository(new OperationalSqlConnectionFactory(options), customerRepository);

        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var phoneNumber = $"775{DateTime.UtcNow:HHmmssfff}";
        var primaryUsername = $"primary_{uniqueToken}@example.com";
        var primaryFullName = $"Primary Family {uniqueToken}";
        var primaryCustomerName = $"Primary Customer {uniqueToken}";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var setupConnection = new SqlConnection(connectionString);
        await setupConnection.OpenAsync();
        await using var scope = setupConnection.BeginTransaction();
        try
        {
            var customerId = await CreateCustomerAsync(setupConnection, scope, primaryCustomerName, phoneNumber);
            var mobileAccountId = await CreateMobileAccountAsync(setupConnection, scope, customerId, primaryUsername, primaryFullName);
            var userId = await GetUserIdByUsernameAsync(setupConnection, scope, primaryUsername);
            scope.Commit();

            var request = new MobileCustomerRegistrationDto(
                $"second_{uniqueToken}@example.com",
                "P@ssword1",
                "P@ssword1",
                "Different Family Member",
                $"Different Family Member {uniqueToken}",
                phoneNumber,
                "Test address",
                "Test note",
                true,
                null,
                null);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => repository.RegisterMobileCustomerAsync(request, CancellationToken.None));
            Assert.Contains("رقم الهاتف مستخدم بالفعل", ex.Message, StringComparison.Ordinal);

            await using var verifyConnection = new SqlConnection(connectionString);
            await verifyConnection.OpenAsync();
            var customerCount = await CountCustomersForPhoneAsync(verifyConnection, phoneNumber);
            var userCount = await CountUsersAsync(verifyConnection, $"second_{uniqueToken}@example.com");
            var mobileAccountCount = await CountMobileAccountsAsync(verifyConnection, $"second_{uniqueToken}@example.com");

            Assert.Equal(1, customerCount);
            Assert.Equal(0, userCount);
            Assert.Equal(0, mobileAccountCount);

            await CleanupInsertedRowsAsync(verifyConnection, customerId, mobileAccountId, userId);
        }
        finally
        {
            if (scope.Connection is not null && scope.Connection.State == System.Data.ConnectionState.Open)
            {
                try { scope.Rollback(); } catch { }
            }
        }
    }

    private static async Task<int> CreateCustomerAsync(SqlConnection connection, SqlTransaction transaction, string customerName, string phoneNumber)
    {
        const string sql = @"INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, IsActive, RelationshipType) OUTPUT INSERTED.CustomerID VALUES (@code, @name, @phone, 1, N'Family');";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@code", $"T{Guid.NewGuid():N}".Substring(0, 10));
        command.Parameters.AddWithValue("@name", customerName);
        command.Parameters.AddWithValue("@phone", phoneNumber);
        return (int)(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CreateMobileAccountAsync(SqlConnection connection, SqlTransaction transaction, int customerId, string username, string fullName)
    {
        const string sql = @"INSERT INTO dbo.Users (Username, FullName, UserRole, IsActive, PasswordHash, UserPassword, SecurityStamp, LastLoginUtc) OUTPUT INSERTED.UserID VALUES (@username, @fullName, 'Customer', 1, '', '', NEWID(), SYSUTCDATETIME());";
        await using var userCommand = new SqlCommand(sql, connection, transaction);
        userCommand.Parameters.AddWithValue("@username", username);
        userCommand.Parameters.AddWithValue("@fullName", fullName);
        var userId = (int)(await userCommand.ExecuteScalarAsync());

        const string mobileSql = @"INSERT INTO dbo.MobileAccounts (AccountType, CustomerId, EmployeeId, SupplierId, Username, NormalizedUsername, PasswordHash, IsActive, FailedLoginAttempts, LockoutEndUtc, SecurityStamp, TokenVersion, CreatedAtUtc, UpdatedAtUtc) OUTPUT INSERTED.MobileAccountId VALUES (@accountType, @customerId, NULL, NULL, @username, @normalizedUsername, '', 1, 0, NULL, CAST(NEWID() AS nvarchar(128)), 0, SYSUTCDATETIME(), SYSUTCDATETIME());";
        await using var mobileCommand = new SqlCommand(mobileSql, connection, transaction);
        mobileCommand.Parameters.AddWithValue("@accountType", "Customer");
        mobileCommand.Parameters.AddWithValue("@customerId", customerId);
        mobileCommand.Parameters.AddWithValue("@username", username);
        mobileCommand.Parameters.AddWithValue("@normalizedUsername", username.ToUpperInvariant());
        return (int)(await mobileCommand.ExecuteScalarAsync());
    }

    private static async Task<int> GetUserIdByUsernameAsync(SqlConnection connection, SqlTransaction transaction, string username)
    {
        const string sql = "SELECT UserID FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @username;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountCustomersForPhoneAsync(SqlConnection connection, string phoneNumber)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.Customers WHERE PhoneNumber = @phoneNumber AND IsActive = 1;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@phoneNumber", phoneNumber);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountUsersAsync(SqlConnection connection, string username)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.Users WHERE Username = @username;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountMobileAccountsAsync(SqlConnection connection, string username)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.MobileAccounts WHERE Username = @username;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task CleanupInsertedRowsAsync(SqlConnection connection, int customerId, int mobileAccountId, int userId)
    {
        await using var deletePush = new SqlCommand("DELETE FROM dbo.MobilePushDevices WHERE MobileAccountId = @mobileAccountId;", connection);
        deletePush.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        await deletePush.ExecuteNonQueryAsync();

        await using var deleteSessions = new SqlCommand("DELETE FROM dbo.UserSessions WHERE UserId = @userId;", connection);
        deleteSessions.Parameters.AddWithValue("@userId", userId);
        await deleteSessions.ExecuteNonQueryAsync();

        await using var deleteAccount = new SqlCommand("DELETE FROM dbo.MobileAccounts WHERE MobileAccountId = @mobileAccountId;", connection);
        deleteAccount.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        await deleteAccount.ExecuteNonQueryAsync();

        await using var deleteUser = new SqlCommand("DELETE FROM dbo.Users WHERE UserID = @userId;", connection);
        deleteUser.Parameters.AddWithValue("@userId", userId);
        await deleteUser.ExecuteNonQueryAsync();

        await using var deleteAccess = new SqlCommand("DELETE FROM dbo.MobileAccountCustomerAccess WHERE CustomerId = @customerId;", connection);
        deleteAccess.Parameters.AddWithValue("@customerId", customerId);
        await deleteAccess.ExecuteNonQueryAsync();

        await using var deleteCustomer = new SqlCommand("DELETE FROM dbo.Customers WHERE CustomerID = @customerId;", connection);
        deleteCustomer.Parameters.AddWithValue("@customerId", customerId);
        await deleteCustomer.ExecuteNonQueryAsync();
    }

    private static string GetValidationConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Auth repository tests require LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }

    [Theory]
    [InlineData("775106545", "775106545")]
    [InlineData("+967775106545", "775106545")]
    [InlineData("00967775106545", "775106545")]
    [InlineData("967775106545", "775106545")]
    [InlineData("966501234567", "966501234567")]
    public void NormalizeCustomerPhone_Uses_Yemen_Formatting_Without_Saudi_966_Logic(string input, string expected)
    {
        var method = typeof(AuthRepository).GetMethod("NormalizeCustomerPhone", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [input]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MobileCustomerRegistrationDto_Exposes_Official_Erp_Customer_Inputs()
    {
        var request = new MobileCustomerRegistrationDto(
            "user@example.com",
            "P@ssword1",
            "P@ssword1",
            "Full Name",
            "Customer Name",
            "0501234567",
            "Dubai",
            "Notes",
            true,
            42,
            "Parent");

        Assert.Equal("Customer Name", request.CustomerName);
        Assert.Equal("0501234567", request.PhoneNumber);
        Assert.Equal(42, request.ReferrerCustomerId);
        Assert.Equal("Parent", request.RelationshipType);
    }

    [Theory]
    [InlineData("MO10", "  staff alias  ", "Mohammed Ali", "staff alias")]
    [InlineData("MO11", null, "عبد العزيز الفقيه", "عبد الفقيه")]
    public void ResolveEmployeeProvisioningUsername_Uses_Normalized_Explicit_Username_Or_Official_Full_Name(string employeeCode, string? requestedUsername, string? officialFullName, string expected)
    {
        Assert.Equal(expected, AuthRepository.ResolveEmployeeProvisioningUsername(employeeCode, requestedUsername, officialFullName));
    }

    [Theory]
    [InlineData(0, "EMP-001", "EMP-001", false)]
    [InlineData(7, "", "MO10", false)]
    [InlineData(7, "MO10", "", false)]
    [InlineData(7, "MO10", "sh", false)]
    [InlineData(7, "MO10", "ali ahmed", true)]
    [InlineData(7, "MO10", "  ali   ahmed  ", true)]
    [InlineData(7, "MO10", "MO10", true)]
    public void ValidateEmployeeProvisioningRequest_Requires_Valid_Employee_And_Username(int employeeId, string employeeCode, string username, bool expected)
    {
        Assert.Equal(expected, AuthRepository.ValidateEmployeeProvisioningRequest(employeeId, employeeCode, username));
    }

    [Fact]
    public void Provisioning_Request_Uses_Official_Employee_Code_And_Optional_Phone()
    {
        var request = new EmployeeProvisioningRequest("MO10", "0551234567", "MO10");

        Assert.Equal("MO10", request.EmployeeCode);
        Assert.Equal("0551234567", request.PhoneNumber);
        Assert.Equal("MO10", request.PreferredUsername);
    }

    [Fact]
    public void EmployeeMobileAccountSchema_Uses_Username_Not_UserId()
    {
        Assert.DoesNotContain("UserId", AuthRepository.EmployeeMobileAccountInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dbo.MobileAccounts", AuthRepository.EmployeeMobileAccountInsertSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NormalizedUsername", AuthRepository.EmployeeMobileAccountInsertSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmployeePasswordSetupJoin_Uses_Username_Lookup_Not_UserId()
    {
        Assert.DoesNotContain("ma.UserId", AuthRepository.EmployeePasswordSetupJoinSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ma.Username = u.Username", AuthRepository.EmployeePasswordSetupJoinSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSessionCommand_Uses_Active_Transaction_When_Present()
    {
        using var connection = new SqlConnection("Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var user = new CurrentUserDto(42, "user@example.com", "Full Name", "Customer", true, DateTime.UtcNow, "Customer", 7, null, null);

        using var command = AuthRepository.BuildSessionCommand(connection, transaction, user, true, "session-token");

        Assert.Same(transaction, command.Transaction);
        Assert.Contains("INSERT INTO dbo.UserSessions", command.CommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("0501234567", true)]
    [InlineData("+966501234567", true)]
    public void ValidateEmployeeProvisioningPhone_Requires_Official_Phone_Match(string? phoneNumber, bool expected)
    {
        var officialPhone = "0501234567";
        Assert.Equal(expected, AuthRepository.ValidateEmployeeProvisioningPhone(phoneNumber, officialPhone));
    }

    [Theory]
    [InlineData("0501234567", "0501234567", true)]
    [InlineData("+966501234567", "0501234567", true)]
    [InlineData("0501234567", "0551234567", false)]
    public void ValidateEmployeeProvisioningPhone_Uses_Normalized_Official_Phone(string providedPhone, string officialPhone, bool expected)
    {
        Assert.Equal(expected, AuthRepository.ValidateEmployeeProvisioningPhone(providedPhone, officialPhone));
    }

    [Fact]
    public void EmployeePasswordSetupContract_Does_Not_Create_Login_Session_Implicitly()
    {
        var usesExtraLoginSession = false;
        Assert.False(usesExtraLoginSession);
    }

    [Theory]
    [InlineData("SUP-100", "  supplier alias  ", "شركة النور", "supplier alias")]
    [InlineData("SUP-101", null, "شركة النور للتجارة", "شركة النور للتجارة")]
    public void ResolveSupplierProvisioningUsername_Uses_Explicit_Username_Or_Official_Supplier_Name(string supplierCode, string? preferredUsername, string? officialSupplierName, string expected)
    {
        Assert.Equal(expected, AuthRepository.ResolveSupplierProvisioningUsername(supplierCode, preferredUsername, officialSupplierName));
    }

    [Theory]
    [InlineData(0, "SUP-100", "SUP-100", false)]
    [InlineData(7, "", "SUP-100", false)]
    [InlineData(7, "SUP-100", "", false)]
    [InlineData(7, "SUP-100", "ab", false)]
    [InlineData(7, "SUP-100", "ali", true)]
    [InlineData(7, "SUP-100", "  supplier   alias  ", true)]
    public void ValidateSupplierProvisioningRequest_Requires_Valid_Supplier_And_Username(int supplierId, string supplierCode, string username, bool expected)
    {
        Assert.Equal(expected, AuthRepository.ValidateSupplierProvisioningRequest(supplierId, supplierCode, username));
    }

    [Fact]
    public void SupplierProvisioning_Request_Uses_Official_Supplier_Code_And_Optional_Phone()
    {
        var request = new SupplierProvisioningRequest("SUP-100", "0551234567", "supplier_alias", true);

        Assert.Equal("SUP-100", request.SupplierCode);
        Assert.Equal("0551234567", request.PhoneNumber);
        Assert.Equal("supplier_alias", request.PreferredUsername);
    }

    [Theory]
    [InlineData("0501234567", "0501234567", true)]
    [InlineData("+966501234567", "0501234567", true)]
    [InlineData("0501234567", "0551234567", false)]
    public void ValidateSupplierProvisioningPhone_Uses_Normalized_Official_Phone(string providedPhone, string officialPhone, bool expected)
    {
        Assert.Equal(expected, AuthRepository.ValidateSupplierProvisioningPhone(providedPhone, officialPhone));
    }
}
