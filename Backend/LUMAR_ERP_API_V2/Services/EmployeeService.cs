using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Repositories;
namespace LUMAR_ERP_API_V2.Services;
public sealed class EmployeeService(IEmployeeRepository repository) : IEmployeeService
{
    public Task<IReadOnlyList<EmployeeListDto>> GetAllAsync(CancellationToken ct) => repository.GetAllAsync(ct);
    public Task<EmployeeDetailsDto?> GetByIdAsync(int id, CancellationToken ct) => repository.GetByIdAsync(id, ct);
    public Task<EmployeeDetailsDto?> CreateAsync(CreateEmployeeDto request, CancellationToken ct) => repository.CreateAsync(request, ct);
    public Task<EmployeeDetailsDto?> UpdateAsync(int employeeId, UpdateEmployeeDto request, CancellationToken ct) => repository.UpdateAsync(employeeId, request, ct);
    public Task<EmployeeDetailsDto?> ActivateAsync(int employeeId, CancellationToken ct) => repository.ActivateAsync(employeeId, ct);
    public Task<EmployeeDetailsDto?> DeactivateAsync(int employeeId, CancellationToken ct) => repository.DeactivateAsync(employeeId, ct);
    public Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct) => repository.GetDepartmentsAsync(ct);
    public Task<IReadOnlyList<EmployeeAttendanceDto>> GetAttendanceAsync(int id, CancellationToken ct) => repository.GetAttendanceAsync(id, ct);
    public Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(int id, CancellationToken ct) => repository.GetLeaveRequestsAsync(id, ct);
    public Task<IReadOnlyList<EmployeeDrawDto>> GetDrawsAsync(string code, CancellationToken ct) => repository.GetDrawsAsync(code, ct);
    public Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(int id, CancellationToken ct) => repository.GetDocumentsAsync(id, ct);
    public Task<IReadOnlyList<EmployeeWorkflowDto>> GetWorkflowAsync(string code, CancellationToken ct) => repository.GetWorkflowAsync(code, ct);
    public Task<IReadOnlyList<EmployeePieceRateAssignmentDto>> GetPieceRateAssignmentsAsync(int employeeId, CancellationToken ct) => repository.GetPieceRateAssignmentsAsync(employeeId, ct);
    public Task<IReadOnlyList<EmployeeContractTemplateDto>> GetContractTemplatesAsync(CancellationToken ct) => repository.GetContractTemplatesAsync(ct);
    public Task<EmployeeContractTemplateDto?> GetContractTemplateByIdAsync(int templateId, CancellationToken ct) => repository.GetContractTemplateByIdAsync(templateId, ct);
    public Task<EmployeeContractTemplateDto> CreateContractTemplateAsync(EmployeeContractTemplateWriteDto request, CancellationToken ct) => repository.CreateContractTemplateAsync(request, ct);
    public Task<EmployeeContractTemplateDto?> UpdateContractTemplateAsync(int templateId, EmployeeContractTemplateUpdateDto request, CancellationToken ct) => repository.UpdateContractTemplateAsync(templateId, request, ct);
    public Task<EmployeeContractDto?> GetContractAsync(int employeeId, CancellationToken ct) => repository.GetContractAsync(employeeId, ct);
    public Task<EmployeeContractDto?> UpsertContractAsync(int employeeId, EmployeeContractDto contract, CancellationToken ct) => repository.UpsertContractAsync(employeeId, contract, ct);
    public Task<EmployeeContractDto?> GenerateContractAsync(int employeeId, CancellationToken ct) => repository.GenerateContractAsync(employeeId, ct);
}