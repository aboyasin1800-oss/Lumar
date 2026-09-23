using LUMAR_ERP_API_V2.DTOs.Employees;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<EmployeeListDto>> GetAllAsync(CancellationToken ct);
    Task<EmployeeDetailsDto?> GetByIdAsync(int employeeId, CancellationToken ct);
    Task<EmployeeDetailsDto?> CreateAsync(CreateEmployeeDto request, CancellationToken ct);
    Task<EmployeeDetailsDto?> UpdateAsync(int employeeId, UpdateEmployeeDto request, CancellationToken ct);
    Task<EmployeeDetailsDto?> ActivateAsync(int employeeId, CancellationToken ct);
    Task<EmployeeDetailsDto?> DeactivateAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<EmployeeAttendanceDto>> GetAttendanceAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDrawDto>> GetDrawsAsync(string employeeCode, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeWorkflowDto>> GetWorkflowAsync(string employeeCode, CancellationToken ct);
    Task<IReadOnlyList<EmployeePieceRateAssignmentDto>> GetPieceRateAssignmentsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeContractTemplateDto>> GetContractTemplatesAsync(CancellationToken ct);
    Task<EmployeeContractTemplateDto?> GetContractTemplateByIdAsync(int templateId, CancellationToken ct);
    Task<EmployeeContractTemplateDto> CreateContractTemplateAsync(EmployeeContractTemplateWriteDto request, CancellationToken ct);
    Task<EmployeeContractTemplateDto?> UpdateContractTemplateAsync(int templateId, EmployeeContractTemplateUpdateDto request, CancellationToken ct);
    Task<EmployeeContractDto?> GetContractAsync(int employeeId, CancellationToken ct);
    Task<EmployeeContractDto?> UpsertContractAsync(int employeeId, EmployeeContractDto contract, CancellationToken ct);
    Task<EmployeeContractDto?> GenerateContractAsync(int employeeId, CancellationToken ct);
}