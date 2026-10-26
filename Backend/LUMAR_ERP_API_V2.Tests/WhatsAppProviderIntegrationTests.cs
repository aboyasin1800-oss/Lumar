using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class WhatsAppProviderIntegrationTests
{
    [Fact]
    public void IntegrationRegistersDistinctChannelsAndCentralStore()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOutboundMessageChannel, InAppMessageChannel>();
        services.AddLocalWhatsAppFoundation(new ConfigurationBuilder().Build());
        services.AddWhatsAppProviderIntegration();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var channels = scope.ServiceProvider.GetServices<IOutboundMessageChannel>().ToArray();
        Assert.Equal(new[] { "InApp", "LocalWhatsApp" }, channels.Select(channel => channel.ChannelName).ToArray());
        Assert.Single(services, service => service.ServiceType == typeof(IWhatsAppProviderStore));
    }

    [Fact]
    public async Task ProviderAdminApiRequiresActiveAdminAndRejectsUnsafeTransitions()
    {
        var store = new MemoryProviderStore(ActiveWhatsAppProvider.Meta);
        using var handler = new ReadyHandler();
        using var transport = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5094/") };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(WhatsAppProviderController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuthenticatedUserContext, ProviderTestUser>();
        builder.Services.AddSingleton<IWhatsAppProviderStore>(store);
        builder.Services.AddSingleton(new LocalWhatsAppGatewayClient(transport));
        builder.Services.AddScoped<WhatsAppProviderService>();
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var role in new[] { "", "Customer", "InactiveAdmin", "Admin", "System Administrator" })
            {
                http.DefaultRequestHeaders.Remove("Authorization");
                if (role.Length > 0) http.DefaultRequestHeaders.Add("Authorization", "Bearer " + role);
                var expected = role.Length == 0 ? HttpStatusCode.Unauthorized
                    : role is "Admin" or "System Administrator" ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                using var get = await http.GetAsync("/whatsapp-provider");
                Assert.Equal(expected, get.StatusCode);
                Assert.True(get.Headers.CacheControl!.NoStore);
                using var set = await http.PutAsJsonAsync("/whatsapp-provider", new { provider = "Meta" });
                Assert.Equal(expected, set.StatusCode);
            }
            foreach (var payload in new[] { "{}", "{\"provider\":1}", "{\"provider\":\"Both\"}",
                "{\"provider\":\"None, Meta\"}", "{\"provider\":\"Local, Local\"}",
                "{\"provider\":\"Meta, Local\"}", "{\"provider\":\"Local\",\"metaActive\":true}" })
            {
                using var invalid = await http.PutAsync("/whatsapp-provider", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            handler.State = "Disconnected";
            Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync("/whatsapp-provider", new { provider = "Local" })).StatusCode);
            Assert.Equal(ActiveWhatsAppProvider.Meta, store.Value);
            handler.State = "Connected";
            Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync("/whatsapp-provider", new { provider = "Local" })).StatusCode);
            handler.State = "LoggedOut";
            using var status = JsonDocument.Parse(await http.GetStringAsync("/whatsapp-provider"));
            Assert.Equal("Local", status.RootElement.GetProperty("activeProvider").GetString());
            Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync("/whatsapp-provider", new { provider = "None" })).StatusCode);
            Assert.Equal(ActiveWhatsAppProvider.None, store.Value);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class ProviderTestUser(IHttpContextAccessor accessor) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var header = accessor.HttpContext!.Request.Headers.Authorization.ToString();
            if (header.Length == 0) return Task.FromResult<CurrentUserDto?>(null);
            var role = header[7..];
            return Task.FromResult<CurrentUserDto?>(new(1, "test", "Test", role == "InactiveAdmin" ? "Admin" : role, role != "InactiveAdmin", null));
        }
    }

    [Fact]
    public async Task ProviderSqlLease_ReadOnlyValidation_ExcludesSwitchDuringSendAcrossConnections()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Validation connection is required.");
        var options = new SqlConnectionStringBuilder(connectionString);
        Assert.Equal("LUMAR_ERP_ES_VALIDATION", options.InitialCatalog);
        var factory = new OperationalSqlConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = connectionString }));
        var repository = new SettingsRepository(null!, factory, null!);
        var first = await repository.AcquireAsync(false, default);
        var original = first.Provider;
        try
        {
            await using var second = await repository.AcquireAsync(false, default);
            Assert.Equal(original, second.Provider);
            await Assert.ThrowsAsync<WhatsAppProviderConflictException>(() => repository.AcquireAsync(true, default));
        }
        finally { await first.DisposeAsync(); }
        await using var exclusive = await repository.AcquireAsync(true, default);
        Assert.Equal(original, exclusive.Provider);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdatePrintSettingsAsync(
            new Dictionary<string, string> { ["\uFF21ctiveWhatsAppProvider"] = "Local" }, default));
    }

    [Theory]
    [InlineData("ActiveWhatsAppProvider")]
    [InlineData(" activewhatsappprovider ")]
    public async Task ProviderSettingCannotBypassActivationThroughPrintSettings(string key)
    {
        var repository = new SettingsRepository(null!, null!, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdatePrintSettingsAsync(new Dictionary<string, string> { [key] = "Local" }, default));
    }

    [Theory]
    [InlineData(false, "Connected", true, "967777123456")]
    [InlineData(true, "Disconnected", true, null)]
    [InlineData(true, "LoggedOut", false, null)]
    [InlineData(true, "Connected", false, "967777123456")]
    [InlineData(true, "Connected", true, null)]
    public async Task UnreadyLocalCannotReplaceCurrentProvider(bool healthy, string state, bool linked, string? number)
    {
        var store = new MemoryProviderStore(ActiveWhatsAppProvider.Meta);
        using var handler = new ReadyHandler { Healthy = healthy, State = state, Linked = linked, Number = number };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5094/") };
        var service = new WhatsAppProviderService(store, new LocalWhatsAppGatewayClient(http));
        await Assert.ThrowsAsync<WhatsAppProviderConflictException>(() => service.SetAsync(ActiveWhatsAppProvider.Local, default));
        Assert.Equal(ActiveWhatsAppProvider.Meta, store.Value);
    }

    [Fact]
    public async Task ManualActivationIsExclusiveAndDisconnectDoesNotSwitchToMeta()
    {
        var store = new MemoryProviderStore(ActiveWhatsAppProvider.Meta);
        using var handler = new ReadyHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5094/") };
        var service = new WhatsAppProviderService(store, new LocalWhatsAppGatewayClient(http));
        Assert.Equal(ActiveWhatsAppProvider.Local, (await service.SetAsync(ActiveWhatsAppProvider.Local, default)).ActiveProvider);
        handler.State = "Disconnected";
        Assert.Equal(ActiveWhatsAppProvider.Local, (await service.GetAsync(default)).ActiveProvider);
        await Assert.ThrowsAsync<WhatsAppProviderConflictException>(() => service.SetAsync(ActiveWhatsAppProvider.Local, default));
        Assert.Equal(ActiveWhatsAppProvider.Local, store.Value);
        Assert.Equal(ActiveWhatsAppProvider.None, (await service.SetAsync(ActiveWhatsAppProvider.None, default)).ActiveProvider);
        Assert.Equal(ActiveWhatsAppProvider.Meta, (await service.SetAsync(ActiveWhatsAppProvider.Meta, default)).ActiveProvider);
    }

    internal sealed class MemoryProviderStore(ActiveWhatsAppProvider initial) : IWhatsAppProviderStore
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        public ActiveWhatsAppProvider Value { get; private set; } = initial;
        public async Task<IWhatsAppProviderLease> AcquireAsync(bool exclusive, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            return new Lease(this, exclusive);
        }
        private sealed class Lease(MemoryProviderStore store, bool exclusive) : IWhatsAppProviderLease
        {
            public ActiveWhatsAppProvider Provider => store.Value;
            public Task SetAsync(ActiveWhatsAppProvider provider, CancellationToken cancellationToken)
            {
                Assert.True(exclusive);
                store.Value = provider;
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync() { store.gate.Release(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class ReadyHandler : HttpMessageHandler
    {
        public bool Healthy { get; set; } = true;
        public string State { get; set; } = "Connected";
        public bool Linked { get; set; } = true;
        public string? Number { get; set; } = "967777123456";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath == "/health"
                ? JsonSerializer.Serialize(new { healthy = Healthy, contractVersion = 1 })
                : JsonSerializer.Serialize(new { state = State, linked = Linked, currentNumber = Number });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }

    [Theory]
    [InlineData(ActiveWhatsAppProvider.Meta, "WhatsApp")]
    [InlineData(ActiveWhatsAppProvider.Local, "LocalWhatsApp")]
    [InlineData(ActiveWhatsAppProvider.None, null)]
    public void ProviderHasOneUnambiguousChannel(ActiveWhatsAppProvider provider, string? channel)
        => Assert.Equal(channel, provider.Channel());

    [Theory]
    [InlineData(null, ActiveWhatsAppProvider.Meta)]
    [InlineData("Meta", ActiveWhatsAppProvider.Meta)]
    [InlineData("Local", ActiveWhatsAppProvider.Local)]
    [InlineData("None", ActiveWhatsAppProvider.None)]
    [InlineData("invalid", ActiveWhatsAppProvider.None)]
    public void MissingSettingPreservesLegacyButCorruptSettingDoesNotFallback(string? value, ActiveWhatsAppProvider expected)
        => Assert.Equal(expected, WhatsAppProviderChannels.FromStoredValue(value));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Provider\":1}")]
    [InlineData("{\"Provider\":\"Both\"}")]
    [InlineData("{\"Provider\":\"None, Meta\"}")]
    [InlineData("{\"Provider\":\"Local, Local\"}")]
    [InlineData("{\"Provider\":\"Local\",\"MetaActive\":true}")]
    public void InvalidOrCombinedProviderRequestIsRejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SetWhatsAppProviderRequest>(json));
}