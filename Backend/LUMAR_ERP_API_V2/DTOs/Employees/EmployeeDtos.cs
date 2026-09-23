using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Employees;

public sealed record EmployeeListDto(int EmployeeId, string EmployeeCode, string EmployeeName, string? JobTitle, string? PhoneNumber, bool? IsActive, string Status, int DepartmentId);
public sealed record EmployeeDetailsDto(int EmployeeId, string EmployeeCode, string EmployeeName, string? JobTitle, string? ScannerCode, string? PhoneNumber, decimal? BaseSalary, string? Notes, bool? IsActive, string? SalaryType, decimal? FixedSalary, string FullName, string? NationalId, string? Phone, string? Email, string? Address, DateTime HireDate, DateTime? TerminationDate, string Status, int DepartmentId, decimal BasicSalary, decimal PieceWageRate, decimal OvertimeHourlyRate, DateTime CreatedAt, DateTime? UpdatedAt, string? ContractNumber, string? ContractType, string? ContractStatus, DateTime? ContractStartDate, DateTime? ContractEndDate, DateTime? ContractSignedDate, string? ContractNotes, string? ContractFilePath, int? ContractTemplateId, string? ContractText);
public sealed record EmployeeContractTemplateDto(int ContractTemplateId, string TemplateName, string ContractType, bool IsActive, string TemplateText, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record EmployeeContractDto(int EmployeeContractId, int EmployeeId, int? ContractTemplateId, string? ContractNumber, string? ContractType, string? ContractStatus, DateTime? ContractStartDate, DateTime? ContractEndDate, DateTime? ContractSignedDate, string? ContractNotes, string? ContractFilePath, string? ContractText, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record DepartmentDto(int DepartmentId, string DepartmentCode, string DepartmentName, string? Description, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record EmployeeAttendanceDto(int EmployeeAttendanceId, int EmployeeId, DateTime AttendanceDate, DateTime? CheckInTime, DateTime? CheckOutTime, decimal WorkedHours, decimal OvertimeHours, bool IsAbsent, string? AbsenceReason, string? Notes, DateTime CreatedAt);
public sealed record LeaveRequestDto(int LeaveRequestId, int EmployeeId, string LeaveType, DateTime StartDate, DateTime EndDate, decimal RequestedDays, string Status, string? Reason, string? ApprovedBy, DateTime? ApprovedAt, DateTime CreatedAt);
public sealed record EmployeeDrawDto(int DrawId, string? EmployeeCode, DateTime? DrawDate, decimal? Amount, string? Notes);
public sealed record EmployeeDrawSettlementDto(int SettlementId, int DrawId, string? EmployeeCode, DateTime SettlementDate, decimal Amount, string? Notes, int? JournalEntryId);
public sealed record EmployeeDocumentDto(int EmployeeDocumentId, int EmployeeId, string DocumentType, string? DocumentNumber, DateTime? IssueDate, DateTime? ExpiryDate, string? FilePath, string? Notes, DateTime CreatedAt);
public sealed record EmployeeWorkflowDto(string EmployeeCode, string? WorkStage);
public sealed record EmployeePieceRateAssignmentDto(int EmployeePieceRateAssignmentId, int EmployeeId, string PieceType, string Stage, decimal Rate, DateTime? EffectiveFrom, DateTime? EffectiveTo, bool IsActive, DateTime CreatedAt);

public sealed record EmployeeContractTemplateWriteDto(string TemplateName, string ContractType, bool IsActive, string TemplateText);

public sealed record EmployeeContractTemplateUpdateDto(int ContractTemplateId, string TemplateName, string ContractType, bool IsActive, string TemplateText);

public sealed record CreateEmployeeDto
{
    [Required, StringLength(50)] public string? EmployeeCode { get; init; }
    [Required, StringLength(200)] public string? FullName { get; init; }
    [Required] public int DepartmentId { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999999.99")] public decimal BasicSalary { get; init; }
    [Phone] public string? PhoneNumber { get; init; }
    public DateTime? HireDate { get; init; }
    [Required, StringLength(50)] public string? Status { get; init; } = "Active";
    public string? SalaryType { get; init; } = "BasicSalary";
    public string? ContractNumber { get; init; }
    public string? ContractType { get; init; }
    public string? ContractStatus { get; init; } = "Active";
    public DateTime? ContractStartDate { get; init; }
    public DateTime? ContractEndDate { get; init; }
    public DateTime? ContractSignedDate { get; init; }
    public string? ContractNotes { get; init; }
    public string? ContractFilePath { get; init; }
    public IReadOnlyList<EmployeePieceRateAssignmentInputDto>? PieceRates { get; init; }
}

public sealed record UpdateEmployeeDto
{
    [Required, StringLength(50)] public string? EmployeeCode { get; init; }
    [Required, StringLength(200)] public string? FullName { get; init; }
    [Required] public int DepartmentId { get; init; }
    [Range(typeof(decimal), "0.01", "9999999999999.99")] public decimal BasicSalary { get; init; }
    [Phone] public string? PhoneNumber { get; init; }
    public DateTime? HireDate { get; init; }
    [Required, StringLength(50)] public string? Status { get; init; }
    public bool IsActive { get; init; } = true;
    public string? SalaryType { get; init; } = "BasicSalary";
    public string? ContractNumber { get; init; }
    public string? ContractType { get; init; }
    public string? ContractStatus { get; init; } = "Active";
    public DateTime? ContractStartDate { get; init; }
    public DateTime? ContractEndDate { get; init; }
    public DateTime? ContractSignedDate { get; init; }
    public string? ContractNotes { get; init; }
    public string? ContractFilePath { get; init; }
    public IReadOnlyList<EmployeePieceRateAssignmentInputDto>? PieceRates { get; init; }
}

public sealed record EmployeePieceRateAssignmentInputDto(string PieceType, string Stage, decimal Rate, DateTime? EffectiveFrom, DateTime? EffectiveTo, bool IsActive = true);