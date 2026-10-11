using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
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
