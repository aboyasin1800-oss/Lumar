using LUMAR_ERP_API_V2.DTOs.Inventory;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken cancellationToken);
    Task<InventoryItemDto?> GetItemByIdAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryWarehouseSummaryDto>> GetWarehouseSummariesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryWarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GoodsReceiptDifferenceDto>> GetGoodsReceiptDifferencesAsync(int goodsReceiptId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FabricDto>> GetFabricsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMadeAsync(CancellationToken cancellationToken);
    Task<ReadyMadeProductDto?> GetReadyMadeByIdAsync(int readyMadeInventoryProductId, CancellationToken cancellationToken);
    Task<ReadyMadeProductDto?> RecordReadyMadeSaleCostAsync(int readyMadeInventoryProductId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImportedAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryItemDto>> GetToolsAsync(CancellationToken cancellationToken);
    Task<ImportedReadyMadeProductDto?> UpsertImportedProductAsync(CreateImportedProductDto product, CancellationToken cancellationToken);
    Task<InventoryItemDto?> UpsertToolItemAsync(CreateToolItemDto tool, CancellationToken cancellationToken);
    Task<FabricBatchResultDto?> ReceiveFabricBatchAsync(CreateFabricBatchDto batch, CancellationToken cancellationToken);
    Task<InventoryFoundationPostingResultDto?> ReceiveFabricInventoryAsync(ReceiveFabricInventoryDto request, CancellationToken cancellationToken);
    Task<InventoryFoundationPostingResultDto?> ReceiveConsumableInventoryAsync(ReceiveConsumableInventoryDto request, CancellationToken cancellationToken);
    Task<GoodsReceiptRuntimeResult> CreateGoodsReceiptAsync(CreateGoodsReceiptDto request, CancellationToken cancellationToken);
    Task<GoodsReceiptReversalResult> ReverseGoodsReceiptAsync(ReverseGoodsReceiptDto request, CancellationToken cancellationToken);
    Task<InventoryFoundationPostingResultDto?> ConsumeFabricInventoryAsync(ConsumeFabricInventoryDto request, CancellationToken cancellationToken);
    Task<InventoryFoundationPostingResultDto?> ConsumeConsumableInventoryAsync(ConsumeConsumableInventoryDto request, CancellationToken cancellationToken);
}