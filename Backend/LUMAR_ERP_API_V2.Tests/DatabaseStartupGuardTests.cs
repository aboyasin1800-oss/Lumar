using LUMAR_ERP_API_V2.Configuration;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class DatabaseStartupGuardTests
{
    [Fact]
    public void IsApprovedDatabaseName_AcceptsApprovedR3Database()
    {
        var connectionString = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION_R3;Integrated Security=True;TrustServerCertificate=True;";

        var result = DatabaseStartupGuard.IsApprovedDevelopmentDatabase(connectionString);

        Assert.True(result);
    }

    [Fact]
    public void IsApprovedDatabaseName_RejectsHistoricalDatabase()
    {
        var connectionString = "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_ES_VALIDATION;Integrated Security=True;TrustServerCertificate=True;";

        var result = DatabaseStartupGuard.IsApprovedDevelopmentDatabase(connectionString);

        Assert.False(result);
    }
}
