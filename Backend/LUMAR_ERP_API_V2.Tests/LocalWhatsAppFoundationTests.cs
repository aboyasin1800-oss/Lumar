using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

[Collection(nameof(LocalWhatsAppGatewayProcessHostCollection))]
public sealed class LocalWhatsAppFoundationTests
{
    private static CustomerMessageDto Message() => new(12, 3, null, "OrderCreated", "existing-template",
        "WhatsApp", "Rendered title", "Final rendered LUMAR body", "Processing",
        DateTime.UtcNow, null, null, 1, null, PhoneNumber: "967777123456");

    [Theory]
    [InlineData(503, "{\"code\":\"NOT_CONNECTED\"}", true)]
    [InlineData(409, "{\"code\":\"BUSY\"}", true)]
    [InlineData(403, "{\"code\":\"TEST_RECIPIENT_REQUIRED\"}", false)]
    [InlineData(502, "{\"code\":\"SEND_UNCONFIRMED\",\"error\":\"secret\"}", false)]
    [InlineData(500, "secret raw provider stack", false)]
    [InlineData(200, "{\"outcome\":\"Sent\"}", false)]
    [InlineData(200, "{\"code\":\"NOT_CONNECTED\"}", false)]
    public async Task Adapter_MapsOnlyProvenRetryableFailures(int status, string body, bool retryable)
    {
        using var handler = new GatewayHandler { ResponseStatus = (HttpStatusCode)status, ResponseBody = body };
        using var http = Client(handler);
        var adapter = new LocalWhatsAppMessageChannel(new LocalWhatsAppGatewayClient(http));
        var result = await adapter.SendAsync(Message(), CancellationToken.None);
        var expected = body.Contains("SEND_UNCONFIRMED") || body.Contains("secret raw provider stack") || body.Contains("\"outcome\":\"Sent\"")
            ? OutboundDispatchOutcome.Unconfirmed
            : retryable ? OutboundDispatchOutcome.TransientFailure : OutboundDispatchOutcome.PermanentFailure;
        Assert.Equal(retryable, result.Retryable);
        Assert.Equal(expected, result.Outcome);
        Assert.DoesNotContain("secret", result.FailureReason!);
    }

