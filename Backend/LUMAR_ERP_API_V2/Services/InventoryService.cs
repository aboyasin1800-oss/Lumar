using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class InventoryService(IInventoryRepository repository) : IInventoryService
{
    public Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken ct) => repository.GetItemsAsync(ct);
    public Task<InventoryItemDto?> GetItemByIdAsync(int id, CancellationToken ct) => repository.GetItemByIdAsync(id, ct);
    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken ct) => repository.GetTransactionsAsync(ct);
    public Task<IReadOnlyList<FabricDto>> GetFabricsAsync(CancellationToken ct) => repository.GetFabricsAsync(ct);
    public Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMadeAsync(CancellationToken ct) => repository.GetReadyMadeAsync(ct);
    public Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImportedAsync(CancellationToken ct) => repository.GetImportedAsync(ct);
    public Task<IReadOnlyList<InventoryItemDto>> GetToolsAsync(CancellationToken ct) => repository.GetToolsAsync(ct);
    public Task<ImportedReadyMadeProductDto?> UpsertImportedProductAsync(CreateImportedProductDto product, CancellationToken ct) => repository.UpsertImportedProductAsync(product, ct);
    public Task<InventoryItemDto?> UpsertToolItemAsync(CreateToolItemDto tool, CancellationToken ct) => repository.UpsertToolItemAsync(tool, ct);
    public Task<FabricBatchResultDto?> ReceiveFabricBatchAsync(CreateFabricBatchDto batch, CancellationToken ct) => repository.ReceiveFabricBatchAsync(batch, ct);
}