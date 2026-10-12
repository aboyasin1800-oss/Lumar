using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeSelfServiceSecurityTests
{
    [Fact]
    public void Employee_accounts_only_get_their_own_employee_id()
    {
        var user = new CurrentUserDto(101, "emp01", "Employee One", "Employee", true, DateTime.UtcNow.AddDays(-1), "Employee", null, 7, null);

        Assert.Equal(7, EmployeeSelfAccessGuard.ResolveCurrentEmployeeId(user));
    }

    [Fact]
    public void Employee_cannot_access_another_employee_record()
    {
        var currentEmployeeId = 7;
        var requestedEmployeeId = 9;

        Assert.False(EmployeeSelfAccessGuard.IsAllowed(currentEmployeeId, requestedEmployeeId));
    }

    [Fact]
    public void Non_employee_account_is_not_treated_as_employee_identity()
    {
        var user = new CurrentUserDto(202, "admin01", "Admin User", "Admin", true, DateTime.UtcNow.AddDays(-1), "Admin", null, null, null);

        Assert.Null(EmployeeSelfAccessGuard.ResolveCurrentEmployeeId(user));
    }

    [Fact]
    public void Employee_code_cannot_bypass_current_user_identity_validation()
    {
        var customerUser = new CurrentUserDto(303, "cust01", "Customer", "Customer", true, DateTime.UtcNow.AddDays(-1), "Customer", 5, null, null);
        var employeeUser = new CurrentUserDto(101, "emp01", "Employee One", "Employee", true, DateTime.UtcNow.AddDays(-1), "Employee", null, 7, null);

        Assert.Null(EmployeeSelfAccessGuard.ResolveCurrentEmployeeId(customerUser));
        Assert.Equal(7, EmployeeSelfAccessGuard.ResolveCurrentEmployeeId(employeeUser));
        Assert.False(EmployeeSelfAccessGuard.IsAllowed(7, 5));
    }
}
