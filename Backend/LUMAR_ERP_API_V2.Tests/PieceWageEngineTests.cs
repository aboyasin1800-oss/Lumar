using LUMAR_ERP_API_V2.DTOs.Payroll;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class PieceWageEngineTests
{
    private static readonly IReadOnlyList<PieceWageRateDto> Rates =
    [
        new(1, "PANTS", "Cutting", 12.50m, true, null, DateTime.UtcNow, null),
        new(2, "SHIRT", "Sewing", 15.00m, true, null, DateTime.UtcNow, null),
        new(3, "*", "Sewing", 11.00m, true, null, DateTime.UtcNow, null),
        new(4, "PANTS", "Printing", 0m, true, null, DateTime.UtcNow, null)
    ];

    [Fact]
    public void ShouldCreatePieceWageAfterValidTrackingEvent()
    {
        var resolution = PieceWageEngine.ResolveRate("PANTS", "Cutting", Rates);

        Assert.True(resolution.IsValid);
        Assert.Equal(12.50m, resolution.WageRate);

        var validation = PieceWageEngine.ValidateRecord("PANTS", "Cutting", resolution.WageRate!.Value, "EMP-100", 1m, 0);
        Assert.True(validation.IsValid);
        Assert.Equal(12.50m, validation.TotalWage);
    }

    [Fact]
    public void ShouldPreventDuplicateTrackingEventRecord()
    {
        var validation = PieceWageEngine.ValidateRecord("PANTS", "Cutting", 12.50m, "EMP-100", 1m, 1);

        Assert.False(validation.IsValid);
        Assert.Contains("already exists", validation.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldResolveExactPieceTypeRateBeforeWildcard()
    {
        var exact = PieceWageEngine.ResolveRate("PANTS", "Cutting", Rates);
        var wildcard = PieceWageEngine.ResolveRate("SHIRT", "Sewing", Rates);

        Assert.True(exact.IsValid);
        Assert.True(wildcard.IsValid);
        Assert.Equal(12.50m, exact.WageRate);
        Assert.Equal(15.00m, wildcard.WageRate);
    }

    [Fact]
    public void ShouldUseWildcardRateWhenExactRateIsMissing()
    {
        var result = PieceWageEngine.ResolveRate("POLO", "Sewing", Rates);

        Assert.True(result.IsValid);
        Assert.Equal(11.00m, result.WageRate);
    }

    [Fact]
    public void ShouldRejectOnlyMissingOrNegativeRate()
    {
        var missing = PieceWageEngine.ResolveRate("PANTS", "Quality", Rates);
        var zero = PieceWageEngine.ResolveRate("PANTS", "Printing", Rates);
        var negative = PieceWageEngine.ResolveRate("PANTS", "Cutting", new[]
        {
            new LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto(1, "PANTS", "Cutting", -1m, true, null, DateTime.UtcNow, null)
        });

        Assert.False(missing.IsValid);
        Assert.True(zero.IsValid);
        Assert.Equal(0m, zero.WageRate);
        Assert.False(negative.IsValid);
        Assert.Contains("valid wage rate", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", negative.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldRejectBlockedWageEmployees()
    {
        var validation = PieceWageEngine.ValidateRecord("PANTS", "Cutting", 12.50m, "OP-01", 1m, 0);

        Assert.False(validation.IsValid);
        Assert.Contains("OP-01", validation.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldValidateTotalWageFormula()
    {
        var total = PieceWageEngine.CalculateTotalWage(4m, 13.25m);

        Assert.Equal(53.00m, total);
    }
}
