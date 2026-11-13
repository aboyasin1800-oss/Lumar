namespace LUMAR_ERP_API_V2.Configuration;

public sealed class TestDataResetOptions
{
    public const string SectionName = "TestDataReset";

    public bool LiveMode { get; set; } = true;
    public bool TestOverride { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 180;
}
