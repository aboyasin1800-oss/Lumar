namespace LUMAR_ERP_API_V2.Utilities;

public static class PayrollEngine
{
    public static decimal CalculateGrossAmount(
        decimal basicSalaryAmount,
        decimal pieceWageAmount,
        decimal attendanceAdjustmentAmount,
        decimal overtimeAmount)
    {
        return basicSalaryAmount + pieceWageAmount + attendanceAdjustmentAmount + overtimeAmount;
    }

    public static decimal CalculateNetAmount(decimal grossAmount, decimal deductionsAmount)
    {
        return grossAmount - deductionsAmount;
    }

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Draft";
        }

        var normalized = status.Trim();

        return normalized switch
        {
            "Draft" or "draft" or "DRAFT" => "Draft",
            "Generated" or "generated" or "GENERATED" => "Generated",
            "Approved" or "approved" or "APPROVED" => "Approved",
            "Paid" or "paid" or "PAID" => "Paid",
            "Calculated" or "calculated" or "CALCULATED" => "Calculated",
            "PendingPayroll" or "pendingpayroll" or "PENDINGPAYROLL" => "Generated",
            _ => normalized,
        };
    }

    public static bool IsFinalized(string? status)
    {
        var normalized = NormalizeStatus(status);
        return string.Equals(normalized, "Paid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Approved", StringComparison.OrdinalIgnoreCase);
    }
}
