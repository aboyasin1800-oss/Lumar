using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.DTOs.Payroll;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController(IEmployeeService employees, IAuthenticatedUserContext userContext, OperationalSqlConnectionFactory operationalConnections) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeListDto>>> GetAll(CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts can only access their own data.");

        return Ok(await employees.GetAllAsync(ct));
    }

    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartments(CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts can only access their own data.");

        return Ok(await employees.GetDepartmentsAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var employee = await employees.GetByIdAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [ProducesResponseType<EmployeeDetailsDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeDetailsDto>> CreateEmployee(CreateEmployeeDto request, CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts cannot create employee records.");

        try
        {
            var created = await employees.CreateAsync(request, ct);
            return created is null ? StatusCode(StatusCodes.Status500InternalServerError, "Employee could not be created.") : CreatedAtAction(nameof(GetById), new { id = created.EmployeeId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict("EmployeeCode already exists.");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDetailsDto>> UpdateEmployee(int id, UpdateEmployeeDto request, CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts cannot modify employee records.");

        if (id <= 0) return BadRequest("Employee id must be positive.");
        try
        {
            var updated = await employees.UpdateAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (Microsoft.Data.SqlClient.SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict("EmployeeCode already exists.");
        }
    }

    [HttpPut("{id:int}/activate")]
    public async Task<ActionResult<EmployeeDetailsDto>> ActivateEmployee(int id, CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts cannot change employee status.");

        if (id <= 0) return BadRequest("Employee id must be positive.");
        var employee = await employees.ActivateAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPut("{id:int}/deactivate")]
    public async Task<ActionResult<EmployeeDetailsDto>> DeactivateEmployee(int id, CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts cannot change employee status.");

        if (id <= 0) return BadRequest("Employee id must be positive.");
        var employee = await employees.DeactivateAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("contract-templates")]
    public async Task<ActionResult<IReadOnlyList<EmployeeContractTemplateDto>>> GetContractTemplates(CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts can only access their own data.");

        return Ok(await employees.GetContractTemplatesAsync(ct));
    }

    [HttpPost("contract-templates")]
    public async Task<ActionResult<EmployeeContractTemplateDto>> CreateContractTemplate([FromBody] EmployeeContractTemplateWriteDto request, CancellationToken ct)
    {
        try
        {
            var created = await employees.CreateContractTemplateAsync(request, ct);
            return CreatedAtAction(nameof(GetContractTemplates), created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("contract-templates/{templateId:int}")]
    public async Task<ActionResult<EmployeeContractTemplateDto>> UpdateContractTemplate(int templateId, [FromBody] EmployeeContractTemplateUpdateDto request, CancellationToken ct)
    {
        try
        {
            var updated = await employees.UpdateContractTemplateAsync(templateId, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("{id:int}/contract")]
    public async Task<ActionResult<EmployeeContractDto>> GetContract(int id, CancellationToken ct)
    {
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var employee = await GetEmployeeOrError(id, ct);
        if (employee.Error is not null) return employee.Error;
        var contract = await employees.GetContractAsync(id, ct);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpPost("{id:int}/contract/generate")]
    public async Task<ActionResult<EmployeeContractDto>> GenerateContract(int id, CancellationToken ct)
    {
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var employee = await GetEmployeeOrError(id, ct);
        if (employee.Error is not null) return employee.Error;
        var contract = await employees.GenerateContractAsync(id, ct);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpGet("{id:int}/attendance")] public async Task<ActionResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendance(int id, CancellationToken ct) { var authError = await EnsureCurrentEmployeeAccessAsync(id, ct); if (authError is not null) return authError; var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetAttendanceAsync(id, ct)); }
    [HttpGet("{id:int}/leave-requests")] public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetLeaveRequests(int id, CancellationToken ct) { var authError = await EnsureCurrentEmployeeAccessAsync(id, ct); if (authError is not null) return authError; var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetLeaveRequestsAsync(id, ct)); }
    [HttpGet("{id:int}/draws")] public async Task<ActionResult<IReadOnlyList<EmployeeDrawDto>>> GetDraws(int id, CancellationToken ct) { var authError = await EnsureCurrentEmployeeAccessAsync(id, ct); if (authError is not null) return authError; var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetDrawsAsync(employee.Value!.EmployeeCode, ct)); }
    [HttpGet("me/production")] public async Task<ActionResult<IReadOnlyList<TrackingEventDto>>> GetCurrentEmployeeProduction(CancellationToken ct) { var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct); if (currentEmployeeId.Error is not null) return currentEmployeeId.Error; return Ok(await employees.GetSelfProductionAsync(currentEmployeeId.Value!.Value, ct)); }
    [HttpGet("me/piece-wages")] public async Task<ActionResult<IReadOnlyList<PieceWageRecordDto>>> GetCurrentEmployeePieceWages(CancellationToken ct) { var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct); if (currentEmployeeId.Error is not null) return currentEmployeeId.Error; return Ok(await employees.GetSelfPieceWagesAsync(currentEmployeeId.Value!.Value, ct)); }
    [HttpGet("me/draws")] public async Task<ActionResult<IReadOnlyList<EmployeeDrawDto>>> GetCurrentEmployeeDraws(CancellationToken ct) { var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct); if (currentEmployeeId.Error is not null) return currentEmployeeId.Error; return Ok(await employees.GetSelfDrawsAsync(currentEmployeeId.Value!.Value, ct)); }
    [HttpGet("me/settlements")] public async Task<ActionResult<IReadOnlyList<PayrollSettlementDto>>> GetCurrentEmployeeSettlements(CancellationToken ct) { var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct); if (currentEmployeeId.Error is not null) return currentEmployeeId.Error; return Ok(await employees.GetSelfSettlementsAsync(currentEmployeeId.Value!.Value, ct)); }
    [HttpGet("{id:int}/financial-summary")]
    public async Task<ActionResult<EmployeeSelfFinancialSummaryDto>> GetEmployeeFinancialSummary(int id, [FromQuery] int? periodId, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var summary = await employees.GetSelfFinancialSummaryAsync(id, periodId, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:int}/monthly-statement")]
    public async Task<ActionResult<EmployeeSelfMonthlyStatementDto>> GetEmployeeMonthlyStatement(int id, [FromQuery] int periodId, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        if (periodId <= 0) return BadRequest("Payroll period id must be positive.");

        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var statement = await employees.GetSelfMonthlyStatementAsync(id, periodId, ct);
        return statement is null ? NotFound() : Ok(statement);
    }

    [HttpPost("{id:int}/advances")]
    public async Task<ActionResult<EmployeeFinancialActionResult>> CreateEmployeeAdvance(int id, [FromBody] CreateEmployeeAdvanceRequestDto request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        if (request is null) return BadRequest("Advance payload is required.");
        if (request.EmployeeId <= 0 || request.Amount <= 0 || request.CashAccountId <= 0 || request.SourceOperationId == Guid.Empty)
            return BadRequest("Advance payload is incomplete.");

        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is null) return Unauthorized();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var runtime = new EmployeeAccrualRuntime();
            var reference = request.ReferenceNumber ?? $"ADV-{request.EmployeeId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var eventId = await runtime.PostAdvanceAsync(connection, transaction, request.EmployeeId, request.AdvanceDate, request.Amount, request.CashAccountId, request.SourceOperationId, reference, user.Username ?? "system", ct);
            var before = await new EmployeeLedgerReader().GetBalanceAsync(connection, request.EmployeeId, ct);
            var result = new EmployeeFinancialActionResult(Math.Abs(request.SourceOperationId.GetHashCode()), eventId, request.Amount, request.AdvanceDate.ToDateTime(TimeOnly.MinValue), "Advance", reference, before.NetBalance, before.NetBalance - request.Amount, false);
            transaction.Commit();
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    [HttpPost("{id:int}/daily-expenses")]
    public async Task<ActionResult<EmployeeFinancialActionResult>> CreateEmployeeDailyExpense(int id, [FromBody] CreateEmployeeExpenseRequestDto request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        if (request is null) return BadRequest("Expense payload is required.");
        if (request.EmployeeId <= 0 || request.Amount <= 0 || request.CashAccountId <= 0 || request.SourceOperationId == Guid.Empty)
            return BadRequest("Expense payload is incomplete.");

        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is null) return Unauthorized();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var runtime = new EmployeeAccrualRuntime();
            var reference = request.ReferenceNumber ?? $"EXP-{request.EmployeeId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var eventId = await runtime.PostDailyExpenseAsync(connection, transaction, request.EmployeeId, request.ExpenseDate, request.Amount, request.CashAccountId, request.SourceOperationId, reference, user.Username ?? "system", ct);
            var before = await new EmployeeLedgerReader().GetBalanceAsync(connection, request.EmployeeId, ct);
            var result = new EmployeeFinancialActionResult(Math.Abs(request.SourceOperationId.GetHashCode()), eventId, request.Amount, request.ExpenseDate.ToDateTime(TimeOnly.MinValue), "DailyExpense", reference, before.NetBalance, before.NetBalance - request.Amount, false);
            transaction.Commit();
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<EmployeeFinancialActionResult>> CreateEmployeePayment(int id, [FromBody] CreateEmployeePaymentRequestDto request, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        if (request is null) return BadRequest("Payment payload is required.");
        if (request.EmployeeId <= 0 || request.Amount <= 0 || request.CashAccountId <= 0 || request.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.PaymentMethod))
            return BadRequest("Payment payload is incomplete.");

        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is null) return Unauthorized();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var service = new EmployeePaymentService();
            var reference = request.ReferenceNumber ?? $"PAY-{request.EmployeeId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var paymentRequest = new EmployeePaymentRequest(request.EmployeeId, request.Amount, request.PaymentDate, request.AsOfDate, request.CashAccountId, request.PaymentMethod, reference, request.SourceOperationId, user.Username ?? "system", request.CurrencyCode);
            var result = await service.PayAsync(connection, transaction, paymentRequest, ct);
            transaction.Commit();
            return StatusCode(StatusCodes.Status201Created, new EmployeeFinancialActionResult(result.EmployeePaymentId, result.AccountingEventId, request.Amount, request.PaymentDate.ToDateTime(TimeOnly.MinValue), "Payment", reference, result.BalanceBefore, result.BalanceAfter, result.IsExisting));
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    [HttpGet("{id:int}/ledger")]
    public async Task<ActionResult<IReadOnlyList<EmployeeLedgerEntryDto>>> GetEmployeeLedger(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);

        const string sql = @"SELECT EmployeeLedgerEntryId, EmployeeId, EntryType, Amount, BalanceEffect, EffectiveDate, SourceType, SourceId, ReferenceNumber, Status, CreatedBy, OccurredAt FROM dbo.EmployeeLedgerEntries WHERE EmployeeId=@employeeId ORDER BY EffectiveDate DESC, EmployeeLedgerEntryId DESC;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@employeeId", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<EmployeeLedgerEntryDto>();

        while (await reader.ReadAsync(ct))
        {
            list.Add(new EmployeeLedgerEntryDto(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDateTime(5),
                reader.GetString(6),
                reader.GetInt64(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetDateTime(11)));
        }

        return Ok(list);
    }

    [HttpGet("me/financial-summary")]
    public async Task<ActionResult<EmployeeSelfFinancialSummaryDto>> GetCurrentEmployeeFinancialSummary([FromQuery] int? periodId, CancellationToken ct)
    {
        var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct);
        if (currentEmployeeId.Error is not null) return currentEmployeeId.Error;

        var summary = await employees.GetSelfFinancialSummaryAsync(currentEmployeeId.Value!.Value, periodId, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("me/monthly-statement")]
    public async Task<ActionResult<EmployeeSelfMonthlyStatementDto>> GetCurrentEmployeeMonthlyStatement([FromQuery] int periodId, CancellationToken ct)
    {
        if (periodId <= 0) return BadRequest("Payroll period id must be positive.");

        var currentEmployeeId = await GetCurrentEmployeeIdOrErrorAsync(ct);
        if (currentEmployeeId.Error is not null) return currentEmployeeId.Error;

        var statement = await employees.GetSelfMonthlyStatementAsync(currentEmployeeId.Value!.Value, periodId, ct);
        return statement is null ? NotFound() : Ok(statement);
    }

    [HttpGet("{id:int}/documents")]
    public async Task<ActionResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocuments(int id, CancellationToken ct)
    {
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var employee = await GetEmployeeOrError(id, ct);
        return employee.Error ?? Ok(await employees.GetDocumentsAsync(id, ct));
    }

    [HttpGet("{id:int}/workflow")]
    public async Task<ActionResult<IReadOnlyList<EmployeeWorkflowDto>>> GetWorkflow(int id, CancellationToken ct)
    {
        var authError = await EnsureCurrentEmployeeAccessAsync(id, ct);
        if (authError is not null) return authError;

        var employee = await GetEmployeeOrError(id, ct);
        return employee.Error ?? Ok(await employees.GetWorkflowAsync(employee.Value!.EmployeeCode, ct));
    }

    private async Task<ActionResult?> EnsureCurrentEmployeeAccessAsync(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");

        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is null) return Unauthorized();

        if (!string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!user.EmployeeId.HasValue)
            return StatusCode(StatusCodes.Status403Forbidden, "Employee account is not mapped to a valid employee record.");

        if (user.EmployeeId.Value != id)
            return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts can only access their own data.");

        return null;
    }

    private async Task<(int? Value, ActionResult? Error)> GetCurrentEmployeeIdOrErrorAsync(CancellationToken ct)
    {
        var user = await userContext.GetCurrentUserAsync(ct);
        if (user is null) return (null, Unauthorized());

        if (!string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return (null, StatusCode(StatusCodes.Status403Forbidden, "This endpoint is only for employee accounts."));

        if (!user.EmployeeId.HasValue)
            return (null, StatusCode(StatusCodes.Status403Forbidden, "Employee account is not mapped to a valid employee record."));

        return (user.EmployeeId.Value, null);
    }

    private async Task<(EmployeeDetailsDto? Value, ActionResult? Error)> GetEmployeeOrError(int id, CancellationToken ct)
    { if (id <= 0) return (null, BadRequest("Employee id must be positive.")); var employee = await employees.GetByIdAsync(id, ct); return employee is null ? (null, NotFound()) : (employee, null); }
}