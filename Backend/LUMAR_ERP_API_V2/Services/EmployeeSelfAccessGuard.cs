using LUMAR_ERP_API_V2.DTOs.Auth;

namespace LUMAR_ERP_API_V2.Services;

public static class EmployeeSelfAccessGuard
{
    public static int? ResolveCurrentEmployeeId(CurrentUserDto? user)
    {
        if (user is null) return null;
        if (!string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase)) return null;
        return user.EmployeeId;
    }

    public static bool IsAllowed(int? currentEmployeeId, int? requestedEmployeeId)
    {
        if (!currentEmployeeId.HasValue || !requestedEmployeeId.HasValue) return false;
        return currentEmployeeId.Value == requestedEmployeeId.Value;
    }
}
