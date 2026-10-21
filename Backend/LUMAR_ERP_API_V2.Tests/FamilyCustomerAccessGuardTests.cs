using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FamilyCustomerAccessGuardTests
{
    [Fact]
    public void IsAllowed_AllowsPrimaryAndManagedCustomerIds()
    {
        var user = new CurrentUserDto(
            1,
            "family_user",
            "Family User",
            "Customer",
            true,
            null,
            "Customer",
            10,
            null,
            null)
        {
            ManagedCustomerIds = new[] { 20, 30 }
        };

        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(user, 10));
        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(user, 20));
        Assert.False(CustomerOrderOwnershipGuard.IsAllowed(user, 99));
    }
}
