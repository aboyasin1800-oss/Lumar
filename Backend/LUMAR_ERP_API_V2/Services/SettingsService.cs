using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed record ResetRuntimeConfiguration(
    string DatabaseName,
    int ConfiguredTimeoutSeconds,
    bool LiveMode,
    bool TestOverride)
{
    public const string ApprovedDatabaseName = "LUMAR_ERP_ES_VALIDATION_R3";
    public const int MinimumApprovedResetTimeoutSeconds = 180;

    public static ResetRuntimeConfiguration FromConfiguration(IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("TestDataReset:TimeoutSeconds") ?? MinimumApprovedResetTimeoutSeconds;
        var liveMode = configuration.GetValue<bool?>("TestDataReset:LiveMode") ?? true;
        var testOverride = configuration.GetValue<bool?>("TestDataReset:TestOverride") ?? false;
        return new ResetRuntimeConfiguration(ApprovedDatabaseName, timeoutSeconds, liveMode, testOverride);
    }

    public bool IsSafeForLiveR3 => DatabaseName == ApprovedDatabaseName
        && ConfiguredTimeoutSeconds >= MinimumApprovedResetTimeoutSeconds
        && LiveMode
        && !TestOverride;

    public string? GetInvalidReason()
    {
        if (DatabaseName != ApprovedDatabaseName)
        {
            return $"DatabaseName={DatabaseName}; expected={ApprovedDatabaseName}";
        }

        if (ConfiguredTimeoutSeconds < MinimumApprovedResetTimeoutSeconds)
        {
            return $"ConfiguredTimeoutSeconds={ConfiguredTimeoutSeconds}; minimum approved={MinimumApprovedResetTimeoutSeconds}";
        }

        if (!LiveMode)
        {
            return "LiveMode=False";
        }

        if (TestOverride)
        {
            return "TestOverride=True";
        }

        return null;
    }
}

public sealed class SettingsService : ISettingsService
{
    private const string ApprovedDatabaseName = ResetRuntimeConfiguration.ApprovedDatabaseName;
    private const string ResetTimeoutConfigurationKey = "TestDataReset:TimeoutSeconds";
    private readonly ResetRuntimeConfiguration _runtimeConfiguration;
    private readonly ISettingsRepository r;
    private readonly IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor;
    private readonly IWhatsAppConnectionSettingsStore whatsappSettingsStore;
    private readonly ILogger<SettingsService> logger;
    private readonly IHttpContextAccessor? httpContextAccessor;
    private readonly Func<ProcessStartInfo, Process?>? processStarter;
    private readonly TimeSpan _resetTimeout;
    private readonly bool _hasTestTimeoutOverride;
    private readonly int _configuredResetTimeoutSeconds;
    private readonly bool _liveMode;
    private readonly bool _testOverride;

    public SettingsService(
        ISettingsRepository r,
        IConfiguration configuration,
        IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
        IWhatsAppConnectionSettingsStore whatsappSettingsStore,
        ILogger<SettingsService> logger,
        IHttpContextAccessor? httpContextAccessor = null,
        Func<ProcessStartInfo, Process?>? processStarter = null,
        TimeSpan? resetTimeout = null)
        : this(r,
            ResetRuntimeConfiguration.FromConfiguration(configuration),
            whatsappOptionsMonitor,
            whatsappSettingsStore,
            logger,
            httpContextAccessor,
            processStarter,
            resetTimeout)
    {
    }

