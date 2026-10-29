using System.Text;

namespace LUMAR_ERP_API_V2.Monitoring;

public sealed class SafeRequestCaptureMiddleware(RequestDelegate next)
{
    private const int LogWriteTimeoutMs = 5000;
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LUMAR_ERP_API_V2");

    private static readonly string LogPath = Path.Combine(LogDirectory, "safe-live-monitor.log");
    private static readonly string LogMutexName = OperatingSystem.IsWindows()
        ? "Global\\LUMAR_ERP_API_V2_SafeRequestCapture"
        : "LUMAR_ERP_API_V2_SafeRequestCapture";
    private static readonly Mutex LogWriteMutex = new(false, LogMutexName);

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var traceId = context.TraceIdentifier;

        await next(context);

        if (!IsMonitoredPath(path))
        {
            return;
        }

        var mobileAccountId = GetSafeValue(context.Items, "LiveMonitor.MobileAccountId");
        var employeeId = GetSafeValue(context.Items, "LiveMonitor.EmployeeId");
        var trackingCode = GetSafeValue(context.Items, "LiveMonitor.TrackingCode");
        var requestedStage = GetSafeValue(context.Items, "LiveMonitor.RequestedStage");
        var authResult = GetSafeValue(context.Items, "LiveMonitor.AuthResult");
        var statusCode = context.Response.StatusCode;
        var durationMs = (DateTimeOffset.UtcNow - startedAtUtc).TotalMilliseconds;

        var builder = new StringBuilder();
        builder.Append(startedAtUtc.ToString("O"));
        builder.Append("|METHOD=");
        builder.Append(method);
        builder.Append("|PATH=");
        builder.Append(path);
        builder.Append("|STATUS=");
        builder.Append(statusCode);
        builder.Append("|TRACE=");
        builder.Append(traceId);
        builder.Append("|MobileAccountId=");
        builder.Append(mobileAccountId);
        builder.Append("|EmployeeId=");
        builder.Append(employeeId);
        builder.Append("|TrackingCode=");
        builder.Append(trackingCode);
        builder.Append("|RequestedStage=");
        builder.Append(requestedStage);
        builder.Append("|AuthResult=");
        builder.Append(authResult);
        builder.Append("|DurationMs=");
        builder.Append(durationMs.ToString("0.##"));
        builder.AppendLine();

        try
        {
            await WriteAuditLineAsync(builder.ToString());
        }
        catch
        {
            // Fail-safe: diagnostics must never break the active request or alter business results.
        }
    }

    private static async Task WriteAuditLineAsync(string content)
    {
        var acquired = false;
        try
        {
            acquired = LogWriteMutex.WaitOne(TimeSpan.FromMilliseconds(LogWriteTimeoutMs));
            if (!acquired)
            {
                return;
            }

            Directory.CreateDirectory(LogDirectory);

            await using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);
            await writer.FlushAsync();
        }
        finally
        {
            if (acquired)
            {
                LogWriteMutex.ReleaseMutex();
            }
        }
    }

    private static bool IsMonitoredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.StartsWith("/production", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeValue(IDictionary<object, object?> items, string key)
    {
        if (items.TryGetValue(key, out var value))
        {
            return value is null ? "N/A" : value.ToString() ?? "N/A";
        }

        return "N/A";
    }
}
