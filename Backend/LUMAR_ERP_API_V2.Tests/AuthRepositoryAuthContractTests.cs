using LUMAR_ERP_API_V2.Repositories;
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
}
