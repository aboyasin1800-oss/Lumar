using LUMAR_ERP_API_V2.DTOs.Payroll; using LUMAR_ERP_API_V2.Services; using Microsoft.AspNetCore.Mvc;
namespace LUMAR_ERP_API_V2.Controllers;
[ApiController] [Route("payroll")] public sealed class PayrollController(IPayrollService payroll) : ControllerBase
{
    [HttpGet("periods")] public async Task<ActionResult<IReadOnlyList<PayrollPeriodDto>>> GetPeriods(CancellationToken ct) => Ok(await payroll.GetPeriodsAsync(ct));
    [HttpGet("periods/{id:int}")] public async Task<ActionResult<PayrollPeriodDto>> GetPeriod(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll period id must be positive."); var item = await payroll.GetPeriodAsync(id, ct); return item is null ? NotFound() : Ok(item); }
    [HttpGet("periods/{id:int}/employee-summaries")] public async Task<ActionResult<IReadOnlyList<EmployeePayrollSummaryDto>>> GetSummaries(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll period id must be positive."); if (await payroll.GetPeriodAsync(id, ct) is null) return NotFound(); return Ok(await payroll.GetEmployeeSummariesAsync(id, ct)); }
    [HttpGet("records")] public async Task<ActionResult<IReadOnlyList<PayrollRecordDto>>> GetRecords(CancellationToken ct) => Ok(await payroll.GetRecordsAsync(ct));
    [HttpGet("records/{id:int}")] public async Task<ActionResult<PayrollRecordDto>> GetRecord(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll record id must be positive."); var item = await payroll.GetRecordAsync(id, ct); return item is null ? NotFound() : Ok(item); }
    [HttpGet("records/{id:int}/items")] public async Task<ActionResult<IReadOnlyList<PayrollItemDto>>> GetItems(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll record id must be positive."); if (await payroll.GetRecordAsync(id, ct) is null) return NotFound(); return Ok(await payroll.GetItemsAsync(id, ct)); }
    [HttpGet("piece-wages")] public async Task<ActionResult<IReadOnlyList<PieceWageRecordDto>>> GetPieceWages(CancellationToken ct) => Ok(await payroll.GetPieceWagesAsync(ct));
    [HttpGet("piece-wage-rates")] public async Task<ActionResult<IReadOnlyList<PieceWageRateDto>>> GetPieceWageRates(CancellationToken ct) => Ok(await payroll.GetPieceWageRatesAsync(ct));
    [HttpPost] [HttpPut("{id:int}")] [HttpDelete("{id:int}")] [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)] public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Payroll writes are disabled while LUMAR_ERP is read-only.");
}