using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Pricing;

public sealed record PieceCostManagementDto(
    int ProductTypeId,
    string Code,
    string PieceName,
    decimal SewingCost,
    decimal ConsumablesCost,
    decimal IroningAndPackagingCost,
    decimal FixedOperatingCost,
    decimal MonthlyRent,
    decimal MonthlySalaries,
    decimal MonthlyElectricity,
    decimal MonthlyWater,
    decimal MonthlyInternet,
    decimal MonthlyDepreciation);

public sealed class UpdatePieceCostManagementDto
{
    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal SewingCost { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal ConsumablesCost { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal IroningAndPackagingCost { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal FixedOperatingCost { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlyRent { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlySalaries { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlyElectricity { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlyWater { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlyInternet { get; init; }

    [Range(typeof(decimal), "0", "99999999999999.9999")]
    public decimal MonthlyDepreciation { get; init; }
}