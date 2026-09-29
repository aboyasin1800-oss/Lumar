using LUMAR_ERP_API_V2.DTOs.Auth;

namespace LUMAR_ERP_API_V2.Authorization;

public static class Es7Permission
{
    public const string PurchasingView = "Purchasing.View";
    public const string PurchasingManage = "Purchasing.Manage";
    public const string PurchasingReceive = "Purchasing.Receive";
    public const string PurchasingInvoice = "Purchasing.Invoice";
    public const string PurchasingPay = "Purchasing.Pay";
    public const string PurchasingAllocate = "Purchasing.Allocate";
    public const string PurchasingReverse = "Purchasing.Reverse";
    public const string InventoryReceive = "Inventory.Receive";
    public const string InventoryReverse = "Inventory.Reverse";
    public const string FinanceSupplierPay = "Finance.SupplierPay";
    public const string FinanceSupplierReverse = "Finance.SupplierReverse";
    public const string FinanceAdmin = "Finance.Admin";
}

public static class Es7PermissionPolicy
{
    public const string SystemAdministratorRole = "System Administrator";
    public const string AuthorizedFinancialManagerRole = "Authorized Financial Manager";

    public static bool CanReverse(CurrentUserDto? user) => HasRole(user, SystemAdministratorRole) || HasRole(user, AuthorizedFinancialManagerRole);

    public static bool HasPermission(CurrentUserDto? user, string permission)
    {
        if (user is null || string.IsNullOrWhiteSpace(permission)) return false;
        if (HasRole(user, SystemAdministratorRole)) return true;
        if (!HasRole(user, AuthorizedFinancialManagerRole)) return false;
        return permission is Es7Permission.PurchasingView or Es7Permission.PurchasingInvoice or Es7Permission.PurchasingPay or Es7Permission.PurchasingAllocate or Es7Permission.PurchasingReverse or Es7Permission.InventoryReverse or Es7Permission.FinanceSupplierPay or Es7Permission.FinanceSupplierReverse;
    }

    private static bool HasRole(CurrentUserDto? user, string role) => user is not null && string.Equals(Normalize(user.Role), Normalize(role), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => string.Join(' ', (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}