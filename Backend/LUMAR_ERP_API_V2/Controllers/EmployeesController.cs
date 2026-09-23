using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeListDto>>> GetAll(CancellationToken ct) => Ok(await employees.GetAllAsync(ct));

    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartments(CancellationToken ct) => Ok(await employees.GetDepartmentsAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var employee = await employees.GetByIdAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [ProducesResponseType<EmployeeDetailsDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeDetailsDto>> CreateEmployee(CreateEmployeeDto request, CancellationToken ct)
    {
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
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var employee = await employees.ActivateAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPut("{id:int}/deactivate")]
    public async Task<ActionResult<EmployeeDetailsDto>> DeactivateEmployee(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Employee id must be positive.");
        var employee = await employees.DeactivateAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("contract-templates")]
    public async Task<ActionResult<IReadOnlyList<EmployeeContractTemplateDto>>> GetContractTemplates(CancellationToken ct) => Ok(await employees.GetContractTemplatesAsync(ct));

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
        var employee = await GetEmployeeOrError(id, ct);
        if (employee.Error is not null) return employee.Error;
        var contract = await employees.GetContractAsync(id, ct);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpPost("{id:int}/contract/generate")]
    public async Task<ActionResult<EmployeeContractDto>> GenerateContract(int id, CancellationToken ct)
    {
        var employee = await GetEmployeeOrError(id, ct);
        if (employee.Error is not null) return employee.Error;
        var contract = await employees.GenerateContractAsync(id, ct);
        return contract is null ? NotFound() : Ok(contract);
    }

    [HttpGet("{id:int}/attendance")] public async Task<ActionResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendance(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetAttendanceAsync(id, ct)); }
    [HttpGet("{id:int}/leave-requests")] public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetLeaveRequests(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetLeaveRequestsAsync(id, ct)); }
    [HttpGet("{id:int}/draws")] public async Task<ActionResult<IReadOnlyList<EmployeeDrawDto>>> GetDraws(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetDrawsAsync(employee.Value!.EmployeeCode, ct)); }
    [HttpGet("{id:int}/documents")] public async Task<ActionResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocuments(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetDocumentsAsync(id, ct)); }
    [HttpGet("{id:int}/workflow")] public async Task<ActionResult<IReadOnlyList<EmployeeWorkflowDto>>> GetWorkflow(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetWorkflowAsync(employee.Value!.EmployeeCode, ct)); }

    private async Task<(EmployeeDetailsDto? Value, ActionResult? Error)> GetEmployeeOrError(int id, CancellationToken ct)
    { if (id <= 0) return (null, BadRequest("Employee id must be positive.")); var employee = await employees.GetByIdAsync(id, ct); return employee is null ? (null, NotFound()) : (employee, null); }
}