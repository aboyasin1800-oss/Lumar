using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CustomerSummaryRepositoryTests
{
    [Fact]
    public async Task SearchAsync_ProjectsCurrentLoyaltyPointsAndLatestLedgerBalance()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new CustomerRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options));

        var customer = (await repository.SearchAsync("C10053", CancellationToken.None))
            .SingleOrDefault(item => string.Equals(item.CustomerCode, "C10053", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(customer);

        decimal? expectedPoints;
        decimal? expectedDebt;
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(@"
                SELECT la.CurrentPoints, ledger.BalanceAfterTransaction
                FROM dbo.Customers c
                LEFT JOIN dbo.LoyaltyAccounts la ON la.CustomerId = c.CustomerID
                OUTER APPLY (
                    SELECT TOP (1) cle.BalanceAfterTransaction
                    FROM dbo.CustomerLedgerEntries cle
                    WHERE cle.CustomerID = c.CustomerID
                    ORDER BY cle.CreatedAt DESC, cle.CustomerLedgerEntryId DESC
                ) ledger
                WHERE c.CustomerCode = @customerCode;", connection);
            command.Parameters.AddWithValue("@customerCode", "C10053");
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            expectedPoints = reader.IsDBNull(0) ? null : reader.GetDecimal(0);
            expectedDebt = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
        }

        Assert.Equal(expectedPoints, customer!.TotalPoints);
        Assert.Equal(expectedDebt, customer.TotalDebts);
    }
}