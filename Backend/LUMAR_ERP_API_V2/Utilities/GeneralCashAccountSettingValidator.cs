using LUMAR_ERP_API_V2.DTOs.Finance;

namespace LUMAR_ERP_API_V2.Utilities;

public static class GeneralCashAccountSettingValidator
{
    public static bool IsEligible(CashAccountDto? account)
    {
        if (account is null)
        {
            return false;
        }

        return account.IsActive
            && string.Equals(account.CurrencyCode?.Trim(), "YER", StringComparison.OrdinalIgnoreCase)
            && account.IsReceiptEnabled
            && account.IsDisbursementEnabled;
    }
}
