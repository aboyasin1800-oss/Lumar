using LUMAR_ERP_API_V2.DTOs.Employees;
namespace LUMAR_ERP_API_V2.Services;
public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeListDto>> GetAllAsync(CancellationToken ct);
    Task<EmployeeDetailsDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<EmployeeDetailsDto?> CreateAsync(CreateEmployeeDto request, CancellationToken ct);
    Task<EmployeeDetailsDto?> UpdateAsync(int employeeId, UpdateEmployeeDto request, CancellationToken ct);
    Task<EmployeeDetailsDto?> ActivateAsync(int employeeId, CancellationToken ct);
    Task<EmployeeDetailsDto?> DeactivateAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct);
    Task<IReadOnlyList<EmployeeAttendanceDto>> GetAttendanceAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDrawDto>> GetDrawsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<EmployeeWorkflowDto>> GetWorkflowAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<EmployeePieceRateAssignmentDto>> GetPieceRateAssignmentsAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeContractTemplateDto>> GetContractTemplatesAsync(CancellationToken ct);
    Task<EmployeeContractTemplateDto?> GetContractTemplateByIdAsync(int templateId, CancellationToken ct);
    Task<EmployeeContractTemplateDto> CreateContractTemplateAsync(EmployeeContractTemplateWriteDto request, CancellationToken ct);
    Task<EmployeeContractTemplateDto?> UpdateContractTemplateAsync(int templateId, EmployeeContractTemplateUpdateDto request, CancellationToken ct);
    Task<EmployeeContractDto?> GetContractAsync(int employeeId, CancellationToken ct);
    Task<EmployeeContractDto?> UpsertContractAsync(int employeeId, EmployeeContractDto contract, CancellationToken ct);
    Task<EmployeeContractDto?> GenerateContractAsync(int employeeId, CancellationToken ct);
}