using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Printing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("printing")]
public sealed class PrintingController(IPrintingService printing, IAuthService auth, ILogger<PrintingController> logger) : ControllerBase
{
    [HttpGet("pieces/{pieceId:int}/history")]
    public Task<ActionResult<IReadOnlyList<MeasurementCardPrintHistoryDto>>> GetPieceHistory(int pieceId, CancellationToken cancellationToken) =>
        GetHistoryCore(pieceId, false, cancellationToken);

    [HttpGet("measurement-card-status-summary")]
    public async Task<ActionResult<MeasurementCardStatusSummaryDto>> GetStatusSummary([FromQuery] int? orderId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.GetStatusSummaryAsync(orderId, cancellationToken));
    }

    [HttpGet("eligible-snapshot")]
    public async Task<ActionResult<MeasurementCardEligibleSnapshotDto>> GetEligibleSnapshot([FromQuery] int? orderId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.GetEligibleSnapshotAsync(orderId, cancellationToken));
    }

    [HttpPost("bulk/prepare")]
    public async Task<ActionResult<MeasurementCardBulkPrepareResultDto>> PrepareBulk(MeasurementCardBulkPrepareDto request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await printing.PrepareBulkAsync(request, user, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("bulk/confirm")]
    public async Task<ActionResult<MeasurementCardBulkConfirmationResultDto>> ConfirmBulk(MeasurementCardBulkConfirmationDto request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.ConfirmBulkAsync(request, user, cancellationToken));
    }

    [HttpPost("bulk/cancel")]
    public async Task<ActionResult<MeasurementCardBulkConfirmationResultDto>> CancelBulk(MeasurementCardBulkConfirmationDto request, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.CancelBulkAsync(request, reason, user, cancellationToken));
    }

    [HttpPost("reserved/recover")]
    public async Task<ActionResult<MeasurementCardReservedRecoveryDto>> RecoverReserved([FromQuery] int? orderId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.RecoverReservedAsync(orderId, cancellationToken));
    }

    [HttpGet("readymade-pieces/{pieceId:int}/history")]
    public Task<ActionResult<IReadOnlyList<MeasurementCardPrintHistoryDto>>> GetReadyMadePieceHistory(int pieceId, CancellationToken cancellationToken) =>
        GetHistoryCore(pieceId, true, cancellationToken);

    [HttpPost("pieces/{pieceId:int}/prepare")]
    public Task<ActionResult<MeasurementCardPrintHistoryDto>> PreparePiece(int pieceId, PrepareMeasurementCardPrintDto request, CancellationToken cancellationToken) =>
        PrepareCore(pieceId, false, request, cancellationToken);

    [HttpPost("readymade-pieces/{pieceId:int}/prepare")]
    public Task<ActionResult<MeasurementCardPrintHistoryDto>> PrepareReadyMadePiece(int pieceId, PrepareMeasurementCardPrintDto request, CancellationToken cancellationToken) =>
        PrepareCore(pieceId, true, request, cancellationToken);

    [HttpPost("history/{printHistoryId:int}/complete")]
    public Task<ActionResult<MeasurementCardPrintHistoryDto>> Complete(int printHistoryId, CompleteMeasurementCardPrintDto request, CancellationToken cancellationToken) =>
        CompleteCore(printHistoryId, request, cancellationToken);

    [HttpPost("history/{printHistoryId:int}/fail")]
    public Task<ActionResult<MeasurementCardPrintHistoryDto>> Fail(int printHistoryId, FailMeasurementCardPrintDto request, CancellationToken cancellationToken) =>
        FailCore(printHistoryId, request, cancellationToken);

    private async Task<ActionResult<IReadOnlyList<MeasurementCardPrintHistoryDto>>> GetHistoryCore(int pieceId, bool isReadyMade, CancellationToken cancellationToken)
    {
        if (pieceId <= 0) return BadRequest("معرف القطعة غير صالح.");
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await printing.GetPieceHistoryAsync(pieceId, isReadyMade, cancellationToken));
    }

    private async Task<ActionResult<MeasurementCardPrintHistoryDto>> PrepareCore(int pieceId, bool isReadyMade, PrepareMeasurementCardPrintDto request, CancellationToken cancellationToken)
    {
        if (pieceId <= 0) return BadRequest("معرف القطعة غير صالح.");
        if (request is null) return BadRequest("بيانات الطباعة مطلوبة.");
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await printing.PrepareAsync(pieceId, isReadyMade, request, user, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Measurement card print preparation failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, "تعذر تجهيز عملية الطباعة.");
        }
    }

    private async Task<ActionResult<MeasurementCardPrintHistoryDto>> CompleteCore(int printHistoryId, CompleteMeasurementCardPrintDto request, CancellationToken cancellationToken)
    {
        if (printHistoryId <= 0) return BadRequest("معرف سجل الطباعة غير صالح.");
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await printing.CompleteAsync(printHistoryId, request?.CashAccountId, user, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Measurement card print completion failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, "تعذر اعتماد عملية الطباعة.");
        }
    }

    private async Task<ActionResult<MeasurementCardPrintHistoryDto>> FailCore(int printHistoryId, FailMeasurementCardPrintDto request, CancellationToken cancellationToken)
    {
        if (printHistoryId <= 0) return BadRequest("معرف سجل الطباعة غير صالح.");
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            var result = await printing.FailAsync(printHistoryId, request?.FailureReason, user, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Measurement card print failure recording failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, "تعذر إغلاق محاولة الطباعة.");
        }
    }

    private async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : string.Empty;
        return await auth.GetCurrentUserAsync(token, cancellationToken);
    }
}
