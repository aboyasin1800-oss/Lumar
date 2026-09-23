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
    [HttpGet("employees/{employeeId:int}/settlements")] public async Task<ActionResult<IReadOnlyList<PayrollSettlementDto>>> GetSettlements(int employeeId, CancellationToken ct) { if (employeeId <= 0) return BadRequest("Employee id must be positive."); return Ok(await payroll.GetSettlementsAsync(employeeId, ct)); }
    [HttpGet("piece-wages")] public async Task<ActionResult<IReadOnlyList<PieceWageRecordDto>>> GetPieceWages(CancellationToken ct) => Ok(await payroll.GetPieceWagesAsync(ct));
    [HttpGet("piece-wage-rates")] public async Task<ActionResult<IReadOnlyList<PieceWageRateDto>>> GetPieceWageRates(CancellationToken ct) => Ok(await payroll.GetPieceWageRatesAsync(ct));
    [HttpPost("piece-wage-rates")] public async Task<ActionResult<PieceWageRateDto>> CreatePieceWageRate([FromBody] CreatePieceWageRateDto request, CancellationToken ct)
    {
        if (request is null) return BadRequest("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.PieceType)) return BadRequest("Piece type is required.");
        if (string.IsNullOrWhiteSpace(request.Stage)) return BadRequest("Stage is required.");
        if (request.WageRate < 0m) return BadRequest("Wage rate cannot be negative. Zero is allowed for non-paying stages.");

        var created = await payroll.CreatePieceWageRateAsync(request, ct);
        return CreatedAtAction(nameof(GetPieceWageRates), new { }, created);
    }
    [HttpPut("piece-wage-rates/{id:int}")] public async Task<ActionResult<PieceWageRateDto>> UpdatePieceWageRate(int id, [FromBody] UpdatePieceWageRateDto request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Piece wage rate id must be positive.");
        if (request is null) return BadRequest("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.PieceType)) return BadRequest("Piece type is required.");
        if (string.IsNullOrWhiteSpace(request.Stage)) return BadRequest("Stage is required.");
        if (request.WageRate < 0m) return BadRequest("Wage rate cannot be negative. Zero is allowed for non-paying stages.");

        var updated = await payroll.UpdatePieceWageRateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
    [HttpDelete("piece-wage-rates/{id:int}")] public async Task<IActionResult> DeletePieceWageRate(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Piece wage rate id must be positive.");
        var deleted = await payroll.DeletePieceWageRateAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
    [HttpPost("generate")] public async Task<ActionResult<GeneratePayrollResultDto>> Generate([FromBody] GeneratePayrollRequestDto request, CancellationToken ct) { if (request.StartDate > request.EndDate) return BadRequest("Payroll start date must be on or before the end date."); return Ok(await payroll.GenerateAsync(request, ct)); }
    [HttpPost("periods/{id:int}/approve")] public async Task<ActionResult<PayrollPeriodDto>> Approve(int id, [FromBody] ApprovePayrollRequestDto request, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll period id must be positive."); var item = await payroll.ApproveAsync(id, request, ct); return item is null ? NotFound() : Ok(item); }
    [HttpPost("records/{id:int}/pay")] public async Task<ActionResult<PayrollRecordDto>> Pay(int id, [FromBody] PayrollPaymentRequestDto request, CancellationToken ct) { if (id <= 0) return BadRequest("Payroll record id must be positive."); if (string.IsNullOrWhiteSpace(request.PaymentMethod)) return BadRequest("Payment method is required."); var item = await payroll.PayAsync(id, request, ct); return item is null ? NotFound() : Ok(item); }
    [HttpPost] [HttpPut("{id:int}")] [HttpDelete("{id:int}")] [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)] public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Payroll writes are handled through specific generation, approval, and payment endpoints.");
}