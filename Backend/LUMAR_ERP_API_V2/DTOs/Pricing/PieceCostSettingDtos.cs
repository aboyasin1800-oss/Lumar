using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Pricing;

public sealed record PieceCostSettingDto(
    int ProductTypeId,
    string PieceCode,
    string PieceName,
    decimal SewingCost,
    decimal ConsumablesCost,
    decimal IroningAndPackagingCost,
    decimal FixedOperatingCost,
    string? Notes,
    decimal TotalOperationalCost,
    bool IsConfigured);

public sealed class UpsertPieceCostSettingDto
{
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal SewingCost { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal ConsumablesCost { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal IroningAndPackagingCost { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal FixedOperatingCost { get; init; }
    [StringLength(500)] public string? Notes { get; init; }
}