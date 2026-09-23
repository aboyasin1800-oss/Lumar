namespace LUMAR_ERP_API_V2.Utilities;

public static class WipToFinishedGoodsFinancialTransactionPolicy
{
    public const string TransactionType = "WipToFinishedGoods";
    public const string SourceAccountCode = "1130";
    public const string TargetAccountCode = "1110";
    public const string DescriptionText = "Transfer from Work In Progress to Finished Goods";

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
