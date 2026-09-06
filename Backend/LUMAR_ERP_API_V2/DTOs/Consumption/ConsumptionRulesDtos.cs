using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Consumption;

public sealed record ConsumptionRuleProductTypeDto(
    int ProductTypeId,
    string Code,
    string NameAr,
    string? Category,
    string? Scope,
    bool IsActive);

public sealed record ConsumptionRuleSizeClassDto(
    int SizeClassId,
    int ProductTypeId,
    string Code,
    string NameAr,
    string MeasurementCode,
    decimal? MinimumValue,
    decimal? MaximumValue,
    int Sequence,
    string Status,
    bool IsActive);

public sealed record ConsumptionRuleDto(
    int ConsumptionRuleId,
    int ProductTypeId,
    int? SizeClassId,
    string ProductTypeName,
    string? SizeClassName,
    string Name,
    string RuleType,
    string Formula,
    string ResultUnit,
    int Priority,
    int Version,
    string Status,
    bool IsActive,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal? FabricWidth = null,
    string? FabricWidthUnit = null,
    string? MeasurementCode = null,
    decimal? MinimumValue = null,
    decimal? MaximumValue = null);

public sealed record ConsumptionRuleMeasurementProfileDto(
    int MeasurementProfileId,
    int ProductTypeId,
    string Name,
    int Version,
    string Status,
    bool IsActive,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public sealed record ConsumptionRuleMeasurementFieldDto(
    int MeasurementFieldId,
    int MeasurementProfileId,
    int ProductTypeId,
    string Code,
    string NameAr,
    string Unit,
    bool IsRequired,
    int Sequence);

public sealed record ConsumptionRuleIntegrityIssueDto(
    string Type,
    string Message,
    int? ProductTypeId,
    int? RuleId,
    string? FabricWidth,
    string? MeasurementKey);

public sealed record ConsumptionRulesIntegrityReportDto(
    int TotalRules,
    int ValidRules,
    int OverlapCount,
    int DuplicateCount,
    int GapCount,
    int ProductTypesCoveringAllSizes,
    int ProductTypesNeedingCompletion,
    IReadOnlyList<ConsumptionRuleIntegrityIssueDto> Issues);

public sealed record ConsumptionRulesDashboardDto(
    int ProductTypesCount,
    int SizeClassesCount,
    int RulesCount,
    int ActiveRulesCount,
    IReadOnlyList<ConsumptionRuleProductTypeDto> ProductTypes,
    IReadOnlyList<ConsumptionRuleSizeClassDto> SizeClasses,
    IReadOnlyList<ConsumptionRuleDto> Rules,
    IReadOnlyList<ConsumptionRuleMeasurementProfileDto> MeasurementProfiles,
    IReadOnlyList<ConsumptionRuleMeasurementFieldDto> MeasurementFields);

public sealed record MeasurementTypeWriteResultDto(
    int ProductTypeId,
    int MeasurementProfileId,
    int MeasurementFieldCount,
    string NameAr);

public sealed class CreateMeasurementTypeDto
{
    [Required, StringLength(200)]
    public string NameAr { get; init; } = string.Empty;

    [StringLength(50)]
    public string? Code { get; init; }

    public int FieldCount { get; init; }

    public List<string> FieldNames { get; init; } = [];
}

public sealed class UpdateMeasurementTypeDto
{
    [Required, StringLength(200)]
    public string NameAr { get; init; } = string.Empty;

    public List<string> FieldNames { get; init; } = [];
}

public sealed class SaveProductRulesBatchDto
{
    [Required]
    public int ProductTypeId { get; init; }

    public List<CreateConsumptionRuleDto> Rules { get; init; } = [];
}

public sealed class CreateConsumptionRuleDto
{
    [Required]
    public int ProductTypeId { get; init; }

    public int? SizeClassId { get; init; }

    public decimal? FabricWidth { get; init; }

    public string? FabricWidthUnit { get; init; }

    public string? FirstMeasurementCode { get; init; }

    public decimal? FirstFactor { get; init; }

    public string? SecondMeasurementCode { get; init; }

    public decimal? SecondFactor { get; init; }

    public string? ConditionalMeasurementCode { get; init; }

    public decimal? MinimumValue { get; init; }

    public decimal? MaximumValue { get; init; }

    public decimal? FixedIncrease { get; init; }

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string RuleType { get; init; } = "Formula";

    [Required, StringLength(4000)]
    public string Formula { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string ResultUnit { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Priority { get; init; }

    [Required, StringLength(50)]
    public string Status { get; init; } = "Active";
}

public sealed class UpdateConsumptionRuleDto
{
    public int? SizeClassId { get; init; }

    public decimal? FabricWidth { get; init; }

    public string? FabricWidthUnit { get; init; }

    public string? FirstMeasurementCode { get; init; }

    public decimal? FirstFactor { get; init; }

    public string? SecondMeasurementCode { get; init; }

    public decimal? SecondFactor { get; init; }

    public string? ConditionalMeasurementCode { get; init; }

    public decimal? MinimumValue { get; init; }

    public decimal? MaximumValue { get; init; }

    public decimal? FixedIncrease { get; init; }

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(4000)]
    public string Formula { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string ResultUnit { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Priority { get; init; }

    [Required, StringLength(50)]
    public string Status { get; init; } = "Active";
}
