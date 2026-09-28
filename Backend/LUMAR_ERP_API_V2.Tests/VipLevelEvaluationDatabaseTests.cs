using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class VipLevelEvaluationDatabaseTests
{
    [Fact]
    public async Task EvaluateAllAsync_PersistsEvaluationForEveryLoyaltyAccount()
    {
        var connectionString = GetValidationConnectionString();
        var seed = await SeedAsync(connectionString, classified: false);
        var options = Options.Create(new DatabaseOptions
        {
            ConnectionString = connectionString,
        });
        var readConnections = new ReadOnlySqlConnectionFactory(options);
        var operationalConnections = new OperationalSqlConnectionFactory(options);
        var loyaltyRepository = new LoyaltyRepository(readConnections, operationalConnections);
        var evaluationRepository = new VipLevelEvaluationRepository(readConnections, operationalConnections);
        var service = new VipLevelEvaluationService(evaluationRepository, loyaltyRepository);

        try
        {
            var evaluations = await service.EvaluateAllAsync(CancellationToken.None);
            var accounts = await loyaltyRepository.GetAllAccountsAsync(CancellationToken.None);

            var evaluation = Assert.Single(evaluations);
            var account = Assert.Single(accounts);
            Assert.Equal(seed.CustomerId, evaluation.CustomerId);
            Assert.Equal(seed.CustomerId, account.CustomerId);
            Assert.Equal(seed.VipLevelId, evaluation.EvaluatedVipLevelId);
            Assert.False(string.IsNullOrWhiteSpace(evaluation.Reason));
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    [Fact]
    public async Task GetClassifiedCustomersAsync_ReturnsEngineRowsInLevelScoreAndNetworkOrder()
    {
        var connectionString = GetValidationConnectionString();
        var seed = await SeedAsync(connectionString, classified: true);
        var options = Options.Create(new DatabaseOptions
        {
            ConnectionString = connectionString,
        });
        var readConnections = new ReadOnlySqlConnectionFactory(options);
        var operationalConnections = new OperationalSqlConnectionFactory(options);
        var repository = new VipLevelEvaluationRepository(readConnections, operationalConnections);

        try
        {
            var customer = Assert.Single(await repository.GetClassifiedCustomersAsync(CancellationToken.None));

            Assert.Equal(seed.CustomerId, customer.CustomerId);
            Assert.Equal(seed.VipLevelId, customer.VipLevelId);
            Assert.True(customer.VipLevelId > 0);
            Assert.False(string.IsNullOrWhiteSpace(customer.VipLevelCode));
            Assert.False(string.IsNullOrWhiteSpace(customer.VipLevelDisplayName));
            Assert.Equal(75m, customer.Score);
            Assert.Equal(3, customer.NetworkSize);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    private static string GetValidationConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must target the validation database.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_CUSTOMERS_ONLY_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("VIP evaluation tests are restricted to the validation database.");
        return builder.ConnectionString;
    }

    private static async Task<VipSeed> SeedAsync(string connectionString, bool classified)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            var customerId = await InsertAsync(connection, transaction, @"
                INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, IsActive)
                OUTPUT INSERTED.CustomerID
                VALUES (@code, N'عميل اختبار VIP', @phone, 0, 0, 0, 1);",
                ("@code", $"VIP-{suffix}"), ("@phone", $"010{suffix[..9]}"));
            var vipLevelId = await InsertAsync(connection, transaction, @"
                INSERT INTO dbo.VipLevels (Code, DisplayName, MinimumPoints, Multiplier, Priority, IsActive, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.VipLevelId
                VALUES (@code, N'مستوى اختبار VIP', 0, 1, 100, 1, SYSUTCDATETIME(), SYSUTCDATETIME());",
                ("@code", $"VIP-{suffix}"));
            var criteriaId = await InsertAsync(connection, transaction, @"
                INSERT INTO dbo.VipLevelEvaluationCriteria
                    (VipLevelId, MinimumDirectReferrals, MinimumOwnOrders, MinimumNetworkOrders, MinimumNetworkSize, MinimumScore,
                     DirectReferralWeight, OwnOrderWeight, NetworkOrderWeight, NetworkSizeWeight, CreatedAtUtc)
                OUTPUT INSERTED.VipLevelCriteriaId
                VALUES (@vipLevelId, 0, 0, 0, 0, 0, 25, 25, 25, 25, SYSUTCDATETIME());",
                ("@vipLevelId", vipLevelId));
            var accountId = await InsertAsync(connection, transaction, @"
                INSERT INTO dbo.LoyaltyAccounts
                    (CustomerId, CurrentPoints, LifetimeEarnedPoints, LifetimeRedeemedPoints, PendingExpirePoints, VipLevelId,
                     CreatedAt, UpdatedAt, LoyaltyAccountStatus, VipDirectReferralCount, VipOwnOrderCount,
                     VipNetworkOrderCount, VipNetworkSize, VipNetworkMaxDepth, VipLevelScore, VipLevelReason, VipLevelEvaluatedAtUtc)
                OUTPUT INSERTED.LoyaltyAccountId
                VALUES (@customerId, 0, 0, 0, 0, @vipLevelId, SYSUTCDATETIME(), SYSUTCDATETIME(), N'Active',
                        0, 0, 0, @networkSize, 0, @score, @reason, @evaluatedAtUtc);",
                ("@customerId", customerId),
                ("@vipLevelId", vipLevelId),
                ("@networkSize", classified ? 3 : 0),
                ("@score", classified ? 75m : 0m),
                ("@reason", classified ? "Fixture classification" : (object)DBNull.Value),
                ("@evaluatedAtUtc", classified ? DateTime.UtcNow : (object)DBNull.Value));
            await transaction.CommitAsync();
            return new VipSeed(customerId, accountId, vipLevelId, criteriaId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, VipSeed seed)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DELETE FROM dbo.LoyaltyAccounts WHERE LoyaltyAccountId = @accountId;
            DELETE FROM dbo.VipLevelEvaluationCriteria WHERE VipLevelCriteriaId = @criteriaId;
            DELETE FROM dbo.VipLevels WHERE VipLevelId = @vipLevelId;
            DELETE FROM dbo.Customers WHERE CustomerID = @customerId;", connection);
        command.Parameters.AddWithValue("@accountId", seed.AccountId);
        command.Parameters.AddWithValue("@criteriaId", seed.CriteriaId);
        command.Parameters.AddWithValue("@vipLevelId", seed.VipLevelId);
        command.Parameters.AddWithValue("@customerId", seed.CustomerId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InsertAsync(SqlConnection connection, SqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record VipSeed(int CustomerId, int AccountId, int VipLevelId, int CriteriaId);
}