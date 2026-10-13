using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CustomerOrderOwnershipGuardTests
{
    [Fact]
    public void Customer_can_access_only_their_own_orders()
    {
        var user = new CurrentUserDto(11, "cust01", "Customer One", "Customer", true, DateTime.UtcNow.AddDays(-1), "Customer", 42, null, null);

        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(user, 42));
        Assert.False(CustomerOrderOwnershipGuard.IsAllowed(user, 99));
    }

    [Fact]
    public void Employee_and_admin_accounts_remain_unrestricted()
    {
        var employee = new CurrentUserDto(21, "emp01", "Employee One", "Employee", true, DateTime.UtcNow.AddDays(-1), "Employee", null, 7, null);
        var admin = new CurrentUserDto(31, "admin01", "Admin User", "Admin", true, DateTime.UtcNow.AddDays(-1), "Admin", null, null, null);

        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(employee, 42));
        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(admin, 42));
    }

    [Fact]
    public void Guest_or_unmapped_customer_sessions_are_rejected()
    {
        Assert.False(CustomerOrderOwnershipGuard.IsAllowed(null, 42));

        var malformedCustomer = new CurrentUserDto(41, "cust02", "Customer Two", "Customer", true, DateTime.UtcNow.AddDays(-1), "Customer", null, null, null);
        Assert.False(CustomerOrderOwnershipGuard.IsAllowed(malformedCustomer, 42));
    }
}
