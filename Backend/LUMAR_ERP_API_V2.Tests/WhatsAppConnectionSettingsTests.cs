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
        var model = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(ok.Value);
        Assert.True(model.TokenConfigured);
        Assert.True(model.ConnectionConfigured);
        Assert.True(model.IsConfigured);
        Assert.Equal("123456", model.PhoneNumberId);
        Assert.False(string.IsNullOrWhiteSpace(model.PhoneNumberId));
        Assert.False(string.IsNullOrWhiteSpace(model.Message));

        var secretProperties = new[]
        {
            "AccessToken",
            "VerifyToken",
            "AppSecret",
            "AccessTokenMasked",
            "VerifyTokenMasked",
            "AppSecretMasked"
        };

        foreach (var propertyName in secretProperties)
        {
            var property = typeof(WhatsAppConnectionSettingsResponseDto).GetProperty(propertyName);
            Assert.Null(property);
        }
    }

    [Fact]
    public async Task UnauthorizedUser_CannotReadOrUpdateSettings()
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userRole: "Customer");

        var status = await controller.GetStatus(CancellationToken.None);
        var statusResult = Assert.IsType<ObjectResult>(status.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        var updateResult = Assert.IsType<ObjectResult>(update.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, updateResult.StatusCode);
    }

    [Fact]
    public async Task MissingUser_ReturnsUnauthorized()
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userExists: false);

        var status = await controller.GetStatus(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(status.Result);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(update.Result);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("AdminViewer")]
    [InlineData("NotAdministrator")]
    [InlineData("AssistantAdmin")]
    public async Task NonAdminRoleNames_AreRejected(string userRole)
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userRole: userRole);

        var status = await controller.GetStatus(CancellationToken.None);
        var statusResult = Assert.IsType<ObjectResult>(status.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        var updateResult = Assert.IsType<ObjectResult>(update.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, updateResult.StatusCode);
    }

    [Theory]
    [InlineData("Admin", 200)]
    [InlineData("System Administrator", 200)]
    [InlineData("Customer", 403)]
    [InlineData("AdminViewer", 403)]
    [InlineData("NotAdministrator", 403)]
    [InlineData("AssistantAdmin", 403)]
    public async Task SettingsController_EnforcesExplicitAdminContract(string? userRole, int expectedStatus)
    {
        var controller = CreateSettingsController(userRole: userRole, userExists: true);

        var get = await controller.WhatsAppConnection(CancellationToken.None);
        AssertStatusCode(get.Result, expectedStatus);

        var update = await controller.UpdateWhatsAppConnection(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        AssertStatusCode(update.Result, expectedStatus);
    }

    [Fact]
    public async Task SettingsController_NoUser_ReturnsUnauthorized()
    {
        var controller = CreateSettingsController(userRole: "Admin", userExists: false);

        var get = await controller.WhatsAppConnection(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(get.Result);

        var update = await controller.UpdateWhatsAppConnection(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "999",
            AccessToken = "new-token"
        }, CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(update.Result);
    }

    [Fact]
    public async Task AdminRole_IsAlsoAllowedForSettings()
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userRole: "Admin");

        var status = await controller.GetStatus(CancellationToken.None);
        var statusOk = Assert.IsType<OkObjectResult>(status.Result);
        var statusModel = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(statusOk.Value);
        Assert.True(statusModel.ConnectionConfigured);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "777",
            AccessToken = "new-secret"
        }, CancellationToken.None);
        var updateOk = Assert.IsType<OkObjectResult>(update.Result);
        var updateModel = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(updateOk.Value);
        Assert.Equal("777", updateModel.PhoneNumberId);
        Assert.True(updateModel.ConnectionConfigured);
    }

    [Fact]
    public async Task SystemAdministratorCanReadAndUpdateSettings()
    {
        var controller = CreateController(
            new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = "secret" },
            userRole: "System Administrator");

        var status = await controller.GetStatus(CancellationToken.None);
        var statusOk = Assert.IsType<OkObjectResult>(status.Result);
        var statusModel = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(statusOk.Value);
        Assert.True(statusModel.ConnectionConfigured);

        var update = await controller.Update(new WhatsAppConnectionSettingsUpdateDto
        {
            Enabled = true,
            PhoneNumberId = "777",
            AccessToken = "new-secret"
        }, CancellationToken.None);
        var updateOk = Assert.IsType<OkObjectResult>(update.Result);
        var updateModel = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(updateOk.Value);
        Assert.Equal("777", updateModel.PhoneNumberId);
        Assert.True(updateModel.ConnectionConfigured);
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
        var model = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(ok.Value);
        Assert.Equal("2222", model.PhoneNumberId);
        Assert.True(model.ConnectionConfigured);
        Assert.True(model.IsConfigured);
        Assert.False(model.Message is not null && model.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));

        var status = await controller.GetStatus(CancellationToken.None);
        var statusOk = Assert.IsType<OkObjectResult>(status.Result);
        var statusModel = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(statusOk.Value);
        Assert.Equal("2222", statusModel.PhoneNumberId);
        Assert.True(statusModel.ConnectionConfigured);
    }

    [Fact]
    public async Task MissingToken_MakesConnectionConfiguredFalse()
    {
        var controller = CreateController(new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = "123456", AccessToken = string.Empty });
        var result = await controller.GetStatus(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(ok.Value);
        Assert.False(model.ConnectionConfigured);
        Assert.False(model.TokenConfigured);
        Assert.False(model.IsConfigured);
    }

    [Fact]
    public async Task MissingPhoneNumberId_MakesConnectionConfiguredFalse()
    {
        var controller = CreateController(new WhatsAppConnectionSettingsOptions { Enabled = true, PhoneNumberId = string.Empty, AccessToken = "token" });
        var result = await controller.GetStatus(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<WhatsAppConnectionSettingsResponseDto>(ok.Value);
        Assert.False(model.ConnectionConfigured);
        Assert.False(model.IsConfigured);
        Assert.True(string.IsNullOrEmpty(model.PhoneNumberId));
        Assert.True(model.TokenConfigured);
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
        string? userRole = "System Administrator",
        CurrentUserDto? user = null,
        bool userExists = true)
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
        var currentUser = user ?? new CurrentUserDto(1, "admin", "Admin User", userRole ?? "System Administrator", true, DateTime.UtcNow);
        var controller = new WhatsAppSettingsController(
            store,
            FakeAuthenticatedUserContext.Create(currentUser, userExists));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static SettingsController CreateSettingsController(
        string? userRole = "System Administrator",
        CurrentUserDto? user = null,
        bool userExists = true)
    {
        var settingsService = new FakeSettingsService();
        var currentUser = user ?? new CurrentUserDto(1, "admin", "Admin User", userRole ?? "System Administrator", true, DateTime.UtcNow);
        var controller = new SettingsController(
            settingsService,
            FakeAuthenticatedUserContext.Create(currentUser, userExists));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static void AssertStatusCode(ActionResult? result, int expectedStatus)
    {
        if (result is UnauthorizedResult unauthorized)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            return;
        }

        var objectResult = result as ObjectResult;
        Assert.NotNull(objectResult);
        Assert.Equal(expectedStatus, objectResult!.StatusCode ?? expectedStatus);
    }

    private sealed class FakeAuthenticatedUserContext : IAuthenticatedUserContext
    {
        private readonly CurrentUserDto? overrideUser;
        private readonly bool userExists;

        private FakeAuthenticatedUserContext(CurrentUserDto? overrideUser, bool userExists)
        {
            this.overrideUser = overrideUser;
            this.userExists = userExists;
        }

        public static FakeAuthenticatedUserContext Create(CurrentUserDto? overrideUser, bool userExists)
        {
            return new FakeAuthenticatedUserContext(overrideUser, userExists);
        }

        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(userExists ? overrideUser : null);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());

        public Task<SystemSettingDetailsDto?> GetByIdAsync(int i, CancellationToken c) =>
            Task.FromResult<SystemSettingDetailsDto?>(null);

        public Task<SystemSettingDetailsDto?> GetByKeyAsync(string k, CancellationToken c) =>
            Task.FromResult<SystemSettingDetailsDto?>(null);

        public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SettingCategoryDto>>(Array.Empty<SettingCategoryDto>());

        public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());

        public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) =>
            Task.FromResult(new ProductionRoutesConfigDto(Array.Empty<ProductionRouteEntryDto>()));

        public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) =>
            Task.FromResult(config);

        public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) =>
            Task.FromResult(new MessageSettingsDto(false, false, false, false, false, true));

        public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c) =>
            Task.FromResult(config);

        public Task<WhatsAppConnectionSettingsResponseDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c) =>
            Task.FromResult(new WhatsAppConnectionSettingsResponseDto(true, "https://graph.facebook.com", "v20.0", "123456", 30, true, true, true, "تمت تهيئة اتصال واتساب بشكل كامل."));

        public Task<WhatsAppConnectionSettingsResponseDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsUpdateDto config, CancellationToken c) =>
            Task.FromResult(new WhatsAppConnectionSettingsResponseDto(true, "https://graph.facebook.com", "v20.0", config.PhoneNumberId ?? "123456", 30, true, true, true, "تمت تهيئة اتصال واتساب بشكل كامل."));

        public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c) => Task.FromResult(true);
        public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => Task.FromResult(true);
        public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) =>
            Task.FromResult(new GeneralCashAccountSettingDto(null, null, false, ""));
        public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) =>
            Task.FromResult(new GeneralCashAccountSettingDto(cashAccountId, null, false, ""));
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
