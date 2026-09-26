namespace LUMAR_ERP_API_V2.DTOs.SalesReference;

/// <summary>Represents the current immutable version of sales reference data.</summary>
public sealed record SalesReferenceVersionResponse(string Version, int SchemaVersion);

/// <summary>Represents the cacheable reference data required to initialize tailoring sales.</summary>
public sealed record SalesReferenceSnapshotResponse(
    string Version,
    int SchemaVersion,
    IReadOnlyList<SalesReferenceProductTypeResponse> ProductTypes,
    IReadOnlyList<SalesReferenceMeasurementFieldResponse> MeasurementFields,
    IReadOnlyList<SalesReferenceLoyaltyPointSettingResponse> LoyaltyPiecePointSettings);

/// <summary>Represents an official tailoring product type.</summary>
public sealed record SalesReferenceProductTypeResponse(
    int ProductTypeId,
    string Code,
    string NameAr,
    string? Category,
    string? Scope,
    bool IsActive);

/// <summary>Represents an official measurement field for a product type.</summary>
public sealed record SalesReferenceMeasurementFieldResponse(
    int ProductTypeId,
    int MeasurementProfileId,
    int MeasurementFieldId,
    string Code,
    string NameAr,
    string Unit,
    bool IsRequired,
    int Sequence);

/// <summary>Represents the configured official loyalty points for a product type.</summary>
public sealed record SalesReferenceLoyaltyPointSettingResponse(
    int ProductTypeId,
    decimal? Points,
    bool? IsActive,
    bool IsConfigured);