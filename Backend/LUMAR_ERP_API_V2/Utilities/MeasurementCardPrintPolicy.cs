namespace LUMAR_ERP_API_V2.Utilities;

public static class MeasurementCardPrintPolicy
{
    public const string DamagedCard = "DamagedCard";
    public const string LostCard = "LostCard";
    public const string DamagedPiece = "DamagedPiece";
    public const string PieceSold = "PieceSold";
    public const string Cash = "Cash";
    public const string Credit = "Credit";
    public const string Donation = "Donation";

    public static bool IsReprintReason(string? value) => value is DamagedCard or LostCard or DamagedPiece or PieceSold;

    public static bool IsLegacyPrintedStatus(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized == "printed"
            || normalized == "printed_again"
            || normalized == "reprinted"
            || normalized == "completed"
            || normalized == "done"
            || normalized.Contains("printed", StringComparison.Ordinal)
            || normalized.Contains("completed", StringComparison.Ordinal);
    }

    public static int NextCopyNumber(int? lastCompletedCopyNumber, bool legacyPrinted)
        => lastCompletedCopyNumber is > 0
            ? lastCompletedCopyNumber.Value + 1
            : legacyPrinted ? 2 : 1;

    public static string CopyLabel(int copyNumber) => copyNumber switch
    {
        2 => "النسخة الثانية",
        3 => "النسخة الثالثة",
        4 => "النسخة الرابعة",
        > 1 => $"النسخة رقم {copyNumber}",
        _ => string.Empty,
    };

    public static string NormalizePaymentType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "cash" or "نقداً" or "نقدا" => Cash,
            "credit" or "onaccount" or "آجل" => Credit,
            "donation" or "تبرعاً" or "تبرعا" => Donation,
            _ => throw new ArgumentException("طريقة الدفع يجب أن تكون نقداً أو آجلاً أو تبرعاً.")
        };
    }
}
