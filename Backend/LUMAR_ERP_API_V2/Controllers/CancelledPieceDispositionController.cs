using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("cancelled-piece-dispositions")]
public sealed class CancelledPieceDispositionController(ICancelledPieceDispositionService service) : ControllerBase
{
    [HttpGet("{pieceId:int}")]
    [ProducesResponseType<CancelledPieceDispositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancelledPieceDispositionDto>> GetByPieceId(int pieceId, CancellationToken cancellationToken)
    {
        if (pieceId <= 0) return BadRequest("Piece id must be positive.");
        var result = await service.GetByPieceIdAsync(pieceId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<CancelledPieceDispositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CancelledPieceDispositionDto>> SaveDecision([FromBody] CancelledPieceDispositionDecisionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.PieceId <= 0) return BadRequest("Piece id must be positive.");
        if (string.IsNullOrWhiteSpace(request.Decision)) return BadRequest("A valid decision is required.");

        try
        {
            var result = await service.SaveDecisionAsync(request.PieceId, request.Decision, request.Reason, request.DecidedBy, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{pieceId:int}/execute")]
    [ProducesResponseType<CancelledPieceDispositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CancelledPieceDispositionDto>> ExecuteDecision(int pieceId, CancellationToken cancellationToken)
    {
        if (pieceId <= 0) return BadRequest("Piece id must be positive.");

        try
        {
            var result = await service.ExecuteDecisionAsync(pieceId, cancellationToken);
            return result is null ? BadRequest("The decision could not be executed.") : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
