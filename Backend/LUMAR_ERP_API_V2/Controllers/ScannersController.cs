using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("production")]
public sealed class ScannersController(IScannerService service) : ControllerBase
{
    [HttpGet("scanners")]
    public Task<IReadOnlyList<ScannerDto>> GetScanners(CancellationToken ct) => service.GetScannersAsync(ct);

    [HttpGet("live-scan")]
    public Task<IReadOnlyList<LiveScanDto>> GetLiveScans(CancellationToken ct) => service.GetLiveScansAsync(ct);

    [HttpPost("scanners")]
    [HttpPut("scanners/{id:int}")]
    [HttpDelete("scanners/{id:int}")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Scanner writes are disabled while LUMAR_ERP is read-only.");
}