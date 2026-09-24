using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PricingEngineService(
    IInventoryService inventoryService,
    IPieceCostManagementService pieceCostManagementService,
    IPricingProfitSettingsService pricingProfitSettingsService) : IPricingEngineService
{
    public async Task<PricingEngineResponseDto> CalculateAsync(
        PricingEngineRequestDto request,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        var fabricCode = request.FabricCode.Trim();
        var normalizedCode = fabricCode.ToUpperInvariant();
        var unit = request.ConsumptionUnit.Trim();
        var pieceProfitPercentage = 0m;
        var globalProfitPercentage = 0m;
        var fabrics = await inventoryService.GetFabricsAsync(cancellationToken);
        var matches = fabrics
            .Where(fabric => string.Equals(
                fabric.FabricCode?.Trim(), normalizedCode,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            reasons.Add("تعذر احتساب السعر: كود القماش غير موجود في مخزن الأقمشة.");
        }
        else if (matches.GroupBy(fabric => fabric.SourceTable, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            reasons.Add("تعذر احتساب السعر: يوجد أكثر من سجل للقماش بالكود نفسه. راجع بيانات مخزن الأقمشة.");
        }

        var fabric = matches
            .OrderBy(item => item.SourceTable.Equals("InventoryItems", StringComparison.OrdinalIgnoreCase) ? 0
                : item.SourceTable.Equals("Fabrics_Inventory", StringComparison.OrdinalIgnoreCase) ? 1
                : 2)
            .FirstOrDefault();
        if (fabric is not null && fabric.IsActive == false)
        {
            reasons.Add("تعذر احتساب السعر: سجل القماش غير نشط في مخزن الأقمشة.");
        }

        if (fabric is not null && (fabric.PricePerInch is null || fabric.PricePerInch <= 0))
        {
            reasons.Add("تعذر احتساب السعر: سعر البوصة للقماش المحدد غير مكتمل.");
        }

        if (fabric is not null && !HasAvailableQuantity(fabric))
        {
            reasons.Add("تعذر احتساب السعر: لا توجد كمية متاحة للقماش المحدد.");
        }

        if (!IsInch(unit))
        {
            reasons.Add("تعذر احتساب السعر: وحدة قاعدة الاستهلاك غير معتمدة. اضبط وحدة النتيجة على البوصة من إعدادات قواعد الاستهلاك.");
        }

        try
        {
            var profitSettings = await pricingProfitSettingsService.GetAsync(cancellationToken);
            globalProfitPercentage = profitSettings.GlobalProfitPercentage;
            profitSettings.ProductTypeProfitPercentages.TryGetValue(request.ProductTypeId, out pieceProfitPercentage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reasons.Add($"تعذر احتساب السعر: فشل قراءة إعدادات نسب الأرباح الرسمية. {exception.Message}");
        }

        if (pieceProfitPercentage < 0 || globalProfitPercentage < 0)
        {
            reasons.Add("تعذر احتساب السعر: نسبة الربح لا يمكن أن تكون سالبة.");
        }

        var costs = (await pieceCostManagementService.GetAllAsync(cancellationToken))
            .SingleOrDefault(item => item.ProductTypeId == request.ProductTypeId);
        if (costs is null)
        {
            reasons.Add("لا يمكن إصدار سعر نهائي: لم توجد تكاليف تشغيلية لنوع القطعة المحدد.");
        }
        else if (!HasCompleteOperatingCosts(costs))
        {
            reasons.Add("لا يمكن إصدار سعر نهائي: أكمل تكاليف القطعة من شاشة إدارة تكاليف القطع.");
        }

        if (reasons.Count > 0)
        {
            return NotReady(request, fabricCode, unit, reasons, fabric, costs, pieceProfitPercentage, globalProfitPercentage);
        }

        var calculation = PricingEngineCalculator.Calculate(
            request.Consumption,
            fabric!.PricePerInch!.Value,
            costs!.SewingCost,
            costs.ConsumablesCost,
            costs.IroningAndPackagingCost,
            costs.FixedOperatingCost,
            pieceProfitPercentage,
            globalProfitPercentage,
            request.Quantity);

        return new PricingEngineResponseDto(
            true,
            Array.Empty<string>(),
            request.ProductTypeId,
            fabricCode,
            unit,
            request.Consumption,
            request.Quantity,
            fabric.PricePerInch,
            calculation.FabricCostPerPiece,
            calculation.FabricCostTotal,
            costs.SewingCost,
            costs.ConsumablesCost,
            costs.IroningAndPackagingCost,
            costs.FixedOperatingCost,
            calculation.OperationalCostPerPiece,
            calculation.FullCostPerPiece,
            calculation.FullCostTotal,
            pieceProfitPercentage,
            calculation.PieceProfitValuePerPiece,
            calculation.PriceAfterPieceProfitPerPiece,
            globalProfitPercentage,
            calculation.GlobalProfitValuePerPiece,
            calculation.FinalPricePerPiece,
            calculation.FinalPriceTotal,
            fabric.SourceTable,
            "System_Settings: PieceTypeCost.*");
    }

    private static PricingEngineResponseDto NotReady(
        PricingEngineRequestDto request,
        string fabricCode,
        string unit,
        IReadOnlyList<string> reasons,
        FabricDto? fabric,
        PieceCostManagementDto? costs,
        decimal pieceProfitPercentage,
        decimal globalProfitPercentage) => new(
            false,
            reasons,
            request.ProductTypeId,
            fabricCode,
            unit,
            request.Consumption,
            request.Quantity,
            fabric?.PricePerInch,
            null,
            null,
            costs?.SewingCost,
            costs?.ConsumablesCost,
            costs?.IroningAndPackagingCost,
            costs?.FixedOperatingCost,
            null,
            null,
            null,
            pieceProfitPercentage,
            null,
            null,
            globalProfitPercentage,
            null,
            null,
            null,
            fabric?.SourceTable,
            "System_Settings: PieceTypeCost.*");

    private static bool IsInch(string unit) =>
        unit.Equals("Inch", StringComparison.OrdinalIgnoreCase) ||
        unit.Equals("Inches", StringComparison.OrdinalIgnoreCase) ||
        unit.Equals("بوصة", StringComparison.OrdinalIgnoreCase);

    private static bool HasAvailableQuantity(FabricDto fabric)
    {
        var available = fabric.QuantityInch ??
            (fabric.AvailableQuantity * 36m) ??
            (fabric.QuantityYard * 36m);
        return available > 0;
    }

    private static bool HasCompleteOperatingCosts(PieceCostManagementDto costs) =>
        costs.SewingCost > 0 &&
        costs.ConsumablesCost > 0 &&
        costs.IroningAndPackagingCost > 0 &&
        costs.FixedOperatingCost > 0;
}
