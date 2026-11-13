using System.Diagnostics;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SettingsControllerReadinessTests
{
    [Fact]
    public void SettingsController_CanBeResolvedThroughAspNetDi()
    {
        var expected = new TestDataResetReadinessResultDto(
            Success: true,
            DatabaseName: "LUMAR_ERP_ES_VALIDATION_R3",
            ScriptPath: "D:\\YASIN\\Backend\\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1",
            WorkingDirectory: "D:\\YASIN\\Backend",
            ManifestPath: "D:\\YASIN\\Backend\\R3_RESET_REFERENCE_MANIFEST.csv",
            DependencyGraphPath: "D:\\YASIN\\Backend\\R3_RESET_DEPENDENCY_GRAPH.csv",
            ExitCode: 0,
            StdOutSummary: "Preview started successfully",
            StdErrSummary: string.Empty,
            FailureStage: "NONE",
            FailureCode: "NONE",
            CorrelationId: "corr-di-123");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISettingsService>(new FakeSettingsService(expected));
        services.AddSingleton<IAuthenticatedUserContext>(new FakeAuthenticatedUserContext(new CurrentUserDto(
            UserId: 7,
            Username: "admin",
            FullName: "System Admin",
            Role: "Admin",
            IsActive: true,
            LastLoginUtc: DateTime.UtcNow)));

        var provider = services.BuildServiceProvider();
        var controller = (SettingsController)ActivatorUtilities.CreateInstance(provider, typeof(SettingsController));

        Assert.NotNull(controller);
        Assert.Equal(typeof(SettingsController), controller.GetType());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var actionResult = controller.GetTestDataResetReadiness(CancellationToken.None).GetAwaiter().GetResult();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<TestDataResetReadinessResultDto>(okResult.Value);
        Assert.True(dto.Success);
        Assert.Equal("NONE", dto.FailureCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsPreviewResult_ForAdminUser()
    {
        var expected = new TestDataResetReadinessResultDto(
            Success: true,
            DatabaseName: "LUMAR_ERP_ES_VALIDATION_R3",
            ScriptPath: "D:\\YASIN\\Backend\\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1",
            WorkingDirectory: "D:\\YASIN\\Backend",
            ManifestPath: "D:\\YASIN\\Backend\\R3_RESET_REFERENCE_MANIFEST.csv",
            DependencyGraphPath: "D:\\YASIN\\Backend\\R3_RESET_DEPENDENCY_GRAPH.csv",
            ExitCode: 0,
            StdOutSummary: "Preview started successfully",
            StdErrSummary: string.Empty,
            FailureStage: "NONE",
            FailureCode: "NONE",
            CorrelationId: "corr-123");

        var service = new FakeSettingsService(expected);
        var userContext = new FakeAuthenticatedUserContext(new CurrentUserDto(
            UserId: 7,
            Username: "admin",
            FullName: "System Admin",
            Role: "Admin",
            IsActive: true,
            LastLoginUtc: DateTime.UtcNow));

        var controller = new SettingsController(service, userContext);

        var result = await controller.GetTestDataResetReadiness(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TestDataResetReadinessResultDto>(okResult.Value);
        Assert.True(dto.Success);
        Assert.Equal("LUMAR_ERP_ES_VALIDATION_R3", dto.DatabaseName);
        Assert.Equal(0, dto.ExitCode);
        Assert.Equal("NONE", dto.FailureStage);
        Assert.Equal("corr-123", dto.CorrelationId);
    }

    [Fact]
    public async Task SettingsService_ReadinessPreview_UsesRealScript_AndWritesLogFile()
    {
        var repo = new FakeSettingsRepository();
        var settings = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor());

        var readiness = await settings.GetTestDataResetReadinessAsync(CancellationToken.None);

        Assert.True(readiness.Success, readiness.StdErrSummary ?? "Preview did not succeed.");
        Assert.Equal("LUMAR_ERP_ES_VALIDATION_R3", readiness.DatabaseName);
        Assert.Equal(0, readiness.ExitCode);
        Assert.NotEmpty(readiness.CorrelationId);
        Assert.Contains("R3_SAFE_ZERO_OPERATIONAL_RESET.ps1", readiness.ScriptPath);
        Assert.True(File.Exists(Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{readiness.CorrelationId}.log")), "Reset log file was not written.");
    }

    [Fact]
    public async Task LiveReset_UsesOfficialTimeoutConfiguration_AndLogsConfiguredTimeout()
    {
        var repo = new FakeSettingsRepository();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "240"
            })
            .Build();

        var processIds = new List<int>();
        var service = new SettingsService(
            repo,
            config,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            startInfo =>
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c echo VERIFY_RESULT=PASS& echo OPERATIONAL_ROWS_AFTER=0& echo NONZERO_OPERATIONAL_TABLES_AFTER=NONE& echo PROTECTED_ROWS_PRESERVED=PASS& echo ORPHAN_CHECK=PASS& echo FINANCIAL_INTEGRITY=PASS& exit /b 0",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                if (process is not null)
                {
                    processIds.Add(process.Id);
                }

                return process;
            });

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.True(result.Success, result.Details);
        var logPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{result.CorrelationId}.log");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath);
        Assert.Contains("ConfiguredTimeoutSeconds=240", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveReset_WhenConfiguredTimeoutIsBelowSafetyFloor_FailsWithInvalidTimeoutGuard()
    {
        var repo = new FakeSettingsRepository();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "120"
            })
            .Build();

        var service = new SettingsService(
            repo,
            config,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor());

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_RESET_TIMEOUT_CONFIGURATION", result.FailureCode);
        Assert.Equal("INVALID_TIMEOUT_CONFIGURATION", result.FailureStage);
    }

    [Fact]
    public async Task ExecuteTestDataResetAsync_WhenRequestCancelledAfterStart_RecordsLog_AndClassifiesRequestCancelled()
    {
        var repo = new FakeSettingsRepository();
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "req-cancel-123";
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        var service = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            httpContextAccessor,
            startInfo =>
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping -n 20 127.0.0.1 > nul",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                return process;
            });

        using var cts = new CancellationTokenSource();
        var requestTask = Task.Run(async () =>
        {
            await Task.Delay(250);
            cts.Cancel();
        });

        var result = await service.ExecuteTestDataResetAsync(cts.Token);
        await requestTask;

        Assert.False(result.Success);
        Assert.Equal("REQUEST_CANCELLED", result.FailureCode);
        Assert.NotNull(result.CorrelationId);
        Assert.Equal("req-cancel-123", result.CorrelationId);
        var cancelLogPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{result.CorrelationId}.log");
        Assert.True(File.Exists(cancelLogPath));
        var cancelLog = File.ReadAllText(cancelLogPath);
        Assert.Contains("Stage=PROCESS_STARTING", cancelLog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTestDataResetAsync_WhenPowerShellExitsNonZero_RecordsFailureCode()
    {
        var repo = new FakeSettingsRepository();
        var service = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit /b 17",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }));

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PROCESS_EXIT_NONZERO", result.FailureCode);
        Assert.NotNull(result.CorrelationId);
        var nonZeroLogPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{result.CorrelationId}.log");
        Assert.True(File.Exists(nonZeroLogPath));
        var nonZeroLog = File.ReadAllText(nonZeroLogPath);
        Assert.Contains("Stage=PROCESS_STARTING", nonZeroLog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteTestDataResetAsync_WhenProcessTimesOut_RecordsTimeout()
    {
        var repo = new FakeSettingsRepository();
        var spawnedProcessId = 0;
        var service = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ =>
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping -n 30 127.0.0.1 > nul",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                spawnedProcessId = process?.Id ?? 0;
                return process;
            },
            TimeSpan.FromSeconds(2));

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("RESET_TIMEOUT", result.FailureCode);
        Assert.NotNull(result.CorrelationId);
        var timeoutLogPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{result.CorrelationId}.log");
        Assert.True(File.Exists(timeoutLogPath));
        var timeoutLog = File.ReadAllText(timeoutLogPath);
        Assert.Contains("Stage=PROCESS_STARTING", timeoutLog, StringComparison.OrdinalIgnoreCase);
        Assert.True(spawnedProcessId > 0);
        Assert.Empty(Process.GetProcesses().Where(x => x.Id == spawnedProcessId));
    }

    [Fact]
    public async Task ReadinessAndExecuteUseSameLiveConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "180",
                ["TestDataReset:LiveMode"] = bool.TrueString,
                ["TestDataReset:TestOverride"] = bool.FalseString,
            })
            .Build();

        var service = SettingsService.CreateForTest(
            new FakeSettingsRepository(),
            configuration,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo VERIFY_RESULT=PASS& echo OPERATIONAL_ROWS_AFTER=0& echo NONZERO_OPERATIONAL_TABLES_AFTER=NONE& echo PROTECTED_ROWS_PRESERVED=PASS& echo ORPHAN_CHECK=PASS& echo FINANCIAL_INTEGRITY=PASS& exit /b 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }),
            resetTimeout: null);

        var readiness = await service.GetTestDataResetReadinessAsync(CancellationToken.None);
        var execute = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.True(readiness.Success);
        Assert.Equal(180, readiness.ConfiguredTimeoutSeconds);
        Assert.True(readiness.LiveMode);
        Assert.False(readiness.TestOverride);
        Assert.True(execute.Success);

        var logPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{execute.CorrelationId}.log");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath);
        Assert.Contains("ConfiguredTimeoutSeconds=180", log, StringComparison.Ordinal);
        Assert.Contains("LiveMode=True", log, StringComparison.Ordinal);
        Assert.Contains("TestOverride=False", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostExecuteUsesLiveConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "180",
                ["TestDataReset:LiveMode"] = bool.TrueString,
                ["TestDataReset:TestOverride"] = bool.FalseString,
            })
            .Build();

        var service = SettingsService.CreateForTest(
            new FakeSettingsRepository(),
            configuration,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo VERIFY_RESULT=PASS& echo OPERATIONAL_ROWS_AFTER=0& echo NONZERO_OPERATIONAL_TABLES_AFTER=NONE& echo PROTECTED_ROWS_PRESERVED=PASS& echo ORPHAN_CHECK=PASS& echo FINANCIAL_INTEGRITY=PASS& exit /b 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }),
            resetTimeout: null);

        var controller = new SettingsController(service, new FakeAuthenticatedUserContext(new CurrentUserDto(
            UserId: 7,
            Username: "admin",
            FullName: "System Admin",
            Role: "Admin",
            IsActive: true,
            LastLoginUtc: DateTime.UtcNow)));

        var result = await controller.ExecuteTestDataReset(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TestDataResetExecutionResultDto>(okResult.Value);
        Assert.True(dto.Success);

        var logPath = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{dto.CorrelationId}.log");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath);
        Assert.Contains("ConfiguredTimeoutSeconds=180", log, StringComparison.Ordinal);
        Assert.Contains("LiveMode=True", log, StringComparison.Ordinal);
        Assert.Contains("TestOverride=False", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidLiveConfigurationIsBlockedBeforeProcessStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "120",
                ["TestDataReset:LiveMode"] = bool.FalseString,
                ["TestDataReset:TestOverride"] = bool.TrueString,
            })
            .Build();

        var started = false;
        var service = new SettingsService(
            new FakeSettingsRepository(),
            config,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ =>
            {
                started = true;
                return Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c exit /b 0",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
            });

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_RESET_RUNTIME_CONFIGURATION", result.FailureCode);
        Assert.Equal("CONFIGURATION", result.FailureStage);
        Assert.False(started);
    }

    [Fact]
    public async Task ExecuteTestDataResetAsync_WhenVerifyOutputIsIncomplete_FailsContractEvenIfExitCodeIsZero()
    {
        var repo = new FakeSettingsRepository();
        var service = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor(),
            _ => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo VERIFY_RESULT=FAIL& echo OPERATIONAL_ROWS_AFTER=3& echo NONZERO_OPERATIONAL_TABLES_AFTER=Suppliers& echo PROTECTED_ROWS_PRESERVED=PASS& echo ORPHAN_CHECK=PASS& echo FINANCIAL_INTEGRITY=PASS& exit /b 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }));

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("VERIFY_CONTRACT", result.FailureStage);
        Assert.Contains("OPERATIONAL_ROWS_REMAIN", result.FailureCode ?? "");
        Assert.Contains("اكتمل تشغيل أداة التنظيف", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteTestDataResetAsync_SanitizesCorrelationIdForWindowsLogFileNames()
    {
        var repo = new FakeSettingsRepository();
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "0HNPTNA8MOC2H:00000001";
        var service = new SettingsService(
            repo,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor { HttpContext = httpContext },
            _ => Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo VERIFY_RESULT=PASS& echo OPERATIONAL_ROWS_AFTER=0& echo NONZERO_OPERATIONAL_TABLES_AFTER=NONE& echo PROTECTED_ROWS_PRESERVED=PASS& echo ORPHAN_CHECK=PASS& echo FINANCIAL_INTEGRITY=PASS& exit /b 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }));

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.NotNull(result.CorrelationId);
        Assert.Equal("0HNPTNA8MOC2H:00000001", result.CorrelationId);
        var sanitized = Path.Combine("D:\\YASIN\\Backend", "logs", $"test-data-reset-{SanitizeForTest(httpContext.TraceIdentifier)}.log");
        Assert.True(File.Exists(sanitized), "The sanitized log file should be created.");
        var log = File.ReadAllText(sanitized);
        Assert.Contains("CorrelationId=0HNPTNA8MOC2H:00000001", log, StringComparison.Ordinal);
    }

    private static string SanitizeForTest(string input)
        => string.Concat(input.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_'));

    [Fact]
    public async Task ProductionDIUsesCreateForLiveOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "180",
                ["TestDataReset:LiveMode"] = bool.TrueString,
                ["TestDataReset:TestOverride"] = bool.FalseString,
            })
            .Build());
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IOptionsMonitor<WhatsAppOptions>>(new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()));
        services.AddSingleton<IWhatsAppConnectionSettingsStore, FakeWhatsAppSettingsStore>();
        services.AddSingleton<ILogger<SettingsService>>(NullLogger<SettingsService>.Instance);
        services.AddSingleton<ISettingsRepository, FakeSettingsRepository>();
        services.AddSingleton<ISettingsService>(sp => SettingsService.CreateForLive(
            sp.GetRequiredService<ISettingsRepository>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IOptionsMonitor<WhatsAppOptions>>(),
            sp.GetRequiredService<IWhatsAppConnectionSettingsStore>(),
            sp.GetRequiredService<ILogger<SettingsService>>(),
            sp.GetRequiredService<IHttpContextAccessor>()));

        var provider = services.BuildServiceProvider();
        var live = provider.GetRequiredService<ISettingsService>();
        var readiness = await live.GetTestDataResetReadinessAsync(CancellationToken.None);

        Assert.IsType<SettingsService>(live);
        Assert.Equal(180, readiness.ConfiguredTimeoutSeconds);
        Assert.True(readiness.LiveMode);
        Assert.False(readiness.TestOverride);
    }

    [Fact]
    public async Task TestOverrideCannotReachProductionDI()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDataReset:TimeoutSeconds"] = "2",
                ["TestDataReset:LiveMode"] = bool.FalseString,
                ["TestDataReset:TestOverride"] = bool.TrueString,
            })
            .Build();

        var service = SettingsService.CreateForLive(
            new FakeSettingsRepository(),
            configuration,
            new FakeOptionsMonitor<WhatsAppOptions>(new WhatsAppOptions()),
            new FakeWhatsAppSettingsStore(),
            NullLogger<SettingsService>.Instance,
            new HttpContextAccessor());

        var result = await service.ExecuteTestDataResetAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("INVALID_RESET_RUNTIME_CONFIGURATION", result.FailureCode);
        Assert.Equal("CONFIGURATION", result.FailureStage);
    }

    private sealed class FakeSettingsService(TestDataResetReadinessResultDto readiness) : ISettingsService
    {
        public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<SystemSettingDetailsDto?> GetByIdAsync(int i, CancellationToken c) => throw new NotImplementedException();
        public Task<SystemSettingDetailsDto?> GetByKeyAsync(string k, CancellationToken c) => throw new NotImplementedException();
        public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken c) => throw new NotImplementedException();
        public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) => throw new NotImplementedException();
        public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c) => throw new NotImplementedException();
        public Task<WhatsAppConnectionSettingsResponseDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<WhatsAppConnectionSettingsResponseDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsUpdateDto config, CancellationToken c) => throw new NotImplementedException();
        public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c) => throw new NotImplementedException();
        public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => throw new NotImplementedException();
        public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) => throw new NotImplementedException();
        public Task<TestDataResetExecutionResultDto> ExecuteTestDataResetAsync(CancellationToken c) => throw new NotImplementedException();
        public Task<TestDataResetReadinessResultDto> GetTestDataResetReadinessAsync(CancellationToken c) => Task.FromResult(readiness);
    }

    private sealed class FakeAuthenticatedUserContext(CurrentUserDto user) : IAuthenticatedUserContext
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrentUserDto?>(user);
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<SystemSettingDetailsDto?> GetByIdAsync(int i, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(null);
        public Task<SystemSettingDetailsDto?> GetByKeyAsync(string key, CancellationToken c) => Task.FromResult<SystemSettingDetailsDto?>(null);
        public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SettingCategoryDto>>(Array.Empty<SettingCategoryDto>());
        public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken c) => Task.FromResult<IReadOnlyList<SystemSettingListDto>>(Array.Empty<SystemSettingListDto>());
        public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) => Task.FromResult(new ProductionRoutesConfigDto(Array.Empty<ProductionRouteEntryDto>()));
        public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) => Task.FromResult(config);
        public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) => Task.FromResult(new MessageSettingsDto(false, false, false, false, false, true));
        public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c) => Task.FromResult(config);
        public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c) => Task.FromResult(true);
        public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => Task.FromResult(true);
        public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) => Task.FromResult(new GeneralCashAccountSettingDto(null, null, false, string.Empty));
        public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) => Task.FromResult(new GeneralCashAccountSettingDto(cashAccountId, null, false, string.Empty));
    }

    private sealed class FakeWhatsAppSettingsStore : IWhatsAppConnectionSettingsStore
    {
        public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new WhatsAppConnectionStatusDto(true, "123456", true, true, "ok"));
        public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(new WhatsAppRuntimeSettings(true, "https://graph.facebook.com", "v20.0", "123456", "token", "verify", "secret", 30));
        public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken) => GetStatusAsync(cancellationToken);
        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
