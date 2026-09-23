namespace LUMAR_ERP_API_V2.Repositories;

public interface IPricingProfitSettingsRepository
{
    Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken cancellationToken);
    Task SetGlobalAsync(decimal value, CancellationToken cancellationToken);
    Task SetProductTypeAsync(int productTypeId, decimal value, CancellationToken cancellationToken);
}
