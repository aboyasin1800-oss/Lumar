using LUMAR_ERP_API_V2.DTOs.Payroll; using LUMAR_ERP_API_V2.Repositories; namespace LUMAR_ERP_API_V2.Services;
public sealed class PayrollService(IPayrollRepository repository) : IPayrollService
{
    public Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(CancellationToken ct) => repository.GetPeriodsAsync(ct);
    public Task<PayrollPeriodDto?> GetPeriodAsync(int id, CancellationToken ct) => repository.GetPeriodAsync(id, ct);
    public Task<IReadOnlyList<PayrollRecordDto>> GetRecordsAsync(CancellationToken ct) => repository.GetRecordsAsync(ct);
    public Task<PayrollRecordDto?> GetRecordAsync(int id, CancellationToken ct) => repository.GetRecordAsync(id, ct);
    public Task<IReadOnlyList<PayrollItemDto>> GetItemsAsync(int id, CancellationToken ct) => repository.GetItemsAsync(id, ct);
    public Task<IReadOnlyList<EmployeePayrollSummaryDto>> GetEmployeeSummariesAsync(int id, CancellationToken ct) => repository.GetEmployeeSummariesAsync(id, ct);
    public Task<IReadOnlyList<PieceWageRecordDto>> GetPieceWagesAsync(CancellationToken ct) => repository.GetPieceWagesAsync(ct);
    public Task<IReadOnlyList<PieceWageRateDto>> GetPieceWageRatesAsync(CancellationToken ct) => repository.GetPieceWageRatesAsync(ct);
    public Task<PieceWageRateDto> CreatePieceWageRateAsync(CreatePieceWageRateDto request, CancellationToken ct) => repository.CreatePieceWageRateAsync(request, ct);
    public Task<PieceWageRateDto?> UpdatePieceWageRateAsync(int id, UpdatePieceWageRateDto request, CancellationToken ct) => repository.UpdatePieceWageRateAsync(id, request, ct);
    public Task<bool> DeletePieceWageRateAsync(int id, CancellationToken ct) => repository.DeletePieceWageRateAsync(id, ct);
    public Task<GeneratePayrollResultDto> GenerateAsync(GeneratePayrollRequestDto request, CancellationToken ct) => repository.GenerateAsync(request, ct);
    public Task<PayrollPeriodDto?> ApproveAsync(int periodId, ApprovePayrollRequestDto request, CancellationToken ct) => repository.ApproveAsync(periodId, request, ct);
    public Task<PayrollRecordDto?> PayAsync(int recordId, PayrollPaymentRequestDto request, CancellationToken ct) => repository.PayAsync(recordId, request, ct);
    public Task<IReadOnlyList<PayrollSettlementDto>> GetSettlementsAsync(int employeeId, CancellationToken ct) => repository.GetSettlementsAsync(employeeId, ct);
}