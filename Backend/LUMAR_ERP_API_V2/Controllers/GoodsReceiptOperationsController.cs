using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("inventory/operations")]
public sealed class GoodsReceiptOperationsController(
    IGoodsReceiptWorkflowCoordinator coordinator,
    IAuthenticatedUserContext userContext,
    IPurchasingService purchasingService) : ControllerBase
{
    [HttpPost("receipts")]
    public async Task<ActionResult<GoodsReceiptRuntimeResult>> CreateReceipt(CreateGoodsReceiptRequestDto request, CancellationToken cancellationToken)
    {
        if (request.SupplierId <= 0 || request.WarehouseId <= 0 || string.IsNullOrWhiteSpace(request.ReceiptNumber) || request.SourceOperationId == Guid.Empty || request.Items.Count == 0)
            return BadRequest("Goods receipt data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.InventoryReceive, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return StatusCode(StatusCodes.Status201Created, await coordinator.CreateAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    [HttpPost("receipts/reversals")]
    public async Task<ActionResult<GoodsReceiptReversalResult>> ReverseReceipt(ReverseGoodsReceiptRequestDto request, CancellationToken cancellationToken)
    {
        if (request.GoodsReceiptId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("Goods receipt reversal data is incomplete.");
        var auth = await AuthorizeAsync(Es7Permission.InventoryReverse, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        return Ok(await coordinator.ReverseAsync(request, auth.User!, CorrelationId, cancellationToken));
    }

    [HttpGet("receipts/{id:int}/matching")]
    public async Task<ActionResult<GoodsReceiptMatchingQueryDto>> GetMatching(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Goods receipt id must be positive.");
        var auth = await AuthorizeAsync(Es7Permission.PurchasingView, cancellationToken);
        if (auth.Error is not null) return auth.Error;
        var receipt = await purchasingService.GetReceiptAsync(id, cancellationToken);
        if (receipt is null) return NotFound();
        return Ok(new GoodsReceiptMatchingQueryDto(receipt, await purchasingService.GetReceiptItemsAsync(id, cancellationToken)));
    }

    private async Task<(CurrentUserDto? User, ActionResult? Error)> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return (null, Unauthorized());
        return Es7PermissionPolicy.HasPermission(user, permission) ? (user, null) : (null, Forbid());
    }

    private string CorrelationId => ControllerContext.HttpContext?.TraceIdentifier ?? "unbound";
}