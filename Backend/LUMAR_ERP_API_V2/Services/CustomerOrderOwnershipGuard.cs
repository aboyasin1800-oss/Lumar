using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;

namespace LUMAR_ERP_API_V2.Services;

public static class CustomerOrderOwnershipGuard
{
    public static bool IsCustomerAccount(CurrentUserDto? user)
        => user is not null
            && string.Equals(user.AccountType, "Customer", StringComparison.OrdinalIgnoreCase)
            && user.CustomerId is > 0;

    public static bool IsAllowed(CurrentUserDto? user, int? orderCustomerId)
    {
        if (user is null)
            return false;

        if (!string.Equals(user.AccountType, "Customer", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!orderCustomerId.HasValue || orderCustomerId.Value <= 0)
            return false;

        return user.HasCustomerAccess(orderCustomerId.Value);
    }

    public static bool IsCrossCustomerViolation(CurrentUserDto? user, int? orderCustomerId)
    {
        if (user is null)
            return false;

        if (!string.Equals(user.AccountType, "Customer", StringComparison.OrdinalIgnoreCase)
            || !orderCustomerId.HasValue)
            return false;

        if (orderCustomerId.Value <= 0)
            return false;

        var currentCustomerId = user.CustomerId;
        if (currentCustomerId.HasValue && currentCustomerId.Value > 0 && currentCustomerId.Value == orderCustomerId.Value)
            return false;

        return !user.ManagedCustomerIds.Contains(orderCustomerId.Value);
    }
}
