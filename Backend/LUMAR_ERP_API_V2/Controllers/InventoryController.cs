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

    [HttpGet("warehouses")]
    public Task<IReadOnlyList<InventoryWarehouseDto>> GetWarehouses(CancellationToken ct) => service.GetWarehousesAsync(ct);

    [HttpGet("pending-receipt-storage")]
    public Task<IReadOnlyList<PendingGoodsReceiptStorageDto>> GetPendingReceiptStorage(CancellationToken ct) => service.GetPendingGoodsReceiptStorageAsync(ct);

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

    [HttpPost("tools/issue-operational")]
    [ProducesResponseType<ToolIssuanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ToolIssuanceResultDto>> IssueToolOperational(CreateToolOperationalIssueDto request, CancellationToken ct)
    {
        if (request.InventoryItemId <= 0) return BadRequest("Inventory item is required.");
        if (request.Quantity <= 0m) return BadRequest("Quantity must be positive.");
        if (request.OfficialUnitCost <= 0m) return BadRequest("Official unit cost must be positive.");
        if (string.IsNullOrWhiteSpace(request.OperationalReason)) return BadRequest("Operational reason is required.");
        if (request.SourceOperationId == Guid.Empty) return BadRequest("Source operation id is required.");

        var result = await service.IssueToolOperationalAsync(request, ct);
        return result is null ? BadRequest("Unable to issue tool operationally.") : Ok(result);
    }

    [HttpPost("tools/issue-custody")]
    [ProducesResponseType<ToolIssuanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ToolIssuanceResultDto>> IssueToolCustody(CreateToolCustodyIssueDto request, CancellationToken ct)
    {
        if (request.InventoryItemId <= 0) return BadRequest("Inventory item is required.");
        if (request.Quantity <= 0m) return BadRequest("Quantity must be positive.");
        if (request.OfficialUnitCost <= 0m) return BadRequest("Official unit cost must be positive.");
        if (string.IsNullOrWhiteSpace(request.BeneficiaryName)) return BadRequest("Beneficiary name is required.");
        if (string.IsNullOrWhiteSpace(request.DestinationType)) return BadRequest("Destination type is required.");
        if (string.IsNullOrWhiteSpace(request.DestinationName)) return BadRequest("Destination name is required.");
        if (string.IsNullOrWhiteSpace(request.LoanReason)) return BadRequest("Loan reason is required.");
        if (request.SourceOperationId == Guid.Empty) return BadRequest("Source operation id is required.");

        var result = await service.IssueToolCustodyAsync(request, ct);
        return result is null ? BadRequest("Unable to issue tool custody.") : Ok(result);
    }

    [HttpPost("tools/returns/custody")]
    [ProducesResponseType<ToolIssuanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ToolIssuanceResultDto>> ReturnToolCustody(ReturnToolCustodyDto request, CancellationToken ct)
    {
        if (request.ToolIssuanceId <= 0) return BadRequest("Tool issuance id is required.");
        if (request.ReturnedQuantity <= 0m) return BadRequest("Returned quantity must be positive.");
        if (request.SourceOperationId == Guid.Empty) return BadRequest("Source operation id is required.");

        var result = await service.ReturnToolCustodyAsync(request, ct);
        return result is null ? BadRequest("Unable to return tool custody.") : Ok(result);
    }

    [HttpPost("tools/reversals/operational")]
    [ProducesResponseType<ToolIssuanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ToolIssuanceResultDto>> ReverseToolOperationalIssue(ReverseToolOperationalIssueDto request, CancellationToken ct)
    {
        if (request.ToolIssuanceId <= 0) return BadRequest("Tool issuance id is required.");
        if (string.IsNullOrWhiteSpace(request.ReversalReason)) return BadRequest("Reversal reason is required.");
        if (string.IsNullOrWhiteSpace(request.Notes)) return BadRequest("Notes are required.");
        if (request.SourceOperationId == Guid.Empty) return BadRequest("Source operation id is required.");

        var result = await service.ReverseToolOperationalIssueAsync(request, ct);
        return result is null ? BadRequest("Unable to reverse tool operational issue.") : Ok(result);
    }

    [HttpGet("tools/history")]
    public Task<IReadOnlyList<ToolIssuanceHistoryDto>> GetToolIssuanceHistory(CancellationToken ct) => service.GetToolIssuanceHistoryAsync(ct);

    [HttpGet("tools/custody/open")]
    public Task<IReadOnlyList<OpenToolCustodyDto>> GetOpenToolCustody(CancellationToken ct) => service.GetOpenToolCustodyAsync(ct);

    [HttpPost("imported")]
    [ProducesResponseType<ImportedReadyMadeProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportedReadyMadeProductDto>> UpsertImportedProduct(CreateImportedProductDto product, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.ProductName)) return BadRequest("Product name is required.");
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
        if (batch.SupplierId <= 0 || string.IsNullOrWhiteSpace(batch.InvoiceNumber) || batch.Rolls.Count == 0)
            return BadRequest("Supplier, invoice number, and at least one fabric roll are required.");

        var result = await service.ReceiveFabricBatchAsync(batch, ct);
        return result is null ? NotFound() : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("foundation/fabric-receipts")]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<InventoryFoundationPostingResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryFoundationPostingResultDto>> ReceiveFoundationFabric(ReceiveFabricInventoryDto request, CancellationToken ct)
    {
        var hasRollBatch = request.Rolls is { Count: > 0 };
        if (request.GoodsReceiptItemId <= 0)
            return BadRequest("Goods receipt item is required.");
        if (request.UnitId is not (1 or 2) || string.IsNullOrWhiteSpace(request.OpposingLedgerAccountCode) || request.SourceOperationId == Guid.Empty)
            return BadRequest("A valid fabric unit, opposing ledger account, and source operation id are required.");
        if (!hasRollBatch && (string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.FabricTypeCode)))
            return BadRequest("Goods receipt item, item code, and fabric type code are required.");
        if (hasRollBatch)
        {
            var batchRolls = request.Rolls ?? [];
            if (string.IsNullOrWhiteSpace(request.FabricTypeCode))
                return BadRequest("Fabric type code is required when storing a multi-roll batch.");
            if (batchRolls.Any(roll => string.IsNullOrWhiteSpace(roll.FabricCode) || roll.Quantity <= 0m))
                return BadRequest("Each roll in the batch must include a fabric code and a positive quantity.");
            if (batchRolls.Sum(roll => roll.Quantity) <= 0m)
                return BadRequest("The multi-roll batch must have a positive total quantity.");
        }

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