using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<EmployeeListDto>>> GetAll(CancellationToken ct) => Ok(await employees.GetAllAsync(ct));
    [HttpGet("departments")] public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetDepartments(CancellationToken ct) => Ok(await employees.GetDepartmentsAsync(ct));
    [HttpGet("{id:int}")] public async Task<ActionResult<EmployeeDetailsDto>> GetById(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Employee id must be positive."); var employee = await employees.GetByIdAsync(id, ct); return employee is null ? NotFound() : Ok(employee); }
    [HttpGet("{id:int}/attendance")] public async Task<ActionResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendance(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetAttendanceAsync(id, ct)); }
    [HttpGet("{id:int}/leave-requests")] public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetLeaveRequests(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetLeaveRequestsAsync(id, ct)); }
    [HttpGet("{id:int}/draws")] public async Task<ActionResult<IReadOnlyList<EmployeeDrawDto>>> GetDraws(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetDrawsAsync(employee.Value!.EmployeeCode, ct)); }
    [HttpGet("{id:int}/documents")] public async Task<ActionResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocuments(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetDocumentsAsync(id, ct)); }
    [HttpGet("{id:int}/workflow")] public async Task<ActionResult<IReadOnlyList<EmployeeWorkflowDto>>> GetWorkflow(int id, CancellationToken ct) { var employee = await GetEmployeeOrError(id, ct); return employee.Error ?? Ok(await employees.GetWorkflowAsync(employee.Value!.EmployeeCode, ct)); }
    [HttpPost] [HttpPut("{id:int}")] [HttpDelete("{id:int}")] [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)] public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Employee writes are disabled while LUMAR_ERP is read-only.");
    private async Task<(EmployeeDetailsDto? Value, ActionResult? Error)> GetEmployeeOrError(int id, CancellationToken ct)
    { if (id <= 0) return (null, BadRequest("Employee id must be positive.")); var employee = await employees.GetByIdAsync(id, ct); return employee is null ? (null, NotFound()) : (employee, null); }
}