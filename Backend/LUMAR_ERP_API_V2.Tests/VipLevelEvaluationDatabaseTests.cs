using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class VipLevelEvaluationDatabaseTests
{
    [Fact]
    public async Task EvaluateAllAsync_PersistsEvaluationForEveryLoyaltyAccount()
    {
        var options = Options.Create(new DatabaseOptions
        {
            ConnectionString = GetConnectionString(),
        });
        var readConnections = new ReadOnlySqlConnectionFactory(options);
        var operationalConnections = new OperationalSqlConnectionFactory(options);
        var loyaltyRepository = new LoyaltyRepository(readConnections, operationalConnections);
        var evaluationRepository = new VipLevelEvaluationRepository(readConnections, operationalConnections);
        var service = new VipLevelEvaluationService(evaluationRepository, loyaltyRepository);

        var evaluations = await service.EvaluateAllAsync(CancellationToken.None);
        var accounts = await loyaltyRepository.GetAllAccountsAsync(CancellationToken.None);

        Assert.NotEmpty(evaluations);
        Assert.Equal(accounts.Count, evaluations.Count);
        Assert.All(evaluations, evaluation =>
        {
            Assert.True(evaluation.EvaluatedVipLevelId > 0);
            Assert.False(string.IsNullOrWhiteSpace(evaluation.Reason));
        });
    }

    [Fact]
    public async Task GetClassifiedCustomersAsync_ReturnsEngineRowsInLevelScoreAndNetworkOrder()
    {
        var options = Options.Create(new DatabaseOptions
        {
            ConnectionString = GetConnectionString(),
        });
        var readConnections = new ReadOnlySqlConnectionFactory(options);
        var operationalConnections = new OperationalSqlConnectionFactory(options);
        var repository = new VipLevelEvaluationRepository(readConnections, operationalConnections);

        var customers = await repository.GetClassifiedCustomersAsync(CancellationToken.None);

        Assert.NotEmpty(customers);
        Assert.All(customers, customer =>
        {
            Assert.True(customer.VipLevelId > 0);
            Assert.False(string.IsNullOrWhiteSpace(customer.VipLevelCode));
            Assert.False(string.IsNullOrWhiteSpace(customer.VipLevelDisplayName));
        });

        for (var index = 1; index < customers.Count; index++)
        {
            var previous = customers[index - 1];
            var current = customers[index];
            Assert.True(
                previous.VipLevelPriority > current.VipLevelPriority ||
                previous.VipLevelPriority == current.VipLevelPriority && previous.Score > current.Score ||
                previous.VipLevelPriority == current.VipLevelPriority && previous.Score == current.Score && previous.NetworkSize >= current.NetworkSize);
        }
    }

    private static string GetConnectionString() =>
        Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
}