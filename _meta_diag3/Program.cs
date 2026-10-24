using System.Text.Json;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LUMAR_ERP_API_V2.Configuration;

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddOptions<WhatsAppOptions>().Configure(options => { });
services.AddDataProtection().SetApplicationName("LUMAR_ERP_API_V2").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")));
services.AddSingleton<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();
using var provider = services.BuildServiceProvider();
var runtime = await provider.GetRequiredService<IWhatsAppConnectionSettingsStore>().GetRuntimeSettingsAsync(CancellationToken.None);
var uri = $"{runtime.BaseUrl.TrimEnd('/')}/{runtime.ApiVersion.Trim('/')}/1413441637074832/message_templates?fields=id,name,status,language,category,components";
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, uri);
request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runtime.AccessToken);
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
var raw = await response.Content.ReadAsStringAsync();
Console.WriteLine($"HTTP_STATUS={(int)response.StatusCode} {response.StatusCode}");
if (string.IsNullOrWhiteSpace(raw)) { Console.WriteLine("JSON_STATUS=EMPTY"); return; }
using var doc = JsonDocument.Parse(raw);
if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
{
    var items = data.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    Console.WriteLine($"TEMPLATE_COUNT={items.Length}");
    foreach (var item in items)
    {
        Console.WriteLine($"TEMPLATE_NAME={item}");
    }
    var error = doc.RootElement.TryGetProperty("error", out var err) ? err.ToString() : "NONE";
    Console.WriteLine($"ERROR_PROP={error}");
}
else
{
    Console.WriteLine("JSON_STATUS=NO_DATA_ARRAY");
    Console.WriteLine(raw.Length > 800 ? raw.Substring(0,800) : raw);
}
