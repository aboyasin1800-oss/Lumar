using LUMAR_ERP_API_V2.DTOs.Push;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("push")]
public sealed class PushDevicesController(IPushDeviceService service) : ControllerBase
{
    [HttpPost("devices/register")]
    public async Task<ActionResult<PushDeviceSummaryDto>> Register([FromBody] RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.RegisterAsync(request, cancellationToken);
            return result is null ? StatusCode(500, "Unable to register push device.") : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("devices/{deviceId:int}/refresh")]
    public async Task<ActionResult<PushDeviceSummaryDto>> Refresh(int deviceId, [FromBody] RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.RefreshAsync(deviceId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("devices/{deviceId:int}/disable")]
    public async Task<ActionResult<PushDeviceSummaryDto>> Disable(int deviceId, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.DisableAsync(deviceId, reason, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<PushDeviceSummaryDto>>> GetActive(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetActiveAsync(cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
