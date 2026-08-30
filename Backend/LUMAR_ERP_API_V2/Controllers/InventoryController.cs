using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("inventory")]
public sealed class InventoryController(IInventoryService service) : ControllerBase
{
    [HttpGet("items")]
    public Task<IReadOnlyList<InventoryItemDto>> GetItems(CancellationToken ct) => service.GetItemsAsync(ct);

    [HttpGet("items/{id:int}")]
    public async Task<ActionResult<InventoryItemDto>> GetItem(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Inventory item id must be positive.");
        var item = await service.GetItemByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("transactions")]
    public Task<IReadOnlyList<InventoryTransactionDto>> GetTransactions(CancellationToken ct) => service.GetTransactionsAsync(ct);

    [HttpGet("fabrics")]
    public Task<IReadOnlyList<FabricDto>> GetFabrics(CancellationToken ct) => service.GetFabricsAsync(ct);

    [HttpGet("readymade")]
    public Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMade(CancellationToken ct) => service.GetReadyMadeAsync(ct);

    [HttpGet("imported")]
    public Task<IReadOnlyList<ImportedReadyMadeProductDto>> GetImported(CancellationToken ct) => service.GetImportedAsync(ct);

    [HttpPost("items")]
    [HttpPut("items/{id:int}")]
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Inventory writes are disabled while LUMAR_ERP is read-only.");
}