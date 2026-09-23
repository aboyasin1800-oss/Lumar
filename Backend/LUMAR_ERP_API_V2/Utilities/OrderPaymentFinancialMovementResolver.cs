namespace LUMAR_ERP_API_V2.Utilities;

public static class OrderPaymentFinancialMovementResolver
{
    public static (string TransactionType, string Description) Resolve(string paymentKind)
    {
        if (string.Equals(paymentKind, "Advance", StringComparison.OrdinalIgnoreCase))
        {
            return ("CustomerAdvance", "Advance payment for order");
        }

        if (string.Equals(paymentKind, "DebtCollection", StringComparison.OrdinalIgnoreCase))
        {
            return ("CustomerPayment", "Customer payment for order");
        }

        return ("CustomerPayment", "Customer payment for order");
    }
}
