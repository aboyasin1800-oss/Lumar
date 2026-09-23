using LUMAR_ERP_API_V2.DTOs.Payroll;
namespace LUMAR_ERP_API_V2.Repositories;
public interface IPayrollRepository
{
    Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(CancellationToken ct);
    Task<PayrollPeriodDto?> GetPeriodAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PayrollRecordDto>> GetRecordsAsync(CancellationToken ct);
    Task<PayrollRecordDto?> GetRecordAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PayrollItemDto>> GetItemsAsync(int recordId, CancellationToken ct);
    Task<IReadOnlyList<EmployeePayrollSummaryDto>> GetEmployeeSummariesAsync(int periodId, CancellationToken ct);
    Task<IReadOnlyList<PieceWageRecordDto>> GetPieceWagesAsync(CancellationToken ct);
    Task<IReadOnlyList<PieceWageRateDto>> GetPieceWageRatesAsync(CancellationToken ct);
    Task<PieceWageRateDto> CreatePieceWageRateAsync(CreatePieceWageRateDto request, CancellationToken ct);
    Task<PieceWageRateDto?> UpdatePieceWageRateAsync(int id, UpdatePieceWageRateDto request, CancellationToken ct);
    Task<bool> DeletePieceWageRateAsync(int id, CancellationToken ct);
    Task<GeneratePayrollResultDto> GenerateAsync(GeneratePayrollRequestDto request, CancellationToken ct);
    Task<PayrollPeriodDto?> ApproveAsync(int periodId, ApprovePayrollRequestDto request, CancellationToken ct);
    Task<PayrollRecordDto?> PayAsync(int recordId, PayrollPaymentRequestDto request, CancellationToken ct);
    Task<IReadOnlyList<PayrollSettlementDto>> GetSettlementsAsync(int employeeId, CancellationToken ct);
}