    [Fact]
    public async Task Adapter_ReportsUnconfirmed_WhenGatewayCannotProveTheMessageId()
    {
        using var handler = new GatewayHandler { ResponseStatus = HttpStatusCode.BadGateway, ResponseBody = "{\"code\":\"SEND_UNCONFIRMED\",\"error\":\"secret\"}" };
        using var http = Client(handler);
        var adapter = new LocalWhatsAppMessageChannel(new LocalWhatsAppGatewayClient(http));

        var result = await adapter.SendAsync(Message(), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Unconfirmed, result.Outcome);
        Assert.False(result.Retryable);
        Assert.Contains("unconfirmed", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adapter_NetworkAmbiguityIsNotAutomaticallyRetried_AndCancellationPropagates()
    {
        using var handler = new GatewayHandler { Exception = new HttpRequestException("secret") };
        using var http = Client(handler);
        var adapter = new LocalWhatsAppMessageChannel(new LocalWhatsAppGatewayClient(http));
        Assert.False((await adapter.SendAsync(Message(), CancellationToken.None)).Retryable);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.SendAsync(Message(), cancellation.Token));
    }

    [Fact]
    public async Task Adapter_InvalidDestinationDoesNotCallTransport_AndMediaKeepsFinalBody()
    {
        using var handler = new GatewayHandler();
        using var http = Client(handler);
        var adapter = new LocalWhatsAppMessageChannel(new LocalWhatsAppGatewayClient(http));
        Assert.False((await adapter.SendAsync(Message() with { PhoneNumber = "group@g.us" }, CancellationToken.None)).Retryable);
        Assert.Null(handler.Path);
        await adapter.SendAsync(Message() with { CampaignImageUrl = "https://media.example/image.png", RawCampaignText = "Must not replace final body" }, CancellationToken.None);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal(Message().Body, payload.RootElement.GetProperty("text").GetString());
        Assert.Equal("https://media.example/image.png", payload.RootElement.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public void Registration_DoesNotJoinOperationalChannelEnumeration()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOutboundMessageChannel, InAppMessageChannel>();
        services.AddLocalWhatsAppFoundation(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<InAppMessageChannel>(Assert.Single(scope.ServiceProvider.GetServices<IOutboundMessageChannel>()));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LocalWhatsAppMessageChannel>());
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("http://127.0.0.1/other")]
    [InlineData("http://user:password@127.0.0.1/")]
    public void Registration_RejectsNonLocalOrCredentialBearingEndpoints(string url)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LocalWhatsAppGateway:BaseUrl"] = url }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddLocalWhatsAppFoundation(configuration));
    }

    [Fact]
    public async Task AdminApi_HttpRoutesRequireActiveAdmin_AndExposeNoSendEndpoint()
    {
        using var handler = new GatewayHandler
        {
            ResponseBody = "{\"state\":\"Connected\",\"currentNumber\":\"967777123456\",\"linked\":true,\"lastError\":\"secret from provider\",\"operationalActivation\":true}"
        };
        using var transport = Client(handler);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(LocalWhatsAppController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuthenticatedUserContext, HttpTestUserContext>();
        builder.Services.AddSingleton(new LocalWhatsAppGatewayClient(transport));
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var role in new[] { "", "Customer", "AdminViewer", "InactiveAdmin", "Admin", "System Administrator" })
            {
                http.DefaultRequestHeaders.Remove("Authorization");
                if (role.Length > 0) http.DefaultRequestHeaders.Add("Authorization", "Bearer " + role);
                var expected = role.Length == 0 ? HttpStatusCode.Unauthorized
                    : role is "Admin" or "System Administrator" ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                foreach (var route in new[] { "session/status", "session/link-material" })
                {
                    using var response = await http.GetAsync("/local-whatsapp/" + route);
                    Assert.Equal(expected, response.StatusCode);
                    Assert.True(response.Headers.CacheControl!.NoStore);
                    Assert.DoesNotContain("secret from provider", await response.Content.ReadAsStringAsync());
                }
                foreach (var action in new[] { "link", "disconnect", "reconnect", "logout", "replace" })
                {
                    using var response = await http.PostAsync("/local-whatsapp/session/" + action, null);
                    Assert.Equal(expected, response.StatusCode);
                }
            }
            using var status = JsonDocument.Parse(await http.GetStringAsync("/local-whatsapp/session/status"));
            Assert.False(status.RootElement.GetProperty("operationalActivation").GetBoolean());
            Assert.Equal("GATEWAY_ERROR", status.RootElement.GetProperty("lastError").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync("/local-whatsapp/messages/send", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/local-whatsapp/session/activate", null)).StatusCode);
            handler.ResponseStatus = HttpStatusCode.ServiceUnavailable;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await http.GetAsync("/local-whatsapp/health")).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://127.0.0.1:5094/") };

    private sealed class HttpTestUserContext(IHttpContextAccessor accessor) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var header = accessor.HttpContext!.Request.Headers.Authorization.ToString();
            if (header.Length == 0) return Task.FromResult<CurrentUserDto?>(null);
            var role = header[7..];
            return Task.FromResult<CurrentUserDto?>(new(1, "test", "Test Admin", role == "InactiveAdmin" ? "Admin" : role, role != "InactiveAdmin", null));
        }
    }

    [Fact]
    public async Task Adapter_TransportsFinalBodyWithoutMetaTemplateContract()
    {
        using var handler = new GatewayHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5094/") };
        var gateway = new LocalWhatsAppGatewayClient(http);
        var adapter = new LocalWhatsAppMessageChannel(gateway);
        var message = new CustomerMessageDto(12, 3, null, "OrderCreated", "existing-template",
            "WhatsApp", "Rendered title", "Final rendered LUMAR body", "Processing",
            DateTime.UtcNow, null, null, 1, null, PhoneNumber: "967777123456");

        var result = await adapter.SendAsync(message, CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal("local-provider-id", result.ProviderMessageId);
        Assert.False(result.Retryable);
        Assert.Equal("LocalWhatsApp", adapter.ChannelName);
        using var payload = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal(message.Body, payload.RootElement.GetProperty("text").GetString());
        Assert.Equal(message.PhoneNumber, payload.RootElement.GetProperty("recipient").GetString());
        Assert.False(payload.RootElement.TryGetProperty("template", out _));
        Assert.Equal("/messages/send", handler.Path);
    }

    [Fact]
    public void GatewayLifecycle_ResolvesOfficialCampaignMediaOriginFromBackendConfig()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CampaignMedia:PublicBaseUrl"] = "https://public-backend.example.com",
            ["LUMAR_LOCAL_MEDIA_ORIGIN"] = "https://other.invalid"
        }).Build();

