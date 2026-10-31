using System.Diagnostics;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

var port = GetFreeTcpPort();
var baseUrl = $"http://127.0.0.1:{port}/";
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["LocalWhatsAppGateway:BaseUrl"] = baseUrl,
    ["LocalWhatsAppGateway:AutoStart"] = "true",
}).Build();

var host = new LocalWhatsAppGatewayProcessHost(NullLogger<LocalWhatsAppGatewayProcessHost>.Instance, configuration);
await host.StartAsync(CancellationToken.None);

var field = typeof(LocalWhatsAppGatewayProcessHost).GetField("_process", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var process = field?.GetValue(host) as Process;
Console.WriteLine($"PROCESS_AFTER_START={process != null}");
Console.WriteLine($"PID={process?.Id}");
Console.WriteLine($"PORT={port}");

await host.StopAsync(CancellationToken.None);

static int GetFreeTcpPort()
{
    using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
}
