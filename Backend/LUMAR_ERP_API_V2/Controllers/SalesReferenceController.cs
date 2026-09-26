using LUMAR_ERP_API_V2.DTOs.SalesReference;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("sales-reference")]
public sealed class SalesReferenceController(ISalesReferenceService service) : ControllerBase
{
    [HttpGet("version")]
    [ProducesResponseType<SalesReferenceVersionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesReferenceVersionResponse>> GetVersion(
        CancellationToken cancellationToken) => Ok(await service.GetVersionAsync(cancellationToken));

    [HttpGet("snapshot")]
    [ProducesResponseType<SalesReferenceSnapshotResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesReferenceSnapshotResponse>> GetSnapshot(
        CancellationToken cancellationToken) => Ok(await service.GetSnapshotAsync(cancellationToken));
}