namespace LUMAR_ERP_API_V2.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Lumar";

    public string? ConnectionString { get; init; }
}