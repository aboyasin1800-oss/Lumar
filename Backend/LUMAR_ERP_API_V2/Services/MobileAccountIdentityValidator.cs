namespace LUMAR_ERP_API_V2.Services;

public static class MobileAccountIdentityValidator
{
    public static bool IsValid(string accountType, int? customerId, int? employeeId, int? supplierId)
    {
        if (string.IsNullOrWhiteSpace(accountType))
            return false;

        return accountType switch
        {
            "Customer" => customerId.HasValue && !employeeId.HasValue && !supplierId.HasValue,
            "Employee" => employeeId.HasValue && !customerId.HasValue && !supplierId.HasValue,
            "Supplier" => supplierId.HasValue && !customerId.HasValue && !employeeId.HasValue,
            _ => false
        };
    }
}
