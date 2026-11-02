using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Orders;

public sealed record TailoringOrderEditRequest
{
    public Guid EditOperationId { get; init; }
    [Required, StringLength(100)] public string ExpectedVersion { get; init; } = string.Empty;
    public TailoringOrderDetailsPatch? Details { get; init; }
    public IReadOnlyList<TailoringExistingPieceChange> ChangedPieces { get; init; } = [];
    public IReadOnlyList<TailoringNewPiece> AddedPieces { get; init; } = [];
    public IReadOnlyList<TailoringPieceCancellation> CancelledPieces { get; init; } = [];
}

public sealed record TailoringOrderDetailsPatch
{
    public bool SetDeliveryDate { get; init; }
    public DateTimeOffset? DeliveryDate { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed record TailoringExistingPieceChange
{
    [Range(1, int.MaxValue)] public int PieceId { get; init; }
    public TailoringPiecePatch Patch { get; init; } = new();
}

public sealed record TailoringPiecePatch
{
    [Range(1, int.MaxValue)] public int? ProductTypeId { get; init; }
    [StringLength(50)] public string? FabricCode { get; init; }
    public IReadOnlyDictionary<string, string>? Measurements { get; init; }
    [StringLength(100)] public string? CatalogNumber { get; init; }
    [StringLength(500)] public string? Request1 { get; init; }
    [StringLength(500)] public string? Request2 { get; init; }
    [StringLength(500)] public string? SpecialRequest { get; init; }
    [StringLength(500)] public string? Notes1 { get; init; }
    [StringLength(500)] public string? Notes2 { get; init; }
}

public sealed record TailoringNewPiece
{
    public Guid AddOperationId { get; init; }
    [Range(1, int.MaxValue)] public int ProductTypeId { get; init; }
    [Required, StringLength(50)] public string FabricCode { get; init; } = string.Empty;
    [StringLength(100)] public string? FabricType { get; init; }
    [StringLength(100)] public string? FabricColor { get; init; }
    public IReadOnlyDictionary<string, string> Measurements { get; init; } = new Dictionary<string, string>();
    [StringLength(100)] public string? CatalogNumber { get; init; }
    [StringLength(500)] public string? Request1 { get; init; }
    [StringLength(500)] public string? Request2 { get; init; }
    [StringLength(500)] public string? SpecialRequest { get; init; }
    [StringLength(500)] public string? Notes1 { get; init; }
    [StringLength(500)] public string? Notes2 { get; init; }
}

public sealed record TailoringPieceCancellation
{
    [Range(1, int.MaxValue)] public int PieceId { get; init; }
    [Required, StringLength(500)] public string Reason { get; init; } = string.Empty;
}

public sealed record TailoringEditablePiece
{
    public int PieceId { get; init; }
    public int OrderItemId { get; init; }
    public int ProductTypeId { get; init; }
    public string PieceType { get; init; } = string.Empty;
    public string TrackingCode { get; init; } = string.Empty;
    public string PieceStatus { get; init; } = string.Empty;
    public string FabricCode { get; init; } = string.Empty;
    public string? FabricType { get; init; }
    public string? FabricColor { get; init; }
    public string? CatalogNumber { get; init; }
    public string? Request1 { get; init; }
    public string? Request2 { get; init; }
    public string? SpecialRequest { get; init; }
    public string? Notes1 { get; init; }
    public string? Notes2 { get; init; }
    public IReadOnlyDictionary<string, string> Measurements { get; init; } = new Dictionary<string, string>();
    public decimal? OriginalSalePrice { get; init; }
    public decimal? ApprovedSalePrice { get; init; }
    public decimal? DiscountShare { get; init; }
    public bool HasTracking { get; init; }
    public bool HasWage { get; init; }
    public bool CanEditMeasurements { get; init; }
    public bool CanEditDetails { get; init; }
    public bool CanCancel { get; init; }
    public string? LockReason { get; init; }
}

public sealed record TailoringOrderEditSnapshot(
    OrderDetailsDto Order,
    string Version,
    bool CanEdit,
    bool CanAddPiece,
    IReadOnlyList<TailoringEditablePiece> Pieces,
    IReadOnlyDictionary<Guid, int> AddedPieceIds,
    string? LockReason = null);