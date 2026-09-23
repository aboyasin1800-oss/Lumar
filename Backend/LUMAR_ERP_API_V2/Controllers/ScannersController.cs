using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("production")]
public sealed class ScannersController(IScannerService service) : ControllerBase
{
    [HttpGet("scanners")]
    public async Task<ActionResult<IReadOnlyList<ScannerDto>>> GetScanners(CancellationToken ct) => Ok(await service.GetScannersAsync(ct));

    [HttpGet("scanners/{id:int}")]
    public async Task<ActionResult<ScannerDto>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Scanner id must be positive.");
        var scanner = await service.GetByIdAsync(id, ct);
        return scanner is null ? NotFound() : Ok(scanner);
    }

    [HttpGet("live-scan")]
    public Task<IReadOnlyList<LiveScanDto>> GetLiveScans(CancellationToken ct) => service.GetLiveScansAsync(ct);

    [HttpPost("scanners")]
    [ProducesResponseType<ScannerDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ScannerDto>> CreateScanner(CreateScannerDto request, CancellationToken ct)
    {
        try
        {
            var created = await service.CreateAsync(request, ct);
            return created is null
                ? StatusCode(StatusCodes.Status500InternalServerError, "Scanner could not be created.")
                : CreatedAtAction(nameof(GetById), new { id = created.ScannerId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict("ScannerCode already exists.");
        }
    }

    [HttpPut("scanners/{id:int}")]
    public async Task<ActionResult<ScannerDto>> UpdateScanner(int id, UpdateScannerDto request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Scanner id must be positive.");
        try
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict("ScannerCode already exists.");
        }
    }

    [HttpPut("scanners/{id:int}/activate")]
    public async Task<ActionResult<ScannerDto>> ActivateScanner(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Scanner id must be positive.");
        var scanner = await service.ActivateAsync(id, ct);
        return scanner is null ? NotFound() : Ok(scanner);
    }

    [HttpPut("scanners/{id:int}/deactivate")]
    public async Task<ActionResult<ScannerDto>> DeactivateScanner(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Scanner id must be positive.");
        var scanner = await service.DeactivateAsync(id, ct);
        return scanner is null ? NotFound() : Ok(scanner);
    }
}