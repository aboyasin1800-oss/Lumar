using LUMAR_ERP_API_V2.DTOs.Inventory;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItemDto>> GetItemsAsync(CancellationToken cancellationToken);
    Task<InventoryItemDto?> GetItemByIdAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FabricDto>> GetFabricsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMadeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImportedAsync(CancellationToken cancellationToken);
}