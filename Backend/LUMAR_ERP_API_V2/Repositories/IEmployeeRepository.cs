using LUMAR_ERP_API_V2.DTOs.Employees;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<EmployeeListDto>> GetAllAsync(CancellationToken ct);
    Task<EmployeeDetailsDto?> GetByIdAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<EmployeeAttendanceDto>> GetAttendanceAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDrawDto>> GetDrawsAsync(string employeeCode, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeWorkflowDto>> GetWorkflowAsync(string employeeCode, CancellationToken ct);
}