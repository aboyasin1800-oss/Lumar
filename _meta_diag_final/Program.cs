using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddOptions<WhatsAppOptions>().Configure(_ => { });
services.AddDataProtection().SetApplicationName("LUMAR_ERP_API_V2").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")));
services.AddSingleton<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();
using var provider = services.BuildServiceProvider();
var runtime = await provider.GetRequiredService<IWhatsAppConnectionSettingsStore>().GetRuntimeSettingsAsync(CancellationToken.None);
Console.WriteLine($"RUNTIME_SETTINGS_LOADED={(runtime is not null ? "YES" : "NO")}");
Console.WriteLine($"RUNTIME_ENABLED={(runtime.Enabled ? "YES" : "NO")}");
Console.WriteLine($"PHONE_PRESENT={(!string.IsNullOrWhiteSpace(runtime.PhoneNumberId) ? "YES" : "NO")}");
Console.WriteLine($"TOKEN_PRESENT={(!string.IsNullOrWhiteSpace(runtime.AccessToken) ? "YES" : "NO")}");
Console.WriteLine($"TOKEN_LENGTH={runtime.AccessToken?.Length ?? 0}");
Console.WriteLine($"BASE_URL={runtime.BaseUrl}");
Console.WriteLine($"API_VERSION={runtime.ApiVersion}");
var uri = $"{runtime.BaseUrl.TrimEnd('/')}/{runtime.ApiVersion.Trim('/')}/1413441637074832/message_templates?fields=id,name,status,language,category,components";
Console.WriteLine($"META_GET_URI={uri}");
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, uri);
request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runtime.AccessToken);
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
var raw = await response.Content.ReadAsStringAsync();
Console.WriteLine($"HTTP_STATUS={(int)response.StatusCode} {response.StatusCode}");
if (!response.IsSuccessStatusCode)
{
    Console.WriteLine("SAFE_ERROR_TYPE=META_HTTP_ERROR");
    Console.WriteLine("SAFE_ERROR_DETAILS=" + (raw.Length > 500 ? raw.Substring(0,500) : raw));
    return;
}
using var doc = JsonDocument.Parse(raw);
var names = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
    ? data.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
    : Array.Empty<string>();
Console.WriteLine($"TEMPLATE_COUNT={names.Length}");
for (var i = 0; i < names.Length; i++) Console.WriteLine($"TEMPLATE_NAME_{i + 1}={names[i]}");
if (names.Length == 0)
{
    Console.WriteLine("RAW_BODY_SHORT=" + (raw.Length > 500 ? raw.Substring(0,500) : raw));
}