    internal SettingsService(
        ISettingsRepository r,
        ResetRuntimeConfiguration runtimeConfiguration,
        IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
        IWhatsAppConnectionSettingsStore whatsappSettingsStore,
        ILogger<SettingsService> logger,
        IHttpContextAccessor? httpContextAccessor = null,
        Func<ProcessStartInfo, Process?>? processStarter = null,
        TimeSpan? resetTimeout = null)
    {
        _runtimeConfiguration = runtimeConfiguration ?? throw new ArgumentNullException(nameof(runtimeConfiguration));
        this.r = r ?? throw new ArgumentNullException(nameof(r));
        this.whatsappOptionsMonitor = whatsappOptionsMonitor ?? throw new ArgumentNullException(nameof(whatsappOptionsMonitor));
        this.whatsappSettingsStore = whatsappSettingsStore ?? throw new ArgumentNullException(nameof(whatsappSettingsStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.httpContextAccessor = httpContextAccessor;
        this.processStarter = processStarter;
        _hasTestTimeoutOverride = resetTimeout.HasValue;
        _configuredResetTimeoutSeconds = resetTimeout.HasValue
            ? (int)Math.Ceiling(resetTimeout.Value.TotalSeconds)
            : runtimeConfiguration.ConfiguredTimeoutSeconds;
        _liveMode = runtimeConfiguration.LiveMode;
        _testOverride = runtimeConfiguration.TestOverride;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(Math.Max(ResetRuntimeConfiguration.MinimumApprovedResetTimeoutSeconds, runtimeConfiguration.ConfiguredTimeoutSeconds));
    }

    public static SettingsService CreateForLive(
        ISettingsRepository repository,
        IConfiguration configuration,
        IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
        IWhatsAppConnectionSettingsStore whatsappSettingsStore,
        ILogger<SettingsService> logger,
        IHttpContextAccessor httpContextAccessor)
        => new SettingsService(
            repository,
            ResetRuntimeConfiguration.FromConfiguration(configuration),
            whatsappOptionsMonitor,
            whatsappSettingsStore,
            logger,
            httpContextAccessor,
            processStarter: null,
            resetTimeout: null);

    internal static SettingsService CreateForTest(
        ISettingsRepository repository,
        IConfiguration configuration,
        IOptionsMonitor<WhatsAppOptions> whatsappOptionsMonitor,
        IWhatsAppConnectionSettingsStore whatsappSettingsStore,
        ILogger<SettingsService> logger,
        IHttpContextAccessor? httpContextAccessor,
        Func<ProcessStartInfo, Process?>? processStarter,
        TimeSpan? resetTimeout)
        => new SettingsService(
            repository,
            ResetRuntimeConfiguration.FromConfiguration(configuration),
            whatsappOptionsMonitor,
            whatsappSettingsStore,
            logger,
            httpContextAccessor,
            processStarter,
            resetTimeout);
    private static readonly IReadOnlyDictionary<string, string> SupportedCurrencies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["YER"] = "الريال اليمني",
            ["SAR"] = "الريال السعودي",
            ["USD"] = "الدولار الأمريكي",
            ["EGP"] = "الجنيه المصري",
            ["QAR"] = "الريال القطري",
            ["OMR"] = "الريال العماني",
            ["AED"] = "الدرهم الإماراتي",
            ["KWD"] = "الدينار الكويتي",
            ["IQD"] = "الدينار العراقي",
            ["SDG"] = "الجنيه السوداني",
        };

    public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken c) => r.GetAllAsync(c);

    public Task<SystemSettingDetailsDto?> GetByIdAsync(int i, CancellationToken c) => r.GetByIdAsync(i, c);

    public Task<SystemSettingDetailsDto?> GetByKeyAsync(string k, CancellationToken c) => r.GetByKeyAsync(k, c);

