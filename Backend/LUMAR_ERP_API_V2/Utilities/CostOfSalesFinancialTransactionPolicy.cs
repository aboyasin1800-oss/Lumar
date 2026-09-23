namespace LUMAR_ERP_API_V2.Utilities;

public static class CostOfSalesFinancialTransactionPolicy
{
    public const string TransactionType = "CostOfSales";
    public const string SourceAccountCode = "1110";
    public const string TargetAccountCode = "5200";
    public const string DescriptionText = "Cost of sold item";

    public static bool ShouldCreateTransaction(string? trackingCode, decimal amount, bool alreadyExists)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            return false;
        }

        if (amount <= 0m)
        {
            return false;
        }

        return !alreadyExists;
    }

    public static string GetReferenceNumber(string trackingCode)
        => string.IsNullOrWhiteSpace(trackingCode)
            ? throw new ArgumentException("TrackingCode is required.", nameof(trackingCode))
            : trackingCode.Trim();

    public static string GetDescription() => DescriptionText;
}
