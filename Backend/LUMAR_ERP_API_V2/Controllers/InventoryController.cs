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

    [HttpGet("tools")]
    public Task<IReadOnlyList<InventoryItemDto>> GetTools(CancellationToken ct) => service.GetToolsAsync(ct);

    [HttpPost("tools")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InventoryItemDto>> UpsertTool(CreateToolItemDto tool, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tool.ProductName)) return BadRequest("Product name is required.");
        if (string.IsNullOrWhiteSpace(tool.Unit)) return BadRequest("Unit is required.");
        if (tool.Quantity <= 0) return BadRequest("Quantity must be greater than zero.");
        if (tool.UnitPrice <= 0) return BadRequest("Unit price must be greater than zero.");

        var result = await service.UpsertToolItemAsync(tool, ct);
        return result is null ? BadRequest("Unable to save tool item.") : Ok(result);
    }

    [HttpPost("imported")]
    [ProducesResponseType<ImportedReadyMadeProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportedReadyMadeProductDto>> UpsertImportedProduct(CreateImportedProductDto product, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.ProductName)) return BadRequest("Product name is required.");
        if (string.IsNullOrWhiteSpace(product.ProductCode)) return BadRequest("Product code is required.");
        if (string.IsNullOrWhiteSpace(product.Unit)) return BadRequest("Product unit is required.");
        if (product.Quantity <= 0) return BadRequest("Quantity must be greater than zero.");
        if (product.PurchasePrice <= 0) return BadRequest("Purchase price must be greater than zero.");
        if (product.SellingPrice <= 0) return BadRequest("Selling price must be greater than zero.");

        var result = await service.UpsertImportedProductAsync(product, ct);
        return result is null ? BadRequest("Unable to save imported product.") : Ok(result);
    }

    [HttpPost("fabric-batches")]
    [ProducesResponseType<FabricBatchResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FabricBatchResultDto>> ReceiveFabricBatch(CreateFabricBatchDto batch, CancellationToken ct)
    {
        if (batch.SupplierId <= 0) return BadRequest("Supplier is required.");
        if (string.IsNullOrWhiteSpace(batch.InvoiceNumber)) return BadRequest("Invoice number is required.");
        if (batch.Rolls == null || batch.Rolls.Count == 0) return BadRequest("At least one roll is required.");

        foreach (var roll in batch.Rolls)
        {
            if (string.IsNullOrWhiteSpace(roll.FabricCode)) return BadRequest("Fabric code is required for all rolls.");
            if (string.IsNullOrWhiteSpace(roll.FabricType)) return BadRequest("Fabric type is required for all rolls.");
            if (roll.FabricWidth <= 0) return BadRequest("Fabric width must be positive.");
            if (roll.QuantityYards <= 0) return BadRequest("Quantity of yards must be positive.");
            if (roll.YardPrice <= 0) return BadRequest("Yard price must be positive.");
        }

        var result = await service.ReceiveFabricBatchAsync(batch, ct);
        return result is null ? NotFound("Supplier does not exist.") : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("items")]
    [HttpPut("items/{id:int}")]
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "General item modifications are restricted. Use dedicated entry endpoints.");
}