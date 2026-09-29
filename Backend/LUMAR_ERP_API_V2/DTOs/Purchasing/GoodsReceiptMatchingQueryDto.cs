using LUMAR_ERP_API_V2.DTOs.Inventory;

namespace LUMAR_ERP_API_V2.DTOs.Purchasing;

public sealed record GoodsReceiptMatchingQueryDto(GoodsReceiptDto Receipt, IReadOnlyList<GoodsReceiptItemDto> Items, IReadOnlyList<GoodsReceiptDifferenceDto> Differences);