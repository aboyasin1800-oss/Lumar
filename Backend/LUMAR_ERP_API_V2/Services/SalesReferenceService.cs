using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.DTOs.Consumption;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.SalesReference;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SalesReferenceService(
    IConsumptionRulesService consumptionRules,
    IPiecePointSettingsService piecePointSettings) : ISalesReferenceService
{
    private const int SchemaVersion = 1;

    public async Task<SalesReferenceVersionResponse> GetVersionAsync(
        CancellationToken cancellationToken)
    {
        var source = await ReadSourceAsync(cancellationToken);
        return new SalesReferenceVersionResponse(CreateVersion(source), SchemaVersion);
    }

    public async Task<SalesReferenceSnapshotResponse> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var source = await ReadSourceAsync(cancellationToken);
        return new SalesReferenceSnapshotResponse(
            CreateVersion(source),
            SchemaVersion,
            source.Dashboard.ProductTypes
                .OrderBy(item => item.ProductTypeId)
                .Select(item => new SalesReferenceProductTypeResponse(
                    item.ProductTypeId,
                    item.Code,
                    item.NameAr,
                    item.Category,
                    item.Scope,
                    item.IsActive))
                .ToList(),
            source.Dashboard.MeasurementFields
                .OrderBy(item => item.ProductTypeId)
                .ThenBy(item => item.MeasurementProfileId)
                .ThenBy(item => item.Sequence)
                .ThenBy(item => item.MeasurementFieldId)
                .Select(item => new SalesReferenceMeasurementFieldResponse(
                    item.ProductTypeId,
                    item.MeasurementProfileId,
                    item.MeasurementFieldId,
                    item.Code,
                    item.NameAr,
                    item.Unit,
                    item.IsRequired,
                    item.Sequence))
                .ToList(),
            source.PointSettings
                .OrderBy(item => item.ProductTypeId)
                .Select(item => new SalesReferenceLoyaltyPointSettingResponse(
                    item.ProductTypeId,
                    item.Points,
                    item.IsSettingActive,
                    item.IsConfigured))
                .ToList());
    }

    private async Task<SalesReferenceSource> ReadSourceAsync(CancellationToken cancellationToken)
    {
        var dashboardTask = consumptionRules.GetDashboardAsync(cancellationToken);
        var pointSettingsTask = piecePointSettings.GetProductLoyaltyPointSettingsAsync(cancellationToken);
        await Task.WhenAll(dashboardTask, pointSettingsTask);
        return new SalesReferenceSource(await dashboardTask, await pointSettingsTask);
    }

    private static string CreateVersion(SalesReferenceSource source)
    {
        var canonical = new StringBuilder();
        AppendProductTypes(canonical, source.Dashboard.ProductTypes);
        AppendProfiles(canonical, source.Dashboard.MeasurementProfiles);
        AppendFields(canonical, source.Dashboard.MeasurementFields);
        AppendRules(canonical, source.Dashboard.Rules);
        AppendPointSettings(canonical, source.PointSettings);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendProductTypes(
        StringBuilder canonical,
        IReadOnlyList<ConsumptionRuleProductTypeDto> productTypes)
    {
        foreach (var item in productTypes.OrderBy(item => item.ProductTypeId))
        {
            canonical.Append("PT|").Append(item.ProductTypeId).Append('|').Append(item.Code)
                .Append('|').Append(item.NameAr).Append('|').Append(item.Category).Append('|')
                .Append(item.Scope).Append('|').Append(item.IsActive).Append('\n');
        }
    }

    private static void AppendProfiles(
        StringBuilder canonical,
        IReadOnlyList<ConsumptionRuleMeasurementProfileDto> profiles)
    {
        foreach (var item in profiles.OrderBy(item => item.MeasurementProfileId))
        {
            canonical.Append("MP|").Append(item.MeasurementProfileId).Append('|')
                .Append(item.ProductTypeId).Append('|').Append(item.Name).Append('|')
                .Append(item.Version).Append('|').Append(item.Status).Append('|')
                .Append(item.IsActive).Append('|').Append(item.EffectiveFrom.Ticks).Append('|')
                .Append(item.EffectiveTo?.Ticks).Append('\n');
        }
    }

    private static void AppendFields(
        StringBuilder canonical,
        IReadOnlyList<ConsumptionRuleMeasurementFieldDto> fields)
    {
        foreach (var item in fields.OrderBy(item => item.MeasurementFieldId))
        {
            canonical.Append("MF|").Append(item.MeasurementFieldId).Append('|')
                .Append(item.MeasurementProfileId).Append('|').Append(item.ProductTypeId).Append('|')
                .Append(item.Code).Append('|').Append(item.NameAr).Append('|').Append(item.Unit)
                .Append('|').Append(item.IsRequired).Append('|').Append(item.Sequence).Append('\n');
        }
    }

    private static void AppendRules(StringBuilder canonical, IReadOnlyList<ConsumptionRuleDto> rules)
    {
        foreach (var item in rules.OrderBy(item => item.ConsumptionRuleId))
        {
            canonical.Append("CR|").Append(item.ConsumptionRuleId).Append('|')
                .Append(item.ProductTypeId).Append('|').Append(item.SizeClassId).Append('|')
                .Append(item.Formula).Append('|').Append(item.ResultUnit).Append('|')
                .Append(item.Priority).Append('|').Append(item.Version).Append('|')
                .Append(item.Status).Append('|').Append(item.IsActive).Append('|')
                .Append(item.EffectiveFrom.Ticks).Append('|').Append(item.EffectiveTo?.Ticks)
                .Append('|').Append(item.FabricWidth).Append('|').Append(item.FabricWidthUnit)
                .Append('|').Append(item.MeasurementCode).Append('|').Append(item.MinimumValue)
                .Append('|').Append(item.MaximumValue).Append('\n');
        }
    }

    private static void AppendPointSettings(
        StringBuilder canonical,
        IReadOnlyList<ProductLoyaltyPointSettingDto> pointSettings)
    {
        foreach (var item in pointSettings.OrderBy(item => item.ProductTypeId))
        {
            canonical.Append("LP|").Append(item.ProductTypeId).Append('|')
                .Append(item.LoyaltyPiecePointSettingId).Append('|').Append(item.Points)
                .Append('|').Append(item.IsSettingActive).Append('|').Append(item.IsConfigured)
                .Append('\n');
        }
    }

    private sealed record SalesReferenceSource(
        ConsumptionRulesDashboardDto Dashboard,
        IReadOnlyList<ProductLoyaltyPointSettingDto> PointSettings);
}