    public Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken c) => r.GetCategoriesAsync(c);

    public Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken c) => r.GetByCategoryAsync(category, c);

    public Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken c) => r.GetProductionRoutesConfigAsync(c);

    public Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken c) => r.GetProductionRoutePieceTypesAsync(c);

    public Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken c) => r.UpsertProductionRoutesConfigAsync(config, c);

    public Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken c) => r.GetMessageSettingsAsync(c);

    public Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken c) => r.UpdateMessageSettingsAsync(config, c);

    public async Task<WhatsAppConnectionSettingsResponseDto> GetWhatsAppConnectionSettingsAsync(CancellationToken c)
    {
        var runtime = await whatsappSettingsStore.GetRuntimeSettingsAsync(c);
        var connectionConfigured = runtime.Enabled
            && !string.IsNullOrWhiteSpace(runtime.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(runtime.AccessToken);

        return new WhatsAppConnectionSettingsResponseDto(
            Enabled: runtime.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion,
            PhoneNumberId: runtime.PhoneNumberId ?? string.Empty,
            TimeoutSeconds: runtime.TimeoutSeconds <= 0 ? 30 : runtime.TimeoutSeconds,
            IsConfigured: connectionConfigured,
            TokenConfigured: !string.IsNullOrWhiteSpace(runtime.AccessToken),
            ConnectionConfigured: connectionConfigured,
            Message: BuildWhatsAppStatusMessage(runtime.Enabled, runtime.PhoneNumberId, runtime.AccessToken));
    }

    public async Task<WhatsAppConnectionSettingsResponseDto> UpdateWhatsAppConnectionSettingsAsync(WhatsAppConnectionSettingsUpdateDto config, CancellationToken c)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        await whatsappSettingsStore.UpdateAsync(config, c);
        return await GetWhatsAppConnectionSettingsAsync(c);
    }

    public Task<bool> UpdateCurrencySettingsAsync(string currencyName, string preferredCurrency, CancellationToken c)
    {
        var code = preferredCurrency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.TryGetValue(code, out var supportedName))
        {
            throw new ArgumentException("العملة المحددة غير مدعومة للعرض.");
        }

        var name = currencyName.Trim();
        if (!string.Equals(name, supportedName, StringComparison.Ordinal) && !(code == "YER" && name == "ريال"))
        {
            throw new ArgumentException("اسم العملة لا يطابق الكود المحدد.");
        }

        return r.UpdateCurrencySettingsAsync(currencyName, name, c);
    }

    public Task<bool> UpdatePrintSettingsAsync(IReadOnlyDictionary<string, string> values, CancellationToken c) => r.UpdatePrintSettingsAsync(values, c);

    public Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken c) => r.GetGeneralCashAccountSettingAsync(c);

    public Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken c) => r.UpsertGeneralCashAccountSettingAsync(cashAccountId, c);

    public async Task<TestDataResetExecutionResultDto> ExecuteTestDataResetAsync(CancellationToken c)
    {
        var correlationId = ResolveCorrelationId();
        var timestampUtc = DateTimeOffset.UtcNow;
        var requestReceivedAt = DateTimeOffset.UtcNow;
        var scriptPath = ResolveResetScriptPath();
        var workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
        var logPath = GetResetLogPath(correlationId);

        try
        {
            EnsureResetLogDirectory();
            WriteResetLogStage(logPath, correlationId, "RECEIVED", ApprovedDatabaseName, scriptPath, workingDirectory, mode: "Execute", processId: null, exitCode: null, failureStage: "RECEIVED", failureCode: "NONE", startTime: requestReceivedAt, endTime: requestReceivedAt, stdout: string.Empty, stderr: string.Empty, details: "Request received before starting approved reset process.");

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                var failure = new TestDataResetExecutionResultDto(
                    false,
                    "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                    ApprovedDatabaseName,
                    "Reset engine script was not found on the backend host.",
                    FailureStage: "RESOLVE_SCRIPT",
                    FailureCode: "RESET_SCRIPT_NOT_FOUND",
                    CorrelationId: correlationId,
                    TimestampUtc: timestampUtc);
                WriteResetLogStage(logPath, correlationId, "FAILED", ApprovedDatabaseName, scriptPath, workingDirectory, mode: "Execute", processId: null, exitCode: null, failureStage: "RESOLVE_SCRIPT", failureCode: "RESET_SCRIPT_NOT_FOUND", startTime: requestReceivedAt, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: "Reset engine script was not found on the backend host.", details: "Script resolution failed before process start.");
                logger.LogError("{Message} {Details} CorrelationId={CorrelationId}", failure.Message, failure.Details, correlationId);
                return failure;
            }

            WriteResetLogStage(logPath, correlationId, "SCRIPT_RESOLVED", ApprovedDatabaseName, scriptPath, workingDirectory, mode: "Execute", processId: null, exitCode: null, failureStage: "SCRIPT_RESOLVED", failureCode: "NONE", startTime: requestReceivedAt, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: string.Empty, details: "Reset script path and artifacts are resolved.");

            var executeResult = await InvokeResetScriptAsync(scriptPath, "Execute", true, c, correlationId, GetResetArtifactPath(scriptPath, "R3_RESET_REFERENCE_MANIFEST.csv"), GetResetArtifactPath(scriptPath, "R3_RESET_DEPENDENCY_GRAPH.csv"));
            if (!executeResult.Success)
            {
                logger.LogError("TestDataReset execute stage failed. CorrelationId={CorrelationId}; Stage={Stage}; FailureCode={FailureCode}; DatabaseName={DatabaseName}", correlationId, "EXECUTE", executeResult.FailureCode, ApprovedDatabaseName);
                return new TestDataResetExecutionResultDto(
                    false,
                    "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                    ApprovedDatabaseName,
                    executeResult.StdErr,
                    FailureStage: executeResult.FailureStage,
                    FailureCode: executeResult.FailureCode,
                    CorrelationId: correlationId,
                    TimestampUtc: timestampUtc);
            }

            var verifyResult = await InvokeResetScriptAsync(scriptPath, "Verify", false, c, correlationId, GetResetArtifactPath(scriptPath, "R3_RESET_REFERENCE_MANIFEST.csv"), GetResetArtifactPath(scriptPath, "R3_RESET_DEPENDENCY_GRAPH.csv"));
            var verifyContract = TryParseVerifyContract(verifyResult.StdOut, verifyResult.StdErr, verifyResult.ExitCode);
            if (!verifyResult.Success || !verifyContract.IsValid)
            {
                var failureStage = !verifyResult.Success ? verifyResult.FailureStage : "VERIFY_CONTRACT";
                var failureCode = !verifyResult.Success ? verifyResult.FailureCode : (verifyContract.OperationalRowsAfter > 0 ? "OPERATIONAL_ROWS_REMAIN" : "VERIFY_EVIDENCE_INCOMPLETE");
                var message = "اكتمل تشغيل أداة التنظيف، لكن التحقق النهائي وجد بيانات تشغيلية متبقية؛ لم تُعتمد العملية ناجحة.";
                logger.LogError("TestDataReset verify stage failed. CorrelationId={CorrelationId}; Stage={Stage}; FailureCode={FailureCode}; DatabaseName={DatabaseName}; VerifyContractValid={VerifyContractValid}; OperationalRowsAfter={OperationalRowsAfter}", correlationId, "VERIFY", failureCode, ApprovedDatabaseName, verifyContract.IsValid, verifyContract.OperationalRowsAfter);
                return new TestDataResetExecutionResultDto(
                    false,
                    message,
                    ApprovedDatabaseName,
                    verifyContract.Message ?? verifyResult.StdErr,
                    Verified: false,
                    FailureStage: failureStage,
                    FailureCode: failureCode,
                    CorrelationId: correlationId,
                    TimestampUtc: timestampUtc);
            }

            logger.LogInformation("TestDataReset completed successfully. CorrelationId={CorrelationId}; Stage={Stage}; DatabaseName={DatabaseName}; Verified={Verified}", correlationId, "COMPLETE", ApprovedDatabaseName, true);
            return new TestDataResetExecutionResultDto(
                true,
                "تم تنظيف بيانات الاختبار بنجاح.",
                ApprovedDatabaseName,
                "Execute + Verify completed successfully and the approved baseline guard remained active.",
                Verified: true,
                FailureStage: "NONE",
                FailureCode: "NONE",
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }
        catch (OperationCanceledException) when (c.IsCancellationRequested)
        {
            var failureCode = "REQUEST_CANCELLED";
            WriteResetLogStage(logPath, correlationId, "CANCELLED", ApprovedDatabaseName, scriptPath, workingDirectory, mode: "Execute", processId: null, exitCode: null, failureStage: "REQUEST_CANCELLED", failureCode: failureCode, startTime: requestReceivedAt, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: "HTTP request cancellation occurred before or during reset execution.", details: "The request token was cancelled after the process started; the execution was recorded and classified without leaving an orphaned PowerShell process.");
            logger.LogWarning("Request cancellation during reset execution. CorrelationId={CorrelationId}; Stage={Stage}; DatabaseName={DatabaseName}", correlationId, "REQUEST_CANCELLED", ApprovedDatabaseName);
            return new TestDataResetExecutionResultDto(
                false,
                "تعذر إكمال تنظيف بيانات الاختبار. تم إلغاء الطلب أثناء التنفيذ.",
                ApprovedDatabaseName,
                "HTTP request cancellation occurred before the reset could complete.",
                FailureStage: "REQUEST_CANCELLED",
                FailureCode: failureCode,
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }
        catch (IOException ex)
        {
            var failureStage = "LOGGING";
            var failureCode = "RESET_EVIDENCE_LOG_UNAVAILABLE";
            logger.LogError(ex, "Reset log initialization failed for database {DatabaseName}. CorrelationId={CorrelationId}; FailureCode={FailureCode}; FailureStage={FailureStage}", ApprovedDatabaseName, correlationId, failureCode, failureStage);
            return new TestDataResetExecutionResultDto(
                false,
                "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                ApprovedDatabaseName,
                ex.Message,
                FailureStage: failureStage,
                FailureCode: failureCode,
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }
        catch (Exception ex)
        {
            var failureStage = "FAILED";
            var failureCode = ex is TimeoutException ? "RESET_TIMEOUT" : "EXECUTE_FAILED";
            logger.LogError(ex, "Test data reset failed for approved database {DatabaseName}. CorrelationId={CorrelationId}; FailureCode={FailureCode}; FailureStage={FailureStage}", ApprovedDatabaseName, correlationId, failureCode, failureStage);
            return new TestDataResetExecutionResultDto(
                false,
                "تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.",
                ApprovedDatabaseName,
                ex.Message,
                FailureStage: failureStage,
                FailureCode: failureCode,
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }
    }

    public async Task<TestDataResetReadinessResultDto> GetTestDataResetReadinessAsync(CancellationToken c)
    {
        var correlationId = ResolveCorrelationId();
        var timestampUtc = DateTimeOffset.UtcNow;
        var scriptPath = ResolveResetScriptPath();
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return new TestDataResetReadinessResultDto(
                Success: false,
                DatabaseName: ApprovedDatabaseName,
                ScriptPath: string.Empty,
                WorkingDirectory: string.Empty,
                ManifestPath: string.Empty,
                DependencyGraphPath: string.Empty,
                ExitCode: 1,
                ConfiguredTimeoutSeconds: _configuredResetTimeoutSeconds,
                LiveMode: _liveMode,
                TestOverride: _testOverride,
                StdOutSummary: string.Empty,
                StdErrSummary: "Reset engine script was not found on the backend host.",
                FailureStage: "RESOLVE_SCRIPT",
                FailureCode: "RESET_SCRIPT_NOT_FOUND",
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }

        var manifestPath = ResolveResetArtifactPath(scriptPath, "R3_RESET_REFERENCE_MANIFEST.csv");
        var dependencyGraphPath = ResolveResetArtifactPath(scriptPath, "R3_RESET_DEPENDENCY_GRAPH.csv");

        try
        {
            var preview = await InvokeResetScriptAsync(scriptPath, "Preview", false, c, correlationId, manifestPath, dependencyGraphPath);
            return new TestDataResetReadinessResultDto(
                Success: preview.Success,
                DatabaseName: ApprovedDatabaseName,
                ScriptPath: preview.ScriptPath,
                WorkingDirectory: preview.WorkingDirectory,
                ManifestPath: preview.ManifestPath,
                DependencyGraphPath: preview.DependencyGraphPath,
                ExitCode: preview.ExitCode,
                ConfiguredTimeoutSeconds: _configuredResetTimeoutSeconds,
                LiveMode: _liveMode,
                TestOverride: _testOverride,
                StdOutSummary: TruncateForSummary(preview.StdOut),
                StdErrSummary: TruncateForSummary(preview.StdErr),
                FailureStage: preview.FailureStage,
                FailureCode: preview.FailureCode,
                CorrelationId: preview.CorrelationId,
                TimestampUtc: timestampUtc);
        }
        catch (Exception ex)
        {
            var summary = SanitizeForLog(ex.Message);
            return new TestDataResetReadinessResultDto(
                Success: false,
                DatabaseName: ApprovedDatabaseName,
                ScriptPath: scriptPath,
                WorkingDirectory: Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory,
                ManifestPath: manifestPath,
                DependencyGraphPath: dependencyGraphPath,
                ExitCode: 1,
                ConfiguredTimeoutSeconds: _configuredResetTimeoutSeconds,
                LiveMode: _liveMode,
                TestOverride: _testOverride,
                StdOutSummary: string.Empty,
                StdErrSummary: summary,
                FailureStage: "PREVIEW",
                FailureCode: "PREVIEW_FAILED",
                CorrelationId: correlationId,
                TimestampUtc: timestampUtc);
        }
    }

    private async Task<ResetScriptExecutionResult> InvokeResetScriptAsync(
        string scriptPath,
        string mode,
        bool allowExecute,
        CancellationToken cancellationToken,
        string correlationId,
        string manifestPath,
        string dependencyGraphPath)
    {
        var workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
        var databaseName = ApprovedDatabaseName;
        var logPath = GetResetLogPath(correlationId);
        var startTime = DateTimeOffset.UtcNow;
        var exitCode = -1;
        var failureStage = mode == "Execute" ? "EXECUTE" : mode == "Verify" ? "VERIFY" : mode == "Preview" ? "PREVIEW" : mode.ToUpperInvariant();
        var failureCode = "NONE";
        var stdout = string.Empty;
        var stderr = string.Empty;
        Process? process = null;

        var invalidRuntime = _runtimeConfiguration.GetInvalidReason();
        if (!string.IsNullOrWhiteSpace(invalidRuntime))
        {
            if (_configuredResetTimeoutSeconds < ResetRuntimeConfiguration.MinimumApprovedResetTimeoutSeconds && _liveMode && !_testOverride)
            {
                failureStage = "INVALID_TIMEOUT_CONFIGURATION";
                failureCode = "INVALID_RESET_TIMEOUT_CONFIGURATION";
                WriteResetLogStage(logPath, correlationId, "CONFIGURATION", databaseName, scriptPath, workingDirectory, mode, processId: null, exitCode: null, failureStage: failureStage, failureCode: failureCode, startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: invalidRuntime, details: $"ConfiguredTimeoutSeconds={_configuredResetTimeoutSeconds}; LiveMode={_liveMode}; TestOverride={_testOverride}; DatabaseName={databaseName}; SafetyFloorSeconds={ResetRuntimeConfiguration.MinimumApprovedResetTimeoutSeconds}.", configuredTimeoutSeconds: _configuredResetTimeoutSeconds);
                return new ResetScriptExecutionResult(false, 1, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
            }

            failureStage = "CONFIGURATION";
            failureCode = "INVALID_RESET_RUNTIME_CONFIGURATION";
            WriteResetLogStage(logPath, correlationId, "CONFIGURATION", databaseName, scriptPath, workingDirectory, mode, processId: null, exitCode: null, failureStage: failureStage, failureCode: failureCode, startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: invalidRuntime, details: $"ConfiguredTimeoutSeconds={_configuredResetTimeoutSeconds}; LiveMode={_liveMode}; TestOverride={_testOverride}; DatabaseName={databaseName}; SafetyFloorSeconds={ResetRuntimeConfiguration.MinimumApprovedResetTimeoutSeconds}.", configuredTimeoutSeconds: _configuredResetTimeoutSeconds);
            return new ResetScriptExecutionResult(false, 1, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
        }

        try
        {
            EnsureResetLogDirectory();
            WriteResetLogStage(logPath, correlationId, "RECEIVED", databaseName, scriptPath, workingDirectory, mode, processId: null, exitCode: null, failureStage: "RECEIVED", failureCode: "NONE", startTime: startTime, endTime: startTime, stdout: string.Empty, stderr: string.Empty, details: $"Request has reached the reset runner. ConfiguredTimeoutSeconds={_configuredResetTimeoutSeconds}; LiveMode={(!_hasTestTimeoutOverride).ToString()}; TestOverride={_hasTestTimeoutOverride.ToString()}", configuredTimeoutSeconds: _configuredResetTimeoutSeconds);

            var arguments = allowExecute
                ? $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Database \"{databaseName}\" -Mode {mode} -AllowExecute -ManifestPath \"{manifestPath}\" -DependencyPath \"{dependencyGraphPath}\""
                : $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Database \"{databaseName}\" -Mode {mode} -ManifestPath \"{manifestPath}\" -DependencyPath \"{dependencyGraphPath}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };

            if (cancellationToken.IsCancellationRequested)
            {
                failureStage = "REQUEST_CANCELLED";
                failureCode = "REQUEST_CANCELLED";
                WriteResetLogStage(logPath, correlationId, "CANCELLED", databaseName, scriptPath, workingDirectory, mode, processId: null, exitCode: null, failureStage: failureStage, failureCode: failureCode, startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: "Request cancellation was already active before process start.", details: "The request was cancelled before the PowerShell process was created.");
                return new ResetScriptExecutionResult(false, 1, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
            }

            WriteResetLogStage(logPath, correlationId, "PROCESS_STARTING", databaseName, scriptPath, workingDirectory, mode, processId: null, exitCode: null, failureStage: "PROCESS_STARTING", failureCode: "NONE", startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: string.Empty, details: $"Process start is about to begin for the approved reset engine. ConfiguredTimeoutSeconds={_configuredResetTimeoutSeconds}; SafetyFloorSeconds={ResetRuntimeConfiguration.MinimumApprovedResetTimeoutSeconds}.", configuredTimeoutSeconds: _configuredResetTimeoutSeconds);
            process = (processStarter ?? Process.Start)(startInfo) ?? throw new InvalidOperationException("Unable to start the approved reset engine.");
            var processId = process.Id;
            WriteResetLogStage(logPath, correlationId, "PROCESS_STARTED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode: null, failureStage: "PROCESS_STARTED", failureCode: "NONE", startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: string.Empty, stderr: string.Empty, details: "PowerShell process was created successfully.");

            using var timeoutCts = new CancellationTokenSource(_resetTimeout);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeoutCts.Token);

            stdout = await stdoutTask;
            stderr = await stderrTask;
            WriteResetLogStage(logPath, correlationId, "STDOUT_CAPTURED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode: null, failureStage: "STDOUT_CAPTURED", failureCode: "NONE", startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: stdout, stderr: stderr, details: "StdOut captured successfully.");
            WriteResetLogStage(logPath, correlationId, "STDERR_CAPTURED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode: null, failureStage: "STDERR_CAPTURED", failureCode: "NONE", startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: stdout, stderr: stderr, details: "StdErr captured successfully.");

            exitCode = process.ExitCode;
            WriteResetLogStage(logPath, correlationId, "PROCESS_EXITED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode, failureStage: "PROCESS_EXITED", failureCode: exitCode == 0 ? "NONE" : "PROCESS_EXIT_NONZERO", startTime: startTime, endTime: DateTimeOffset.UtcNow, stdout: stdout, stderr: stderr, details: "PowerShell process exited and output capture completed.");

            if (cancellationToken.IsCancellationRequested)
            {
                failureStage = "REQUEST_CANCELLED";
                failureCode = "REQUEST_CANCELLED";
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                }
                WriteResetLogStage(logPath, correlationId, "CANCELLED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode, failureStage, failureCode, startTime, DateTimeOffset.UtcNow, stdout, stderr, "Request cancellation was observed after process start.");
                return new ResetScriptExecutionResult(false, exitCode, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
            }

            if (exitCode != 0)
            {
                failureStage = "PROCESS_EXITED";
                failureCode = "PROCESS_EXIT_NONZERO";
                WriteResetLogStage(logPath, correlationId, "FAILED", databaseName, scriptPath, workingDirectory, mode, processId, exitCode, failureStage, failureCode, startTime, DateTimeOffset.UtcNow, stdout, stderr, "Process exited with a non-zero code.");
                return new ResetScriptExecutionResult(false, exitCode, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
            }

            WriteResetLogStage(logPath, correlationId, "COMPLETE", databaseName, scriptPath, workingDirectory, mode, processId, exitCode, failureStage: "NONE", failureCode: "NONE", startTime, DateTimeOffset.UtcNow, stdout, stderr, "Process completed successfully.");
            return new ResetScriptExecutionResult(true, exitCode, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, "NONE", "NONE", correlationId, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            failureStage = "TIMEOUT";
            failureCode = "RESET_TIMEOUT";
            if (process is not null && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            WriteResetLogStage(logPath, correlationId, "TIMEOUT", databaseName, scriptPath, workingDirectory, mode, process?.Id, exitCode, failureStage, failureCode, startTime, DateTimeOffset.UtcNow, stdout, stderr, "The reset runner exceeded the internal timeout window.");
            return new ResetScriptExecutionResult(false, exitCode, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            failureStage = ex is TimeoutException ? "TIMEOUT" : "FAILED";
            failureCode = ex is TimeoutException ? "RESET_TIMEOUT" : ex.Message.Contains("Unable to start") ? "PROCESS_START_FAILED" : "EXECUTE_FAILED";
            if (process is not null && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            WriteResetLogStage(logPath, correlationId, "FAILED", databaseName, scriptPath, workingDirectory, mode, process?.Id, exitCode, failureStage, failureCode, startTime, DateTimeOffset.UtcNow, stdout, stderr, ex.Message);
            return new ResetScriptExecutionResult(false, exitCode, scriptPath, workingDirectory, manifestPath, dependencyGraphPath, stdout, stderr, failureStage, failureCode, correlationId, DateTimeOffset.UtcNow);
        }
        finally
        {
            try
            {
                if (File.Exists(logPath))
                {
                    WriteResetLogStage(logPath, correlationId, "FINAL", databaseName, scriptPath, workingDirectory, mode, process?.Id, exitCode, failureStage, failureCode, startTime, DateTimeOffset.UtcNow, stdout, stderr, "Final log flush executed in finally block.");
                }
            }
            catch
            {
                // Not allowed to fail the reset surface; final evidence is best effort.
            }
        }
    }

    private string ResolveCorrelationId()
        => !string.IsNullOrWhiteSpace(httpContextAccessor?.HttpContext?.TraceIdentifier)
            ? httpContextAccessor.HttpContext.TraceIdentifier
            : Guid.NewGuid().ToString("N");

    private static string GetResetLogPath(string correlationId)
    {
        var sanitizedCorrelationId = SanitizeFileName(correlationId);
        var logsDirectory = Path.Combine("D:\\YASIN\\Backend", "logs");
        Directory.CreateDirectory(logsDirectory);
        return Path.Combine(logsDirectory, $"test-data-reset-{sanitizedCorrelationId}.log");
    }

    private static void EnsureResetLogDirectory()
    {
        var logsDirectory = Path.Combine("D:\\YASIN\\Backend", "logs");
        Directory.CreateDirectory(logsDirectory);
    }

    private static string GetResetArtifactPath(string scriptPath, string fileName)
        => ResolveResetArtifactPath(scriptPath, fileName);

    private static void WriteResetLogStage(
        string logPath,
        string correlationId,
        string stage,
        string databaseName,
        string? scriptPath,
        string? workingDirectory,
        string mode,
        int? processId,
        int? exitCode,
        string failureStage,
        string failureCode,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string stdout,
        string stderr,
        string details,
        int? configuredTimeoutSeconds = null)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine($"CorrelationId={correlationId}");
            builder.AppendLine($"Stage={stage}");
            builder.AppendLine($"Mode={mode}");
            builder.AppendLine($"DatabaseName={databaseName}");
            builder.AppendLine($"ScriptPath={scriptPath ?? string.Empty}");
            builder.AppendLine($"WorkingDirectory={workingDirectory ?? string.Empty}");
            builder.AppendLine($"PowerShellProcessId={processId?.ToString() ?? string.Empty}");
            builder.AppendLine($"ExitCode={exitCode?.ToString() ?? string.Empty}");
            builder.AppendLine($"ConfiguredTimeoutSeconds={configuredTimeoutSeconds?.ToString() ?? string.Empty}");
            builder.AppendLine($"FailureStage={failureStage}");
            builder.AppendLine($"FailureCode={failureCode}");
            builder.AppendLine($"StartTime={startTime:O}");
            builder.AppendLine($"EndTime={endTime:O}");
            builder.AppendLine($"StdOut={SanitizeForLog(stdout)}");
            builder.AppendLine($"StdErr={SanitizeForLog(stderr)}");
            builder.AppendLine($"Details={SanitizeForLog(details)}");
            builder.AppendLine("---");
            File.AppendAllText(logPath, builder.ToString());
        }
        catch (Exception logWriteException)
        {
            throw new IOException("The reset evidence log could not be written for the reset operation.", logWriteException);
        }
    }

    private static string ResolveResetArtifactPath(string scriptPath, string fileName)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrWhiteSpace(scriptDirectory))
        {
            var resolved = Path.Combine(scriptDirectory, fileName);
            if (File.Exists(resolved))
            {
                return resolved;
            }
        }

        var fallback = Path.Combine("D:\\YASIN\\Backend", fileName);
        return File.Exists(fallback) ? fallback : fallback;
    }

    private static string TruncateForSummary(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var clean = SanitizeForLog(content).Replace("\r", " ").Replace("\n", " ");
        return clean.Length <= 800 ? clean : clean.Substring(0, 800) + "...";
    }

    private static (bool IsValid, long OperationalRowsAfter, string? Message) TryParseVerifyContract(string stdout, string stderr, int exitCode)
    {
        if (exitCode != 0)
        {
            return (false, -1, "Verify exited with a non-zero code.");
        }

        var text = (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, -1, "Verify output was empty.");
        }

        var lines = text.Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) { continue; }
            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();
            values[key] = value;
        }

        var verifyResult = values.TryGetValue("VERIFY_RESULT", out var vr) ? vr : string.Empty;
        var operationalRowsAfter = values.TryGetValue("OPERATIONAL_ROWS_AFTER", out var rows) ? ParseLong(rows) : -1;
        var nonZeroOperational = values.TryGetValue("NONZERO_OPERATIONAL_TABLES_AFTER", out var tables) ? tables : string.Empty;
        var protectedRowsPreserved = values.TryGetValue("PROTECTED_ROWS_PRESERVED", out var protectedRows) ? protectedRows : string.Empty;
        var orphanCheck = values.TryGetValue("ORPHAN_CHECK", out var orphan) ? orphan : string.Empty;
        var financialIntegrity = values.TryGetValue("FINANCIAL_INTEGRITY", out var financial) ? financial : string.Empty;

        if (!string.Equals(verifyResult, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            return (false, operationalRowsAfter, $"VERIFY_RESULT was {verifyResult ?? "<missing>"}.");
        }

        if (operationalRowsAfter != 0)
        {
            return (false, operationalRowsAfter, $"Operational rows after cleanup remained at {operationalRowsAfter}.");
        }

        if (!string.Equals(nonZeroOperational, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            return (false, operationalRowsAfter, $"NONZERO_OPERATIONAL_TABLES_AFTER was {nonZeroOperational}.");
        }

        if (!string.Equals(protectedRowsPreserved, "PASS", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(orphanCheck, "PASS", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(financialIntegrity, "PASS", StringComparison.OrdinalIgnoreCase))
        {
            return (false, operationalRowsAfter, "One or more required verification checks failed.");
        }

        return (true, 0, "Verify contract satisfied.");
    }

    private static long ParseLong(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        if (long.TryParse(value.Trim(), out var parsed))
        {
            return parsed;
        }

        var match = Regex.Match(value, @"-?\d+");
        return match.Success ? long.Parse(match.Value) : -1;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var sanitized = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
    }

    private sealed record ResetScriptExecutionResult(
        bool Success,
        int ExitCode,
        string ScriptPath,
        string WorkingDirectory,
        string ManifestPath,
        string DependencyGraphPath,
        string StdOut,
        string StdErr,
        string FailureStage,
        string FailureCode,
        string CorrelationId,
        DateTimeOffset TimestampUtc);

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value
            .Replace("Authorization", "[redacted]")
            .Replace("Password", "[redacted]")
            .Replace("Token", "[redacted]")
            .Replace("ConnectionString", "[redacted]");
    }

    private static string ResolveResetScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine(Environment.CurrentDirectory, "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine("D:\\YASIN\\Backend", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1"),
            Path.Combine("D:", "YASIN", "Backend", "R3_SAFE_ZERO_OPERATIONAL_RESET.ps1")
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
            {
                return resolved;
            }
        }

        return string.Empty;
    }

    private static bool HasValidProviderSetup(WhatsAppOptions options)
        => options.Enabled && !string.IsNullOrWhiteSpace(options.PhoneNumberId) && !string.IsNullOrWhiteSpace(options.AccessToken);

    private static string BuildWhatsAppStatusMessage(bool enabled, string? phoneNumberId, string? accessToken)
    {
        if (!enabled)
        {
            return "تم إيقاف مزود واتساب مؤقتاً، ولا يتم إرسال الرسائل حتى يتم تفعيله.";
        }

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return "تم التحقق من اكتمال إعدادات الاتصال، ولم يتم إدخال رمز الوصول النهائي بعد.";
        }

        return "تم التحقق من اكتمال إعدادات الاتصال.";
    }

    private static string NormalizeText(string? incoming, string? current, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return string.IsNullOrWhiteSpace(current) ? defaultValue : current.Trim();
        }

        return incoming.Trim();
    }

    private static string NormalizeOptionalText(string? incoming, string? current)
    {
        if (incoming is null)
        {
            return current ?? string.Empty;
        }

        return incoming.Trim();
    }

    private static string NormalizeOptionalSecret(string? incoming, string? current)
    {
        if (incoming is null)
        {
            return current ?? string.Empty;
        }

        var value = incoming.Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return value;
    }
}