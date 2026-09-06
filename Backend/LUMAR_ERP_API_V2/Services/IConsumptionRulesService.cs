using LUMAR_ERP_API_V2.DTOs.Consumption;

namespace LUMAR_ERP_API_V2.Services;

public interface IConsumptionRulesService
{
    Task<ConsumptionRulesDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<ConsumptionRulesIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConsumptionRuleDto>> SaveProductRulesBatchAsync(SaveProductRulesBatchDto request, CancellationToken cancellationToken);
    Task<ConsumptionRuleDto?> CreateAsync(CreateConsumptionRuleDto request, CancellationToken cancellationToken);
    Task<ConsumptionRuleDto?> UpdateAsync(int consumptionRuleId, UpdateConsumptionRuleDto request, CancellationToken cancellationToken);
    Task<MeasurementTypeWriteResultDto?> CreateMeasurementTypeAsync(CreateMeasurementTypeDto request, CancellationToken cancellationToken);
    Task<MeasurementTypeWriteResultDto?> UpdateMeasurementTypeAsync(int productTypeId, UpdateMeasurementTypeDto request, CancellationToken cancellationToken);
    Task<bool> DeleteMeasurementTypeAsync(int productTypeId, CancellationToken cancellationToken);
}
