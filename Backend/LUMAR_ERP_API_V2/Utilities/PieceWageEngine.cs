using LUMAR_ERP_API_V2.DTOs.Payroll;

namespace LUMAR_ERP_API_V2.Utilities;

public static class PieceWageEngine
{
    private static readonly string[] BlockedEmployees = ["OP-01", "OP-SCAN-01"];

    public static decimal CalculateTotalWage(decimal quantity, decimal wageRate)
        => quantity * wageRate;

    public static PieceWageResolution ResolveRate(string? pieceType, string? stage, IReadOnlyList<PieceWageRateDto> rates)
    {
        if (string.IsNullOrWhiteSpace(pieceType) || string.IsNullOrWhiteSpace(stage))
        {
            return new PieceWageResolution(false, "Piece type and stage are required to resolve the wage rate.");
        }

        var normalizedPieceType = NormalizePieceType(pieceType);
        var normalizedStage = NormalizeStage(stage);

        var exact = rates.FirstOrDefault(r =>
            string.Equals(r.PieceType, normalizedPieceType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.Stage, normalizedStage, StringComparison.OrdinalIgnoreCase)
            && r.IsActive);

        if (exact is not null)
        {
            return ValidateWageRate(exact.WageRate, exact.PieceType, exact.Stage, normalizedPieceType, normalizedStage);
        }

        var wildcard = rates.FirstOrDefault(r =>
            string.Equals(r.PieceType, "*", StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.Stage, normalizedStage, StringComparison.OrdinalIgnoreCase)
            && r.IsActive);

        if (wildcard is not null)
        {
            return ValidateWageRate(wildcard.WageRate, wildcard.PieceType, wildcard.Stage, normalizedPieceType, normalizedStage);
        }

        return new PieceWageResolution(false, $"No valid wage rate exists for piece type '{normalizedPieceType}' at stage '{normalizedStage}'.");
    }

    public static PieceWageValidation ValidateRecord(string? pieceType, string? stage, decimal wageRate, string? employeeCode, decimal quantity, int existingWageRecords)
    {
        if (existingWageRecords > 0)
        {
            return new PieceWageValidation(false, "A PieceWageRecord already exists for this TrackingEvent.", 0m);
        }

        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return new PieceWageValidation(false, "EmployeeCode is required to create a piece wage record.", 0m);
        }

        if (BlockedEmployees.Contains(employeeCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return new PieceWageValidation(false, $"EmployeeCode '{employeeCode.Trim()}' is not allowed to create a piece wage record in the operational wage path.", 0m);
        }

        if (quantity <= 0)
        {
            return new PieceWageValidation(false, "Quantity must be greater than zero.", 0m);
        }

        if (wageRate < 0)
        {
            return new PieceWageValidation(false, "WageRate cannot be negative for a piece wage record.", 0m);
        }

        if (string.IsNullOrWhiteSpace(pieceType) || string.IsNullOrWhiteSpace(stage))
        {
            return new PieceWageValidation(false, "Piece type and stage are required.", 0m);
        }

        var total = CalculateTotalWage(quantity, wageRate);
        return new PieceWageValidation(true, "Piece wage record is valid.", total);
    }

    private static PieceWageResolution ValidateWageRate(decimal wageRate, string sourceType, string sourceStage, string pieceType, string stage)
    {
        if (wageRate < 0)
        {
            return new PieceWageResolution(false, $"WageRate for piece type '{pieceType}' at stage '{stage}' cannot be negative.");
        }

        var normalizedStage = NormalizeStage(stage);
        if (wageRate == 0 && !IsPayableStage(normalizedStage))
        {
            return new PieceWageResolution(true, $"Resolved zero-rate non-paying stage from {sourceType}/{sourceStage}.", 0m);
        }

        if (wageRate == 0 && IsPayableStage(normalizedStage))
        {
            return new PieceWageResolution(false, $"WageRate for payable stage '{normalizedStage}' must be greater than zero.");
        }

        return new PieceWageResolution(true, $"Resolved rate from {sourceType}/{sourceStage}.", wageRate);
    }

    private static bool IsPayableStage(string? stage) =>
        string.Equals(stage, "Cutting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, "Sewing", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePieceType(string pieceType)
    {
        var normalized = pieceType.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
        return normalized.Replace("-", " ").Replace("_", " ").Trim().ToUpperInvariant();
    }

    private static string NormalizeStage(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage)) return string.Empty;
        return stage.Trim();
    }
}

public sealed record PieceWageResolution(bool IsValid, string Message, decimal? WageRate = null);
public sealed record PieceWageValidation(bool IsValid, string Message, decimal TotalWage);
