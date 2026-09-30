using LUMAR_ERP_API_V2.DTOs.Inventory;
namespace LUMAR_ERP_API_V2.Services;
public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken ct);
    Task<InventoryItemDto?> GetItemByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken ct);
    Task<IReadOnlyList<InventoryWarehouseSummaryDto>> GetWarehouseSummariesAsync(CancellationToken ct);
    Task<IReadOnlyList<InventoryWarehouseDto>> GetWarehousesAsync(CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptDifferenceDto>> GetGoodsReceiptDifferencesAsync(int goodsReceiptId, CancellationToken ct);
    Task<IReadOnlyList<PendingGoodsReceiptStorageDto>> GetPendingGoodsReceiptStorageAsync(CancellationToken ct);
    Task<IReadOnlyList<FabricDto>> GetFabricsAsync(CancellationToken ct);
    Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMadeAsync(CancellationToken ct);
    Task<ReadyMadeProductDto?> GetReadyMadeByIdAsync(int readyMadeInventoryProductId, CancellationToken ct);
    Task<ReadyMadeProductDto?> RecordReadyMadeSaleCostAsync(int readyMadeInventoryProductId, CancellationToken ct);
    Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImportedAsync(CancellationToken ct);
    Task<IReadOnlyList<InventoryItemDto>> GetToolsAsync(CancellationToken ct);
    Task<ImportedReadyMadeProductDto?> UpsertImportedProductAsync(CreateImportedProductDto product, CancellationToken ct);
    Task<InventoryItemDto?> UpsertToolItemAsync(CreateToolItemDto tool, CancellationToken ct);
    Task<FabricBatchResultDto?> ReceiveFabricBatchAsync(CreateFabricBatchDto batch, CancellationToken ct);
    Task<InventoryFoundationPostingResultDto?> ReceiveFabricInventoryAsync(ReceiveFabricInventoryDto request, CancellationToken ct);
    Task<InventoryFoundationPostingResultDto?> ReceiveConsumableInventoryAsync(ReceiveConsumableInventoryDto request, CancellationToken ct);
    Task<InventoryFoundationPostingResultDto?> ConsumeFabricInventoryAsync(ConsumeFabricInventoryDto request, CancellationToken ct);
    Task<InventoryFoundationPostingResultDto?> ConsumeConsumableInventoryAsync(ConsumeConsumableInventoryDto request, CancellationToken ct);
}