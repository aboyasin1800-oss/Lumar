using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("ready-sales")]
public sealed class ReadyMadeSalesController(IReadyMadeSalesService service, ILogger<ReadyMadeSalesController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ReadyMadeSaleResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReadyMadeSaleResultDto>> Create([FromBody] CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
    {
        if (sale is null) return BadRequest("بيانات البيع مطلوبة.");
        if (sale.Items is null || sale.Items.Count == 0) return BadRequest("يجب إضافة منتج واحد على الأقل.");
        try
        {
            var result = await service.CreateAsync(sale, cancellationToken);
            return CreatedAtAction(nameof(Create), new { id = result.OrderId }, result);
        }
        catch (ArgumentException exception)
        {
            LogFailure(exception, StatusCodes.Status400BadRequest);
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            LogFailure(exception, StatusCodes.Status409Conflict);
            return Conflict(exception.Message);
        }
        catch (SqlException exception)
        {
            LogFailure(exception, StatusCodes.Status500InternalServerError);
            return StatusCode(StatusCodes.Status500InternalServerError, "تعذر إنشاء الفاتورة.");
        }
        catch (Exception exception)
        {
            LogFailure(exception, StatusCodes.Status500InternalServerError);
            return StatusCode(StatusCodes.Status500InternalServerError, "تعذر حفظ عملية البيع.");
        }
    }

    private void LogFailure(Exception exception, int statusCode)
    {
        int? sqlErrorNumber = exception is SqlException sqlException ? sqlException.Number : null;
        logger.LogError(
            exception,
            "Ready-made sale HTTP failure. HttpStatus={HttpStatus}; CorrelationId={CorrelationId}; SqlErrorNumber={SqlErrorNumber}; InnerException={InnerException}",
            statusCode,
            HttpContext.TraceIdentifier,
            sqlErrorNumber,
            exception.InnerException?.ToString() ?? "<none>");
    }
}