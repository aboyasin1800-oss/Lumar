using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class PayrollEngineTests
{
    [Fact]
    public void ShouldCalculateGrossAndNetAmounts()
    {
        var gross = PayrollEngine.CalculateGrossAmount(2500m, 450m, 100m, 180m);
        var net = PayrollEngine.CalculateNetAmount(gross, 320m);

        Assert.Equal(3230m, gross);
        Assert.Equal(2910m, net);
    }

    [Fact]
    public void ShouldNormalizePayrollStatusValues()
    {
        Assert.Equal("Draft", PayrollEngine.NormalizeStatus("draft"));
        Assert.Equal("Generated", PayrollEngine.NormalizeStatus("generated"));
        Assert.Equal("Approved", PayrollEngine.NormalizeStatus("approved"));
        Assert.Equal("Paid", PayrollEngine.NormalizeStatus("paid"));
    }
}
