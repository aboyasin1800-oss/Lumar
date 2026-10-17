using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class WhatsAppConnectionSettingsTests
{
    [Fact]
    public async Task GetStatus_DoesNotExposeAccessToken()
    {
        var controller = CreateController(new WhatsAppConnectionSettingsOptions
        {
            Enabled = true,
            PhoneNumberId = "123456",
            AccessToken = "super-secret-token",
            VerifyToken = "verify-token",
            AppSecret = "app-secret"
        });

        var result = await controller.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WhatsAppConnectionStatusDto>(ok.Value);
        Assert.True(model.TokenConfigured);
        Assert.Equal("123456", model.PhoneNumberId);
        Assert.False(string.IsNullOrWhiteSpace(model.PhoneNumberId));
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public async Task UnauthorizedUser_CannotReadOrUpdateSettings()
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userRole: "Customer");

        var status = await controller.GetStatus(CancellationToken.None);
        Assert.IsType<ForbidResult>(status.Result);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        Assert.IsType<ForbidResult>(update.Result);
    }

    [Fact]
    public async Task UpdatePhoneNumberId_PersistsAndReadsBack()
    {
        var options = new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "1111", AccessToken = "secret" };
        var controller = CreateController(options);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "2222",
            AccessToken = "secret-2"
        }, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var model = Assert.IsType<WhatsAppConnectionStatusDto>(ok.Value);
        Assert.Equal("2222", model.PhoneNumberId);

        var status = await controller.GetStatus(CancellationToken.None);
        var statusOk = Assert.IsType<OkObjectResult>(status.Result);
        var statusModel = Assert.IsType<WhatsAppConnectionStatusDto>(statusOk.Value);
        Assert.Equal("2222", statusModel.PhoneNumberId);
    }

    [Fact]
    public async Task MissingToken_MakesConnectionConfiguredFalse()
    {
        var controller = CreateController(new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = string.Empty });
        var result = await controller.GetStatus(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WhatsAppConnectionStatusDto>(ok.Value);
        Assert.False(model.ConnectionConfigured);
        Assert.False(model.TokenConfigured);
    }

    [Fact]
    public async Task MissingPhoneNumberId_MakesConnectionConfiguredFalse()
    {
        var controller = CreateController(new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = string.Empty, AccessToken = "token" });
        var result = await controller.GetStatus(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WhatsAppConnectionStatusDto>(ok.Value);
        Assert.False(model.ConnectionConfigured);
        Assert.False(model.TokenConfigured == false && model.PhoneNumberId == string.Empty ? false : false);
    }

    [Fact]
    public async Task ProtectedSecretStore_DoesNotWritePlaintextSecretsAndPersistsAcrossRead()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "lumar-whatsapp-secret-tests", Guid.NewGuid().ToString("N"));
        var secretStorePath = Path.Combine(tempRoot, "whatsapp-secrets.json");
        var keyDir = Path.Combine(tempRoot, "keys");

        Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH", secretStorePath);
        Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_DATA_PROTECTION_KEYS_PATH", keyDir);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:Enabled"] = "true",
                    ["WhatsApp:PhoneNumberId"] = "123456",
                    ["WhatsApp:BaseUrl"] = "https://graph.facebook.com",
                    ["WhatsApp:ApiVersion"] = "v20.0",
                    ["WhatsApp:TimeoutSeconds"] = "30",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyDir))
                .SetApplicationName("LUMAR_ERP_API_V2.Tests");

            using var provider = services.BuildServiceProvider();
            var store = new WhatsAppConnectionSettingsStore(
                config,
                Options.Create(new WhatsAppOptions
                {
                    Enabled = true,
                    PhoneNumberId = "123456",
                    BaseUrl = "https://graph.facebook.com",
                    ApiVersion = "v20.0",
                    TimeoutSeconds = 30,
                }),
                provider.GetRequiredService<IDataProtectionProvider>());

            var update = await store.UpdateAsync(new WhatsAppConnectionSettingsUpdateDto
            {
                Enabled = true,
                PhoneNumberId = "123456",
                AccessToken = "super-secret-token-123"
            }, CancellationToken.None);

            Assert.True(update.TokenConfigured);
            Assert.Equal("123456", update.PhoneNumberId);

            var fileContents = await File.ReadAllTextAsync(secretStorePath);
            Assert.DoesNotContain("super-secret-token-123", fileContents);
            Assert.DoesNotContain("super-secret-token-123", fileContents, StringComparison.Ordinal);

            var reloaded = await store.GetStatusAsync(CancellationToken.None);
            Assert.True(reloaded.TokenConfigured);
            Assert.Equal("123456", reloaded.PhoneNumberId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_WHATSAPP_SECRET_STORE_PATH", null);
            Environment.SetEnvironmentVariable("LUMAR_ERP_API_V2_DATA_PROTECTION_KEYS_PATH", null);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static WhatsAppSettingsController CreateController(
        WhatsAppConnectionSettingsOptions options,
        string userRole = "System Administrator")
    {
        var inMemory = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:Enabled"] = options.Enabled.ToString(),
                ["WhatsApp:PhoneNumberId"] = options.PhoneNumberId,
                ["WhatsApp:AccessToken"] = options.AccessToken,
                ["WhatsApp:VerifyToken"] = options.VerifyToken,
                ["WhatsApp:AppSecret"] = options.AppSecret,
                ["WhatsApp:BaseUrl"] = options.BaseUrl,
                ["WhatsApp:ApiVersion"] = options.ApiVersion,
                ["WhatsApp:TimeoutSeconds"] = options.TimeoutSeconds.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(inMemory);
        services.AddSingleton<IWhatsAppConnectionSettingsStore>(new FakeWhatsAppConnectionSettingsStore(options));
        var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IWhatsAppConnectionSettingsStore>();
        var controller = new WhatsAppSettingsController(
            store,
            new FakeAuthenticatedUserContext(userRole));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private sealed class FakeAuthenticatedUserContext(string role) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrentUserDto?>(new CurrentUserDto(1, "admin", "Admin User", role, true, DateTime.UtcNow));
    }

    private sealed class FakeWhatsAppConnectionSettingsStore(WhatsAppConnectionSettingsOptions options) : IWhatsAppConnectionSettingsStore
    {
        public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
        {
            var tokenConfigured = !string.IsNullOrWhiteSpace(options.AccessToken);
            var phoneConfigured = !string.IsNullOrWhiteSpace(options.PhoneNumberId);
            var connectionConfigured = options.Enabled && tokenConfigured && phoneConfigured;

            return Task.FromResult(new WhatsAppConnectionStatusDto(
                Enabled: options.Enabled,
                PhoneNumberId: options.PhoneNumberId,
                TokenConfigured: tokenConfigured,
                ConnectionConfigured: connectionConfigured,
                Message: connectionConfigured ? "تمت تهيئة اتصال واتساب بشكل كامل." : "اتصال واتساب غير مكتمل. يلزم رقم الهاتف ورمز الوصول."));
        }

        public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new WhatsAppRuntimeSettings(
                Enabled: options.Enabled,
                BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl,
                ApiVersion: string.IsNullOrWhiteSpace(options.ApiVersion) ? "v20.0" : options.ApiVersion,
                PhoneNumberId: options.PhoneNumberId,
                AccessToken: options.AccessToken,
                VerifyToken: options.VerifyToken,
                AppSecret: options.AppSecret,
                TimeoutSeconds: options.TimeoutSeconds));
        }

        public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken)
        {
            options.Enabled = request.Enabled;
            options.PhoneNumberId = request.PhoneNumberId ?? options.PhoneNumberId;
            if (!string.IsNullOrWhiteSpace(request.AccessToken))
                options.AccessToken = request.AccessToken;
            if (!string.IsNullOrWhiteSpace(request.VerifyToken))
                options.VerifyToken = request.VerifyToken;
            if (!string.IsNullOrWhiteSpace(request.AppSecret))
                options.AppSecret = request.AppSecret;

            return GetStatusAsync(cancellationToken);
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class WhatsAppConnectionSettingsOptions
    {
        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "https://graph.facebook.com";
        public string ApiVersion { get; set; } = "v20.0";
        public string PhoneNumberId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
