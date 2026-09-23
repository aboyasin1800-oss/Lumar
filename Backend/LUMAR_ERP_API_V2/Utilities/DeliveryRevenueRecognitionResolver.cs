namespace LUMAR_ERP_API_V2.Utilities;

public static class DeliveryRevenueRecognitionResolver
{
    public static bool ShouldRecognizeRevenue(bool revenueRecognized, string orderStatus)
    {
        if (revenueRecognized)
        {
            return false;
        }

        return string.Equals(orderStatus, "Delivered", StringComparison.OrdinalIgnoreCase);
    }

    public static decimal ResolveRevenueAmount(decimal totalAmount, decimal discountAmount)
    {
        return Math.Max(0m, totalAmount - discountAmount);
    }
}
