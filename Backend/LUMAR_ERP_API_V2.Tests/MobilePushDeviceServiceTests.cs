using System.Text;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Push;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MobilePushDeviceServiceTests
{
    [Fact]
    public async Task RegisterAndRefreshPushDevice_StoresEncryptedTokenAndHash()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Push device validation must target LUMAR_ERP_ES_VALIDATION.");

        var mobileAccountId = await ResolveTestMobileAccountIdAsync(connectionString, "Customer");
        var token = "fcmtoken-" + Guid.NewGuid().ToString("N");

        var repository = CreateRepository(connectionString);

        var result = await repository.RegisterAsync(new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = token,
            DeviceKey = "device-key-" + Guid.NewGuid().ToString("N")
        }, mobileAccountId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(mobileAccountId, result!.MobileAccountId);
        Assert.Equal("FCM", result.Provider);
        Assert.Equal("Android", result.Platform);
        Assert.True(result.IsActive);
        Assert.NotEqual(token, result.TokenCiphertext);
        Assert.NotNull(result.TokenHash);
        Assert.False(string.IsNullOrWhiteSpace(result.TokenHash));

        var refreshed = await repository.RefreshAsync(result.DeviceId, new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = "fcmtoken-refresh-" + Guid.NewGuid().ToString("N"),
            DeviceKey = result.DeviceKey
        }, CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.Equal(result.DeviceId, refreshed!.DeviceId);
        Assert.Equal(result.DeviceKey, refreshed.DeviceKey);

        await repository.DisableAsync(result.DeviceId, "validation-test", CancellationToken.None);
        await CleanupDeviceAsync(connectionString, mobileAccountId, result.DeviceKey);
    }

    [Fact]
    public async Task RegisterMultipleDevicesForSameAccount_AllowsDistinctDeviceRoutes()
    {
        var connectionString = GetValidationConnectionString();
        var mobileAccountId = await ResolveTestMobileAccountIdAsync(connectionString, "Customer");
        var repository = CreateRepository(connectionString);

        var first = await repository.RegisterAsync(new RegisterPushDeviceRequest { Provider = "FCM", Platform = "Android", PushToken = "multi-device-a-" + Guid.NewGuid().ToString("N"), DeviceKey = "multi-device-a-" + Guid.NewGuid().ToString("N") }, mobileAccountId, CancellationToken.None);
        var second = await repository.RegisterAsync(new RegisterPushDeviceRequest { Provider = "FCM", Platform = "Android", PushToken = "multi-device-b-" + Guid.NewGuid().ToString("N"), DeviceKey = "multi-device-b-" + Guid.NewGuid().ToString("N") }, mobileAccountId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.DeviceId, second!.DeviceId);

        var active = await repository.GetActiveAsync(mobileAccountId, CancellationToken.None);
        Assert.Contains(active, x => x.DeviceKey == first.DeviceKey);
        Assert.Contains(active, x => x.DeviceKey == second.DeviceKey);

        await CleanupDeviceAsync(connectionString, mobileAccountId, first.DeviceKey);
        await CleanupDeviceAsync(connectionString, mobileAccountId, second.DeviceKey);
    }

    [Fact]
    public async Task RegisterSameTokenForDifferentAccount_RejectsDuplicateOwnership()
    {
        var connectionString = GetValidationConnectionString();
        var customerAccounts = await ResolveTestMobileAccountIdsAsync(connectionString, "Customer", 2);
        Assert.True(customerAccounts.Count >= 2, "A duplicate-token rejection test requires at least two active customer accounts.");

        var repository = CreateRepository(connectionString);
        var token = "duplicate-token-" + Guid.NewGuid().ToString("N");

        var first = await repository.RegisterAsync(new RegisterPushDeviceRequest { Provider = "FCM", Platform = "Android", PushToken = token, DeviceKey = "duplicate-owner-a-" + Guid.NewGuid().ToString("N") }, customerAccounts[0], CancellationToken.None);
        Assert.NotNull(first);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RegisterAsync(new RegisterPushDeviceRequest { Provider = "FCM", Platform = "Android", PushToken = token, DeviceKey = "duplicate-owner-b-" + Guid.NewGuid().ToString("N") }, customerAccounts[1], CancellationToken.None));
        Assert.Contains("different MobileAccount", ex.Message, StringComparison.OrdinalIgnoreCase);

        await CleanupDeviceAsync(connectionString, customerAccounts[0], first!.DeviceKey);
    }

    private static MobilePushDeviceRepository CreateRepository(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();
        var dp = provider.GetRequiredService<IDataProtectionProvider>();

        return new MobilePushDeviceRepository(
            new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString })),
            dp,
            new NullLogger<MobilePushDeviceRepository>());
    }

    private static string GetValidationConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must be set for validation testing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Push device validation must target LUMAR_ERP_ES_VALIDATION.");

        return connectionString;
    }

    private static async Task CleanupDeviceAsync(string connectionString, int mobileAccountId, string deviceKey)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM dbo.MobilePushDevices WHERE MobileAccountId = @mobileAccountId AND DeviceKey = @deviceKey;", connection);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        command.Parameters.AddWithValue("@deviceKey", deviceKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ResolveTestMobileAccountIdAsync(string connectionString, string accountType)
    {
        var match = await ResolveTestMobileAccountIdsAsync(connectionString, accountType, 1);
        return match[0];
    }

    private static async Task<List<int>> ResolveTestMobileAccountIdsAsync(string connectionString, string accountType, int count)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand($"SELECT MobileAccountId FROM dbo.MobileAccounts WHERE AccountType = @accountType AND IsActive = 1 ORDER BY MobileAccountId;", connection);
        command.Parameters.AddWithValue("@accountType", accountType);
        await using var reader = await command.ExecuteReaderAsync();

        var result = new List<int>();
        while (await reader.ReadAsync() && result.Count < count)
        {
            result.Add(reader.GetInt32(0));
        }

        if (result.Count < count)
            throw new InvalidOperationException($"Not enough active {accountType} MobileAccount rows found for validation.");

        return result;
    }
}
