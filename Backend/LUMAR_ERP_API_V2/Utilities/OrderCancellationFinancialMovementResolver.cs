namespace LUMAR_ERP_API_V2.Utilities;

public static class OrderCancellationFinancialMovementResolver
{
    public static bool ShouldCreateRefund(decimal paidAmount, bool refundAlreadyRecorded)
    {
        if (refundAlreadyRecorded)
        {
            return false;
        }

        return paidAmount > 0m;
    }

    public static bool ShouldCreateRevenueReversal(bool revenueRecognized, bool revenueReversalCreated, string orderStatus)
    {
        if (!revenueRecognized || revenueReversalCreated)
        {
            return false;
        }

        return string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldCreateRevenueReversalOnCancellation(bool revenueRecognized, bool revenueReversalCreated)
    {
        return revenueRecognized && !revenueReversalCreated;
    }

    public static decimal ResolveRefundAmount(decimal paidAmount)
    {
        return Math.Max(0m, paidAmount);
    }
}
