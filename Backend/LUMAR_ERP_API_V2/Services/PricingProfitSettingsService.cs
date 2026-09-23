using System.Globalization;
using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PricingProfitSettingsService(IPricingProfitSettingsRepository repository) : IPricingProfitSettingsService
{
    private const string GlobalKey = "Pricing.GlobalProfitPercentage";
    private static readonly Regex ProductKey = new(
        "^Pricing\\.ProductType\\.(?<id>[1-9][0-9]*)\\.ProfitPercentage$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<PricingProfitSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken);
        var global = settings.TryGetValue(GlobalKey, out var globalValue) ? globalValue : 0m;
        var productValues = new Dictionary<int, decimal>();

        foreach (var setting in settings)
        {
            var match = ProductKey.Match(setting.Key);
            if (match.Success && int.TryParse(match.Groups["id"].Value, out var productTypeId))
            {
                productValues[productTypeId] = setting.Value;
            }
        }

        return new PricingProfitSettingsDto(global, productValues);
    }

    public Task<PricingProfitSettingsDto> SetGlobalAsync(decimal value, CancellationToken cancellationToken) =>
        SetAsync(value, () => repository.SetGlobalAsync(value, cancellationToken), cancellationToken);

    public Task<PricingProfitSettingsDto> SetProductTypeAsync(int productTypeId, decimal value, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0) throw new ArgumentOutOfRangeException(nameof(productTypeId));
        return SetAsync(value, () => repository.SetProductTypeAsync(productTypeId, value, cancellationToken), cancellationToken);
    }

    private async Task<PricingProfitSettingsDto> SetAsync(
        decimal value,
        Func<Task> write,
        CancellationToken cancellationToken)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "نسبة الربح لا يمكن أن تكون سالبة.");
        await write();
        return await GetAsync(cancellationToken);
    }

    public static string FormatValue(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string GlobalSettingKey => GlobalKey;

    public static string ProductSettingKey(int productTypeId) =>
        $"Pricing.ProductType.{productTypeId}.ProfitPercentage";
}
