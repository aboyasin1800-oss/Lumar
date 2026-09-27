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

    [HttpGet("summary")]
    public Task<IReadOnlyList<InventoryWarehouseSummaryDto>> GetWarehouseSummaries(CancellationToken ct) => service.GetWarehouseSummariesAsync(ct);

    [HttpGet("fabrics")]
    public Task<IReadOnlyList<FabricDto>> GetFabrics(CancellationToken ct) => service.GetFabricsAsync(ct);

    [HttpGet("readymade")]
    public Task<IReadOnlyList<ReadyMadeProductDto>> GetReadyMade(CancellationToken ct) => service.GetReadyMadeAsync(ct);

    [HttpGet("readymade/{id:int}")]
    public async Task<ActionResult<ReadyMadeProductDto>> GetReadyMadeById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Ready-made inventory id must be positive.");
        var item = await service.GetReadyMadeByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("readymade/{id:int}/sale-cost")]
    [ProducesResponseType<ReadyMadeProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReadyMadeProductDto>> RecordReadyMadeSaleCost(int id, CancellationToken ct)
    {
        var item = await service.RecordReadyMadeSaleCostAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

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
        return StatusCode(StatusCodes.Status405MethodNotAllowed, "Legacy fabric batch input is disabled. Use the official goods-receipt foundation endpoint.");
    }

    [HttpPost("foundation/fabric-receipts")]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryFoundationPostingResultDto>> ReceiveFoundationFabric(ReceiveFabricInventoryDto request, CancellationToken ct)
    {
        if (request.GoodsReceiptItemId <= 0 || string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.FabricTypeCode))
            return BadRequest("Goods receipt item, item code, and fabric type code are required.");
        if (request.UnitId is not (1 or 2) || string.IsNullOrWhiteSpace(request.OpposingLedgerAccountCode) || request.SourceOperationId == Guid.Empty)
            return BadRequest("A valid fabric unit, opposing ledger account, and source operation id are required.");

        var result = await service.ReceiveFabricInventoryAsync(request, ct);
        return result is null ? NotFound() : result.IsExisting ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("foundation/consumable-receipts")]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryFoundationPostingResultDto>> ReceiveFoundationConsumable(ReceiveConsumableInventoryDto request, CancellationToken ct)
    {
        if (request.GoodsReceiptItemId <= 0 || string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.OpposingLedgerAccountCode) || request.SourceOperationId == Guid.Empty)
            return BadRequest("Goods receipt item, item code, opposing ledger account, and source operation id are required.");
        if (request.UnitId != 3) return BadRequest("Consumable inventory must use the formal piece unit.");

        var result = await service.ReceiveConsumableInventoryAsync(request, ct);
        return result is null ? NotFound() : result.IsExisting ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("foundation/fabric-consumptions")]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryFoundationPostingResultDto>> ConsumeFoundationFabric(ConsumeFabricInventoryDto request, CancellationToken ct)
    {
        if (request.FabricRollId <= 0 || request.OrderItemId <= 0 || request.Quantity <= 0 || request.SourceOperationId == Guid.Empty)
            return BadRequest("Fabric roll, order item, positive quantity, and source operation id are required.");

        var result = await service.ConsumeFabricInventoryAsync(request, ct);
        return result is null ? NotFound() : result.IsExisting ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("foundation/consumable-consumptions")]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryFoundationPostingResultDto>> ConsumeFoundationConsumable(ConsumeConsumableInventoryDto request, CancellationToken ct)
    {
        if (request.ProductionOrderId <= 0 || request.InventoryItemId <= 0 || request.Quantity <= 0 || request.SourceOperationId == Guid.Empty)
            return BadRequest("Production order, inventory item, positive quantity, and source operation id are required.");
        if (request.UnitId != 3) return BadRequest("Consumable inventory must use the formal piece unit.");

        var result = await service.ConsumeConsumableInventoryAsync(request, ct);
        return result is null ? NotFound() : result.IsExisting ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("items")]
    [HttpPut("items/{id:int}")]
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "General item modifications are restricted. Use dedicated entry endpoints.");
}