using System.Reflection;
using System.Text;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
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
        var connectionString = GetValidationConnectionString();
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
        Assert.Equal("FCM", result!.Provider);
        Assert.Equal("Android", result.Platform);
        Assert.True(result.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(result.DeviceKey));
        Assert.NotEqual(0, result.DeviceId);

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
        Assert.Equal("FCM", refreshed.Provider);
        Assert.Equal("Android", refreshed.Platform);

        await repository.DisableAsync(result.DeviceId, "validation-test", CancellationToken.None);
        await CleanupDeviceAsync(connectionString, mobileAccountId, result.DeviceKey);
    }

    [Fact]
    public void PublicPushDeviceContract_DoesNotExposeSensitiveTokenData()
    {
        var summaryProperties = typeof(PushDeviceSummaryDto).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains(nameof(PushDeviceSummaryDto.DeviceId), summaryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(nameof(PushDeviceSummaryDto.DeviceKey), summaryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TokenCiphertext", summaryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TokenHash", summaryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("MobileAccountId", summaryProperties, StringComparer.OrdinalIgnoreCase);

        var deliveryProperties = typeof(PushDeliveryRequest).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains(nameof(PushDeliveryRequest.Title), deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(nameof(PushDeliveryRequest.Body), deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("CustomerId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplierId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("MobileAccountId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterMultipleDevicesForSameAccount_AllowsDistinctDeviceRoutes()
    {
        var connectionString = GetValidationConnectionString();
        var mobileAccountId = await ResolveTestMobileAccountIdAsync(connectionString, "Customer");
        var repository = CreateRepository(connectionString);

        var first = await repository.RegisterAsync(new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = "multi-device-a-" + Guid.NewGuid().ToString("N"),
            DeviceKey = "multi-device-a-" + Guid.NewGuid().ToString("N")
        }, mobileAccountId, CancellationToken.None);

        var second = await repository.RegisterAsync(new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = "multi-device-b-" + Guid.NewGuid().ToString("N"),
            DeviceKey = "multi-device-b-" + Guid.NewGuid().ToString("N")
        }, mobileAccountId, CancellationToken.None);

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

        var first = await repository.RegisterAsync(new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = token,
            DeviceKey = "duplicate-owner-a-" + Guid.NewGuid().ToString("N")
        }, customerAccounts[0], CancellationToken.None);

        Assert.NotNull(first);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RegisterAsync(new RegisterPushDeviceRequest
        {
            Provider = "FCM",
            Platform = "Android",
            PushToken = token,
            DeviceKey = "duplicate-owner-b-" + Guid.NewGuid().ToString("N")
        }, customerAccounts[1], CancellationToken.None));

        Assert.Contains("different MobileAccount", ex.Message, StringComparison.OrdinalIgnoreCase);
        await CleanupDeviceAsync(connectionString, customerAccounts[0], first!.DeviceKey);
    }

    [Fact]
    public async Task DispatchAsync_UsesAuthenticatedAccountOnly_AndRejectsClientTargetIds()
    {
        var fakeRepo = new FakePushRepository();
        var fakeUser = new TestAuthenticatedUserContext(new CurrentUserDto(
            UserId: 7,
            Username: "customer-user",
            FullName: "Customer User",
            Role: "Customer",
            IsActive: true,
            LastLoginUtc: DateTime.UtcNow,
            AccountType: "Customer",
            CustomerId: 77,
            EmployeeId: null,
            SupplierId: null));

        var fakeSender = new TestPushSender();
        var service = new PushDeviceService(fakeRepo, fakeUser, fakeSender, null!, NullLogger<PushDeviceService>.Instance);

        var result = await service.DispatchAsync(new PushDeliveryRequest("Hello", "Body", "CustomerNotification", "9999", "Push"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.SentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("device-1", result.DeviceKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("device-2", result.DeviceKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(77, fakeRepo.AccountIdSeenForDispatch);
        Assert.True(fakeSender.Calls.Count >= 2);

        var targetProperties = typeof(PushDeliveryRequest).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("CustomerId", targetProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplierId", targetProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("MobileAccountId", targetProperties, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(nameof(PushDeliveryRequest.ReferenceId), targetProperties, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatchAsync_HandlesProviderSuccessFailureTimeoutAndInvalidToken()
    {
        var service = new PushDeviceService(
            new FakePushRepository(new[]
            {
                new PushDeviceTokenRecord(1, 42, "FCM", "Android", "device-ok", "cipher-ok", "hash-ok", "token-ok", true, false, DateTime.UtcNow, null, null),
                new PushDeviceTokenRecord(2, 42, "FCM", "Android", "device-fail", "cipher-fail", "hash-fail", "token-fail", true, false, DateTime.UtcNow, null, null),
                new PushDeviceTokenRecord(3, 42, "FCM", "Android", "device-timeout", "cipher-timeout", "hash-timeout", "token-timeout", true, false, DateTime.UtcNow, null, null),
                new PushDeviceTokenRecord(4, 42, "FCM", "Android", "device-invalid", "cipher-invalid", "hash-invalid", "token-invalid", true, false, DateTime.UtcNow, null, null)
            }),
            new TestAuthenticatedUserContext(new CurrentUserDto(9, "supplier-user", "Supplier User", "Supplier", true, DateTime.UtcNow, "Supplier", null, null, 42)),
            new TestPushSender(new[]
            {
                new PushSendOutcome(true, "msg-ok", null, false, "Success"),
                new PushSendOutcome(false, null, "Permanent failure", false, "Failed"),
                new PushSendOutcome(false, null, "Request timed out", true, "Timeout"),
                new PushSendOutcome(false, null, "Invalid token", false, "InvalidToken")
            }),
            null!,
            NullLogger<PushDeviceService>.Instance);

        var result = await service.DispatchAsync(new PushDeliveryRequest("Title", "Body", "SupplierPayment", "10", "Push"), CancellationToken.None);

        Assert.Equal(1, result.SentCount);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal("Partial", result.Status);
        Assert.True(result.IsRetryable);
        Assert.Contains("device-ok", result.DeviceKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("device-fail", result.DeviceKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatchForOfficialNotification_UsesResolvedAccountWithoutClientTargetIds()
    {
        var fakeRepo = new FakePushRepository(new[]
        {
            new PushDeviceTokenRecord(7, 77, "FCM", "Android", "bridge-device", "cipher-bridge", "hash-bridge", "token-bridge", true, false, DateTime.UtcNow, null, null)
        });

        var service = new PushDeviceService(
            fakeRepo,
            new TestAuthenticatedUserContext(new CurrentUserDto(8, "customer-user", "Customer User", "Customer", true, DateTime.UtcNow, "Customer", 77, null, null)),
            new TestPushSender(new[] { new PushSendOutcome(true, "bridge-msg", null, false, "Success") }),
            null!,
            NullLogger<PushDeviceService>.Instance);

        var result = await service.DispatchForOfficialNotificationAsync(
            customerId: 9999,
            employeeId: null,
            supplierId: null,
            title: "Official customer title",
            body: "Official customer body",
            referenceType: "CustomerNotification",
            referenceId: "C-9001",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.SentCount);
        Assert.Contains("bridge-device", result.DeviceKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(77, fakeRepo.AccountIdSeenForDispatch);
    }

    [Fact]
    public async Task RuntimeRepository_RejectsMissingSchema_WithoutCreatingOne()
    {
        var masterConnectionString = GetMasterConnectionString();
        var dbName = "LUMAR_ERP_PUSH_SCHEMA_GUARD_TEST_" + Guid.NewGuid().ToString("N");

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            await using var createDb = new SqlCommand($"CREATE DATABASE [{dbName}]", master);
            await createDb.ExecuteNonQueryAsync();
        }

        try
        {
            var repo = CreateRepository(BuildConnectionStringForDatabase(masterConnectionString, dbName));
            var method = typeof(MobilePushDeviceRepository).GetMethod("EnsureRequiredSchemaAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            await using var connection = new SqlConnection(BuildConnectionStringForDatabase(masterConnectionString, dbName));
            await connection.OpenAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await (Task)method.Invoke(repo, new object[] { connection, CancellationToken.None })!);

            Assert.Contains("Apply the official migration", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var master = new SqlConnection(masterConnectionString);
            await master.OpenAsync();
            await using var drop = new SqlCommand($"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]", master);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CustomerAndSupplierRouting_ResolvesOfficialMobileAccountsOnly()
    {
        var customerService = new PushDeviceService(
            new FakePushRepository(new[] { new PushDeviceTokenRecord(11, 101, "FCM", "Android", "customer-device", "cipher-customer", "hash-customer", "token-customer", true, false, DateTime.UtcNow, null, null) }),
            new TestAuthenticatedUserContext(new CurrentUserDto(1, "customer", "Customer", "Customer", true, DateTime.UtcNow, "Customer", 101, null, null)),
            new TestPushSender(new[] { new PushSendOutcome(true, "customer-msg", null, false, "Success") }),
            null!,
            NullLogger<PushDeviceService>.Instance);

        var supplierService = new PushDeviceService(
            new FakePushRepository(new[] { new PushDeviceTokenRecord(12, 202, "FCM", "Android", "supplier-device", "cipher-supplier", "hash-supplier", "token-supplier", true, false, DateTime.UtcNow, null, null) }),
            new TestAuthenticatedUserContext(new CurrentUserDto(2, "supplier", "Supplier", "Supplier", true, DateTime.UtcNow, "Supplier", null, null, 202)),
            new TestPushSender(new[] { new PushSendOutcome(true, "supplier-msg", null, false, "Success") }),
            null!,
            NullLogger<PushDeviceService>.Instance);

        var customerResult = await customerService.DispatchAsync(new PushDeliveryRequest("Customer", "Body", "CustomerNotification", "1", "Push"), CancellationToken.None);
        var supplierResult = await supplierService.DispatchAsync(new PushDeliveryRequest("Supplier", "Body", "SupplierNotification", "2", "Push"), CancellationToken.None);

        Assert.True(customerResult.Success);
        Assert.True(supplierResult.Success);
        Assert.Contains("customer-device", customerResult.DeviceKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("supplier-device", supplierResult.DeviceKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicPushDeviceContract_DoesNotExposeSensitiveTokenValues_EvenInInternalTokenRecord()
    {
        var recordProperties = typeof(PushDeviceTokenRecord).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains(nameof(PushDeviceTokenRecord.TokenValue), recordProperties, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(nameof(PushDeviceTokenRecord.TokenCiphertext), recordProperties, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(nameof(PushDeviceTokenRecord.TokenHash), recordProperties, StringComparer.OrdinalIgnoreCase);

        var summaryProperties = typeof(PushDeviceSummaryDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("MobileAccountId", summaryProperties, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicPushDeliveryRequest_DoesNotAcceptCustomerOrSupplierTargetIds()
    {
        var deliveryProperties = typeof(PushDeliveryRequest).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("CustomerId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplierId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("MobileAccountId", deliveryProperties, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakePushRepository : IMobilePushDeviceRepository
    {
        public int AccountIdSeenForDispatch { get; private set; }
        private readonly List<PushDeviceTokenRecord> _devices;

        public FakePushRepository(IEnumerable<PushDeviceTokenRecord>? devices = null)
        {
            _devices = devices?.ToList() ?? new List<PushDeviceTokenRecord>
            {
                new(1, 77, "FCM", "Android", "device-1", "cipher-1", "hash-1", "token-1", true, false, DateTime.UtcNow, null, null),
                new(2, 77, "FCM", "Android", "device-2", "cipher-2", "hash-2", "token-2", true, false, DateTime.UtcNow, null, null)
            };
        }

        public Task<PushDeviceSummaryDto?> RegisterAsync(RegisterPushDeviceRequest request, int mobileAccountId, CancellationToken cancellationToken) => Task.FromResult<PushDeviceSummaryDto?>(null);
        public Task<PushDeviceSummaryDto?> RefreshAsync(int deviceId, RegisterPushDeviceRequest request, CancellationToken cancellationToken) => Task.FromResult<PushDeviceSummaryDto?>(null);
        public Task<PushDeviceSummaryDto?> DisableAsync(int deviceId, string reason, CancellationToken cancellationToken) => Task.FromResult<PushDeviceSummaryDto?>(null);
        public Task<IReadOnlyList<PushDeviceSummaryDto>> GetActiveAsync(int mobileAccountId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PushDeviceSummaryDto>>(Array.Empty<PushDeviceSummaryDto>());

        public Task<IReadOnlyList<PushDeviceTokenRecord>> GetActiveTokensAsync(int mobileAccountId, CancellationToken cancellationToken)
        {
            AccountIdSeenForDispatch = mobileAccountId;
            return Task.FromResult<IReadOnlyList<PushDeviceTokenRecord>>(_devices.Where(d => d.MobileAccountId == mobileAccountId).ToList());
        }

        public Task<PushDeviceSummaryDto?> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken) => Task.FromResult<PushDeviceSummaryDto?>(null);
        public Task<bool> IsTokenRegisteredToAnotherAccountAsync(string tokenHash, int mobileAccountId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class TestPushSender : IPushSender
    {
        private readonly Queue<PushSendOutcome> _outcomes;
        public List<string> Calls { get; } = new();

        public TestPushSender(IEnumerable<PushSendOutcome>? outcomes = null)
        {
            _outcomes = new Queue<PushSendOutcome>(outcomes ?? new[]
            {
                new PushSendOutcome(true, "m1", null, false, "Success"),
                new PushSendOutcome(true, "m2", null, false, "Success")
            });
        }

        public string Provider => "TestProvider";

        public Task<PushSendOutcome> SendAsync(PushDeviceTokenRecord device, string title, string body, string? referenceType, string? referenceId, CancellationToken cancellationToken)
        {
            Calls.Add(device.DeviceKey);
            return Task.FromResult(_outcomes.Count > 0 ? _outcomes.Dequeue() : new PushSendOutcome(false, null, "No more queued outcomes", false, "Failed"));
        }
    }

    private sealed class TestAuthenticatedUserContext(CurrentUserDto currentUser) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult<CurrentUserDto?>(currentUser);
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

    private static string GetMasterConnectionString()
    {
        var baseConnectionString = GetValidationConnectionString();
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    private static string BuildConnectionStringForDatabase(string masterConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
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
        await using var command = new SqlCommand("SELECT MobileAccountId FROM dbo.MobileAccounts WHERE AccountType = @accountType AND IsActive = 1 ORDER BY MobileAccountId;", connection);
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
