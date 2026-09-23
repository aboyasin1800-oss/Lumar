using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class PrintSettingsValidationTests
{
    [Fact]
    public void NormalizeAndValidate_Allows_Approved_Print_Settings()
    {
        var result = PrintSettingsValidator.NormalizeAndValidate(new Dictionary<string, string>
        {
            ["MeasurementCardHeaderUseImage"] = "true",
            ["MeasurementCardHeaderName"] = "LUMAR",
            ["MeasurementCardLocation"] = "صنعاء",
            ["MeasurementCardPhone1"] = "777777777",
            ["MeasurementCardPhone2"] = "666666666",
            ["MeasurementCardHeaderImage"] = "https://example.com/logo.png",
        });

        Assert.Equal(6, result.Count);
        Assert.Equal("true", result["MeasurementCardHeaderUseImage"]);
        Assert.Equal("LUMAR", result["MeasurementCardHeaderName"]);
    }

    [Fact]
    public void NormalizeAndValidate_Rejects_Unsupported_Key()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            PrintSettingsValidator.NormalizeAndValidate(new Dictionary<string, string>
            {
                ["UnknownSetting"] = "value",
            }));

        Assert.Contains("UnknownSetting", ex.Message);
    }

    [Fact]
    public void NormalizeAndValidate_Rejects_Invalid_Boolean_Value()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            PrintSettingsValidator.NormalizeAndValidate(new Dictionary<string, string>
            {
                ["MeasurementCardHeaderUseImage"] = "maybe",
            }));

        Assert.Contains("MeasurementCardHeaderUseImage", ex.Message);
    }
}
