using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LUMAR_ERP_API_V2.Configuration;

public static class DatabaseStartupGuard
{
    public const string OfficialDevelopmentDatabaseName = "LUMAR_ERP_ES_VALIDATION_R3";
    public const string IsolatedIntegrationTestDatabaseName = "LUMAR_ERP_TEST_R3_ROLLS";
    public const string ApprovedDatabaseName = OfficialDevelopmentDatabaseName;

    private static readonly string[] ForbiddenDevelopmentDatabaseNames =
    [
        "LUMAR_ERP_TEST_R3_ROLLS",
        "LUMAR_ERP_ES_VALIDATION",
        "LUMAR_ERP_ES_VALIDATION_R2",
        "LUMAR_ERP_VIP_2",
        "LUMAR_ERP",
        "LUMAR_ERP_TEST"
    ];

    public static void EnsureApprovedDevelopmentDatabase(IConfiguration configuration)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var connectionString = configuration["Lumar:ConnectionString"]
            ?? Environment.GetEnvironmentVariable("Lumar__ConnectionString");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Development startup requires a valid Lumar__ConnectionString. The process-scoped database connection is missing.");
        }

        if (!IsApprovedDevelopmentDatabase(connectionString))
        {
            throw new InvalidOperationException(
                $"Development startup is blocked. The active database must be '{ApprovedDatabaseName}' but the current connection target is '{GetDatabaseName(connectionString) ?? "<unknown>"}'.");
        }
    }

    public static bool IsApprovedDevelopmentDatabase(string? connectionString)
    {
        return IsApprovedDatabase(connectionString, OfficialDevelopmentDatabaseName);
    }

    public static bool IsApprovedIntegrationTestDatabase(string? connectionString)
    {
        return IsApprovedDatabase(connectionString, IsolatedIntegrationTestDatabaseName);
    }

    private static bool IsApprovedDatabase(string? connectionString, string approvedDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var databaseName = GetDatabaseName(connectionString);
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return false;
            }

            if (ForbiddenDevelopmentDatabaseNames.Contains(databaseName, StringComparer.OrdinalIgnoreCase)
                && !string.Equals(approvedDatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(approvedDatabaseName, databaseName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string? GetDatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog;
    }
}
