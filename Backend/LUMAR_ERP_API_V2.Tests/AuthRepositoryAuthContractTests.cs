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
}
