using LUMAR_ERP_API_V2.DTOs.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Services;

public static class EmployeeScannerRequestGuard
{
    public static ActionResult? ValidateEmployeeAccount(CurrentUserDto? user)
    {
        if (user is null)
            return new UnauthorizedResult();

        if (!string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return new ObjectResult("This endpoint is only for employee accounts.") { StatusCode = StatusCodes.Status403Forbidden };

        if (!user.EmployeeId.HasValue)
            return new ObjectResult("Employee account is not mapped to a valid employee record.") { StatusCode = StatusCodes.Status403Forbidden };

        return null;
    }
}
