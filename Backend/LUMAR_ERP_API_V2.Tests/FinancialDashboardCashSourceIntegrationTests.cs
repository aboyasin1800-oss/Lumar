using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FinancialDashboardCashSourceIntegrationTests
{
    [Fact]
    public async Task Dashboard_CashBalance_EqualsGoLiveCashMovementBalance_AndReconciliationSource()
    {
        var repository = new FinanceRepository(new ReadOnlySqlConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = GetConnectionString() })));

        var expectedBalance = await ReadGoLiveCashMovementBalanceAsync();
        var dashboard = await repository.GetDashboardAsync(CancellationToken.None);
        var reconciliation = await repository.GetCashReconciliationAsync(CancellationToken.None);

        Assert.Equal(expectedBalance, dashboard.CashBalance);
        Assert.Equal(expectedBalance, reconciliation.CashMovementsBalance);
        Assert.Equal("GoLiveCashMovementsOnly", reconciliation.Status);
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private static async Task<decimal> ReadGoLiveCashMovementBalanceAsync()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
                (SELECT CAST(value AS nvarchar(128))
                 FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));
            SELECT COALESCE(SUM(CASE CashDirection WHEN 1 THEN Amount WHEN 2 THEN -Amount ELSE 0 END), 0)
            FROM dbo.CashMovements
            WHERE CreatedAt >= @cutoverUtc;
            """, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }
}