        var origin = LocalWhatsAppGatewayProcessHost.ResolveMediaOrigin(configuration);

        Assert.Equal("https://public-backend.example.com", origin);
    }

    [Fact]
    public void StartupRegistration_RegisterGatewayLifecycleHost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLocalWhatsAppFoundation(new ConfigurationBuilder().Build());
        services.AddHostedService<LocalWhatsAppGatewayProcessHost>();

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, service => service is LocalWhatsAppGatewayProcessHost);
    }

    [Fact]
    public async Task GatewayLifecycle_UsesConfiguredApprovedRecipient_WhenStartingGateway()
    {
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LocalWhatsAppGateway:BaseUrl"] = baseUrl,
                ["LocalWhatsAppGateway:AutoStart"] = "true",
                ["LocalWhatsAppGateway:ApprovedTestRecipient"] = "967775106545"
            }).Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().AddConsole());
        var host = new LocalWhatsAppGatewayProcessHost(loggerFactory.CreateLogger<LocalWhatsAppGatewayProcessHost>(), configuration);

        await host.StartAsync(CancellationToken.None);

        var tokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR", "LocalWhatsAppGateway", "gateway-token");
        var token = (await File.ReadAllTextAsync(tokenPath)).Trim();
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync("/messages/send", new { recipient = "775106545", text = "final body" });
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_CONNECTED", payload.GetProperty("code").GetString());

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GatewayLifecycle_StartsHealthAndShutsDownWithoutDuplicateBinding()
    {
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LocalWhatsAppGateway:BaseUrl"] = baseUrl,
                ["LocalWhatsAppGateway:AutoStart"] = "true"
            }).Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().AddConsole());
        var host = new LocalWhatsAppGatewayProcessHost(loggerFactory.CreateLogger<LocalWhatsAppGatewayProcessHost>(), configuration);

        await host.StartAsync(CancellationToken.None);

        var tokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR", "LocalWhatsAppGateway", "gateway-token");
        var token = (await File.ReadAllTextAsync(tokenPath)).Trim();
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        HttpResponseMessage? first = null;
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                first = await client.GetAsync("/health", timeout.Token);
                if (first.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
                first = null;
            }

            await Task.Delay(200, timeout.Token);
        }

        Assert.NotNull(first);
        Assert.Equal(HttpStatusCode.OK, first!.StatusCode);

        await host.StopAsync(CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/health"));
    }

    [CollectionDefinition(nameof(LocalWhatsAppGatewayProcessHostCollection), DisableParallelization = true)]
    public sealed class LocalWhatsAppGatewayProcessHostCollection
    {
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class GatewayHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Path { get; private set; }
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "{\"outcome\":\"Sent\",\"providerMessageId\":\"local-provider-id\",\"retryable\":false}";
        public Exception? Exception { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Exception is not null) throw Exception;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Path = request.RequestUri!.AbsolutePath;
            return new HttpResponseMessage(ResponseStatus)
            {
                Content = new StringContent(ResponseBody)
            };
        }
    }
}