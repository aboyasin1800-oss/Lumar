using System.Diagnostics;
using System.Runtime.Versioning;

namespace LUMAR_ERP_API_V2.Services;

[SupportedOSPlatform("windows")]
public sealed class LocalWhatsAppGatewayProcessHost(
    ILogger<LocalWhatsAppGatewayProcessHost> logger,
    IConfiguration configuration) : BackgroundService
{
    private Process? _process;
    private string _gatewayDirectory = string.Empty;
    private string _gatewayScript = string.Empty;
    private string _approvedTestRecipient = string.Empty;
    private bool _enabled;
    private int _port;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _enabled = configuration.GetValue("LocalWhatsAppGateway:AutoStart", true)
            && OperatingSystem.IsWindows();

        if (!_enabled)
        {
            logger.LogInformation("Local WhatsApp gateway auto-start is disabled.");
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

    private string FindGatewayDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Gateway", "LocalWhatsApp", "src", "server.mjs");
            if (File.Exists(candidate))
            {
                var srcDirectory = Path.GetDirectoryName(candidate)!;
                return Path.GetDirectoryName(srcDirectory)!;
            }

            var rootCandidate = Path.Combine(current.FullName, "Gateway", "LocalWhatsApp");
            if (Directory.Exists(rootCandidate) && File.Exists(Path.Combine(rootCandidate, "src", "server.mjs")))
            {
                return rootCandidate;
            }

            current = current.Parent;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos", "YASIN", "Gateway", "LocalWhatsApp");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            return;
        }

        await EnsureRunningAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            await EnsureRunningAsync(stoppingToken);
        }
    }

    private async Task EnsureRunningAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false })
        {
            return;
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

        _process = Process.Start(psi);
        if (_process is null)
        {
            logger.LogWarning("Local WhatsApp gateway process could not be started.");
            return;
        }

        logger.LogInformation("Started Local WhatsApp gateway process with PID {Pid} on port {Port}.", _process.Id, _port);

        using var readinessClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await readinessClient.GetAsync($"http://127.0.0.1:{_port}/health");
                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    break;
                }
            }
            catch
            {
                await Task.Delay(200);
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stdout = await _process.StandardOutput.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(stdout)) logger.LogInformation("Gateway stdout: {Output}", stdout.Trim());
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                var stderr = await _process.StandardError.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(stderr)) logger.LogWarning("Gateway stderr: {Output}", stderr.Trim());
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);
    }

    private static string MaskPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return new string('*', Math.Max(4, digits.Length));
        return $"****{digits[^4..]}";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Local WhatsApp gateway did not stop gracefully.");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
