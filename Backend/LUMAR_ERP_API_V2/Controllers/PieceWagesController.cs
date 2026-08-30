using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("production/pieces")]
public sealed class PieceWagesController(IPieceWageService wages, IProductionService production) : ControllerBase
{
    [HttpGet("{id:int}/wages")]
    public async Task<ActionResult<IReadOnlyList<PieceWageDto>>> GetWages(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Piece id must be positive.");
        if (await production.GetPieceByIdAsync(id, ct) is null) return NotFound();
        return Ok(await wages.GetByPieceIdAsync(id, ct));
    }

    [HttpPost("{id:int}/wages")]
    [HttpPut("{id:int}/wages/{wageId:int}")]
    [HttpDelete("{id:int}/wages/{wageId:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Piece wage writes are disabled while LUMAR_ERP is read-only.");
}