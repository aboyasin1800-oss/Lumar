using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ConsumptionRulesService(IConsumptionRulesRepository repository) : IConsumptionRulesService
{
    public Task<ConsumptionRulesDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => repository.GetDashboardAsync(cancellationToken);

    public Task<ConsumptionRulesIntegrityReportDto> GetIntegrityReportAsync(CancellationToken cancellationToken) => repository.GetIntegrityReportAsync(cancellationToken);

    public Task<IReadOnlyList<ConsumptionRuleDto>> SaveProductRulesBatchAsync(SaveProductRulesBatchDto request, CancellationToken cancellationToken) => repository.SaveProductRulesBatchAsync(request, cancellationToken);

    public Task<ConsumptionRuleDto?> CreateAsync(CreateConsumptionRuleDto request, CancellationToken cancellationToken) => repository.CreateAsync(request, cancellationToken);

    public Task<ConsumptionRuleDto?> UpdateAsync(int consumptionRuleId, UpdateConsumptionRuleDto request, CancellationToken cancellationToken) => repository.UpdateAsync(consumptionRuleId, request, cancellationToken);

    public Task<MeasurementTypeWriteResultDto?> CreateMeasurementTypeAsync(CreateMeasurementTypeDto request, CancellationToken cancellationToken) => repository.CreateMeasurementTypeAsync(request, cancellationToken);

    public Task<MeasurementTypeWriteResultDto?> UpdateMeasurementTypeAsync(int productTypeId, UpdateMeasurementTypeDto request, CancellationToken cancellationToken) => repository.UpdateMeasurementTypeAsync(productTypeId, request, cancellationToken);

    public Task<bool> DeleteMeasurementTypeAsync(int productTypeId, CancellationToken cancellationToken) => repository.DeleteMeasurementTypeAsync(productTypeId, cancellationToken);
}
