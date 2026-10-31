using System.Diagnostics;
using System.Runtime.Versioning;

namespace LUMAR_ERP_API_V2.Services;

[SupportedOSPlatform("windows")]
public sealed class LocalWhatsAppGatewayProcessHost(
    ILogger<LocalWhatsAppGatewayProcessHost> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private Process? _process;
    private string _gatewayDirectory = string.Empty;
    private string _gatewayScript = string.Empty;
    private string _approvedTestRecipient = string.Empty;
    private bool _enabled;
    private int _port;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureRuntimeConfiguration();

        _enabled = configuration.GetValue("LocalWhatsAppGateway:AutoStart", true)
            && OperatingSystem.IsWindows();

        if (!_enabled)
        {
            logger.LogInformation("Local WhatsApp gateway auto-start is disabled.");
            return;
        }

        if (_port <= 0 || string.IsNullOrWhiteSpace(_gatewayDirectory))
        {
            logger.LogWarning("Local WhatsApp gateway runtime configuration is invalid; skipping auto-start.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_approvedTestRecipient))
        {
            logger.LogInformation("Local WhatsApp gateway approved recipient configured: {MaskedRecipient}", MaskPhone(_approvedTestRecipient));
        }

        if (!File.Exists(_gatewayScript))
        {
            logger.LogWarning("Local WhatsApp gateway script not found at {ScriptPath}; skipping auto-start.", _gatewayScript);
            return;
        }

        await EnsureRunningAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    private void EnsureRuntimeConfiguration()
    {
        if (_port > 0 && !string.IsNullOrWhiteSpace(_gatewayDirectory))
        {
            return;
        }

        var baseUrl = configuration["LocalWhatsAppGateway:BaseUrl"] ?? "http://127.0.0.1:5094/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp || uri.Host != "127.0.0.1")
        {
            logger.LogWarning("Local WhatsApp gateway base URL is invalid; skipping auto-start. Value={BaseUrl}", baseUrl);
            return;
        }

        _port = uri.Port <= 0 ? 5094 : uri.Port;
        _approvedTestRecipient = configuration["LocalWhatsAppGateway:ApprovedTestRecipient"]
            ?? configuration["LocalWhatsAppGateway:TestRecipient"]
            ?? configuration["LUMAR_LOCAL_TEST_RECIPIENT"]
            ?? string.Empty;
        _gatewayDirectory = FindGatewayDirectory();
        _gatewayScript = Path.Combine(_gatewayDirectory, "src", "server.mjs");
    }

    private string FindGatewayDirectory()
    {
        var roots = new List<string>
        {
            Environment.GetEnvironmentVariable("LUMAR_LOCAL_WHATSAPP_ROOT") ?? string.Empty,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos", "YASIN"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "YASIN"),
            Path.Combine("D:\\", "YASIN"),
            Path.Combine("C:\\", "YASIN")
        };

        foreach (var startingPoint in roots.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            if (TryFindGatewayDirectory(startingPoint, out var directory))
            {
                return directory;
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos", "YASIN", "Gateway", "LocalWhatsApp");
    }

    private static bool TryFindGatewayDirectory(string startingPoint, out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(startingPoint))
        {
            return false;
        }

        var probe = new DirectoryInfo(startingPoint);
        if (!probe.Exists)
        {
            var parent = Directory.GetParent(startingPoint);
            if (parent is null || !parent.Exists)
            {
                return false;
            }

            probe = parent;
        }

        for (var current = probe; current is not null; current = current.Parent)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(current.FullName, "Gateway", "LocalWhatsApp"),
                         Path.Combine(current.FullName, "LocalWhatsApp"),
                         Path.Combine(current.FullName, "YASIN", "Gateway", "LocalWhatsApp"),
                         Path.Combine(current.FullName, "YASIN", "LocalWhatsApp")
                     })
            {
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "src", "server.mjs")))
                {
                    directory = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            await EnsureRunningAsync(stoppingToken);
        }
    }

    private async Task EnsureRunningAsync(CancellationToken cancellationToken)
    {
        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        return;
                    }
                }
                catch
                {
                    _process = null;
                }

                if (_process is not null)
                {
                    try { _process.Dispose(); }
                    catch { }
                    _process = null;
                }
            }

            var existing = TryAttachToExistingGatewayProcessOnPort();
            if (existing is not null)
            {
                _process = existing;
                if (await WaitForGatewayHealthAsync())
                {
                    logger.LogInformation("Reused Local WhatsApp gateway process PID {Pid} on port {Port}.", _process.Id, _port);
                    return;
                }

                logger.LogWarning("Detected a Local WhatsApp gateway process PID {Pid} on port {Port}, but it did not report healthy state. Cleaning it before starting a fresh instance.", existing.Id, _port);
                try { if (!existing.HasExited) existing.Kill(entireProcessTree: true); }
                catch { }
                try { existing.Dispose(); }
                catch { }
                _process = null;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                using var response = await client.GetAsync($"http://127.0.0.1:{_port}/health", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch
            {
                // fall through to launch the gateway when it is not yet reachable.
            }

            var staleRemoved = TryStopStaleGatewayProcessOnPort();
            if (staleRemoved)
            {
                logger.LogInformation("Removed a stale Local WhatsApp gateway instance from port {Port} before startup.", _port);
            }

            Process? candidate = null;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                candidate = StartGatewayProcess();
                if (candidate is null)
                {
                    logger.LogWarning("Local WhatsApp gateway process could not be started.");
                    return;
                }

                _process = candidate;
                logger.LogInformation("Started Local WhatsApp gateway process with PID {Pid} on port {Port} (attempt {Attempt}).", candidate.Id, _port, attempt);

                var ready = await WaitForGatewayHealthAsync();
                if (ready)
                {
                    _process.OutputDataReceived += (_, args) =>
                    {
                        if (string.IsNullOrWhiteSpace(args.Data)) return;
                        logger.LogInformation("LOCAL_WA_SAFE_OBSERVABILITY stdout {Output}", args.Data.Trim());
                    };
                    _process.ErrorDataReceived += (_, args) =>
                    {
                        if (string.IsNullOrWhiteSpace(args.Data)) return;
                        logger.LogWarning("LOCAL_WA_SAFE_OBSERVABILITY stderr {Output}", args.Data.Trim());
                    };
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    return;
                }

                var exitCode = TryGetExitCode(candidate);
                logger.LogWarning("Local WhatsApp gateway did not become healthy on port {Port} after attempt {Attempt}. ExitCode={ExitCode}. Cleaning stale process and retrying once.", _port, attempt, exitCode);
                TryStopStaleGatewayProcessOnPort();
                try { if (!candidate.HasExited) candidate.Kill(entireProcessTree: true); }
                catch { }
                try { candidate.Dispose(); }
                catch { }
                _process = null;
                candidate = null;

                if (attempt == 1)
                {
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                return;
            }
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    private Process? StartGatewayProcess()
    {
        EnsureRuntimeConfiguration();
        var gatewayScript = Path.Combine(_gatewayDirectory, "src", "server.mjs");
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{gatewayScript}\"",
            WorkingDirectory = _gatewayDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["LUMAR_LOCAL_GATEWAY_PORT"] = _port.ToString();
        psi.Environment["LUMAR_LOCAL_TEST_RECIPIENT"] = _approvedTestRecipient;
        psi.Environment["LUMAR_LOCAL_MEDIA_ORIGIN"] = ResolveMediaOrigin(configuration);

        return Process.Start(psi);
    }

    private async Task<bool> WaitForGatewayHealthAsync()
    {
        EnsureRuntimeConfiguration();
        using var readinessClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await readinessClient.GetAsync($"http://127.0.0.1:{_port}/health");
                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return true;
                }
            }
            catch
            {
                await Task.Delay(200);
            }
        }

        return false;
    }

    private Process? TryAttachToExistingGatewayProcessOnPort()
    {
        EnsureRuntimeConfiguration();
        if (!OperatingSystem.IsWindows() || _port <= 0 || string.IsNullOrWhiteSpace(_gatewayScript))
        {
            return null;
        }

        var expectedScript = _gatewayScript.Replace("'", "''");
        var script = "$ids = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'node' -and $_.CommandLine -and (" + Environment.NewLine +
            "  $_.CommandLine -match [regex]::Escape('" + expectedScript + "') -or " + Environment.NewLine +
            "  ($_.CommandLine -match [regex]::Escape('LocalWhatsApp') -and $_.CommandLine -match 'server\\.mjs')" + Environment.NewLine +
            ") } | Select-Object -ExpandProperty ProcessId);" + Environment.NewLine +
            "if (-not $ids) { exit 0 }" + Environment.NewLine +
            "$ids | Sort-Object -Unique | Select-Object -First 1;";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!process.Start())
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(error))
            {
                logger.LogDebug("Gateway attachment check for port {Port} reported errors: {Error}", _port, error.Trim());
            }

            if (!int.TryParse(output.Trim(), out var pid) || pid <= 0)
            {
                return null;
            }

            try
            {
                var existing = Process.GetProcessById(pid);
                return !existing.HasExited ? existing : null;
            }
            catch
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to attach to an existing Local WhatsApp gateway on port {Port}.", _port);
            return null;
        }
    }

    private bool TryStopStaleGatewayProcessOnPort()
    {
        EnsureRuntimeConfiguration();
        if (!OperatingSystem.IsWindows() || _port <= 0 || string.IsNullOrWhiteSpace(_gatewayScript))
        {
            return false;
        }

        var expectedScript = _gatewayScript.Replace("'", "''");
        var script = "$port = " + _port + ";" + Environment.NewLine +
            "$portOwners = @(Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique);" + Environment.NewLine +
            "$matchingPids = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessId -in $portOwners -and $_.CommandLine -and (" + Environment.NewLine +
            "  $_.CommandLine -match [regex]::Escape('" + expectedScript + "') -or " + Environment.NewLine +
            "  ($_.CommandLine -match [regex]::Escape('LocalWhatsApp') -and $_.CommandLine -match 'server\\.mjs')" + Environment.NewLine +
            ") } | Select-Object -ExpandProperty ProcessId);" + Environment.NewLine +
            "if (-not $matchingPids) { Write-Output 'NO_MATCH'; exit 0 }" + Environment.NewLine +
            "$unique = $matchingPids | Sort-Object -Unique;" + Environment.NewLine +
            "foreach ($candidatePid in $unique) { Stop-Process -Id $candidatePid -Force -ErrorAction SilentlyContinue; Write-Output \"KILL:$candidatePid\" }";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!process.Start())
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrWhiteSpace(error))
            {
                logger.LogDebug("Stale gateway cleanup reported errors for port {Port}: {Error}", _port, error.Trim());
            }

            return output.Contains("KILL:", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to clean stale Local WhatsApp gateway processes on port {Port}.", _port);
            return false;
        }
    }

    public static string ResolveMediaOrigin(IConfiguration configuration)
    {
        var configured = configuration["CampaignMedia:PublicBaseUrl"]
            ?? configuration["LUMAR_CAMPAIGN_MEDIA_PUBLIC_BASE_URL"]
            ?? configuration["LUMAR_PUBLIC_BACKEND_BASE_URL"]
            ?? Environment.GetEnvironmentVariable("LUMAR_CAMPAIGN_MEDIA_PUBLIC_BASE_URL")
            ?? Environment.GetEnvironmentVariable("LUMAR_PUBLIC_BACKEND_BASE_URL")
            ?? configuration["LUMAR_LOCAL_MEDIA_ORIGIN"]
            ?? Environment.GetEnvironmentVariable("LUMAR_LOCAL_MEDIA_ORIGIN")
            ?? string.Empty;

        return string.IsNullOrWhiteSpace(configured) ? string.Empty : configured.TrimEnd('/');
    }

    private static string MaskPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return new string('*', Math.Max(4, digits.Length));
        return $"****{digits[^4..]}";
    }

    private static int TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Process? processToStop = null;

        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            processToStop = _process;
            _process = null;
        }
        finally
        {
            _ensureLock.Release();
        }

        if (processToStop is not null)
        {
            try
            {
                if (processToStop.HasExited)
                {
                    return;
                }

                processToStop.Kill(entireProcessTree: true);
                await processToStop.WaitForExitAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                logger.LogDebug("Local WhatsApp gateway process was already detached or cleaned up.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Local WhatsApp gateway did not stop gracefully.");
            }
            finally
            {
                try { processToStop.Dispose(); }
                catch { }
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
