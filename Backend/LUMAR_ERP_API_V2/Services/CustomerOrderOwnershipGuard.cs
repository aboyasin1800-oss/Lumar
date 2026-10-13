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

        var customerId = user.CustomerId;
        if (!customerId.HasValue || customerId.Value <= 0 || !orderCustomerId.HasValue)
            return false;

        return customerId.Value == orderCustomerId.Value;
    }

    public static bool IsCrossCustomerViolation(CurrentUserDto? user, int? orderCustomerId)
    {
        if (user is null)
            return false;

        if (!string.Equals(user.AccountType, "Customer", StringComparison.OrdinalIgnoreCase)
            || !orderCustomerId.HasValue)
            return false;

        var currentCustomerId = user.CustomerId;
        return currentCustomerId.HasValue && currentCustomerId.Value > 0 && currentCustomerId.Value != orderCustomerId.Value;
    }
}
