using System.Net.Http.Headers;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddOptions<WhatsAppOptions>().Configure(options => { });
services.AddDataProtection()
    .SetApplicationName("LUMAR_ERP_API_V2")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")));
services.AddSingleton<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();
using var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<IWhatsAppConnectionSettingsStore>();
var runtime = await store.GetRuntimeSettingsAsync(CancellationToken.None);
Console.WriteLine($"Runtime Settings Loaded = {(runtime != null ? "YES" : "NO")}");
Console.WriteLine($"Runtime Enabled = {(runtime.Enabled ? "YES" : "NO")}");
Console.WriteLine($"PhoneNumberId Present = {(!string.IsNullOrWhiteSpace(runtime.PhoneNumberId) ? "YES" : "NO")}");
Console.WriteLine($"AccessToken Present After Official Store = {(!string.IsNullOrWhiteSpace(runtime.AccessToken) ? "YES" : "NO")}");
Console.WriteLine($"Token Length = {runtime.AccessToken?.Length ?? 0}");
Console.WriteLine($"BaseUrl = {runtime.BaseUrl}");
Console.WriteLine($"ApiVersion = {runtime.ApiVersion}");
Console.WriteLine($"MessagingAccountId = 1413441637074832");
Console.WriteLine($"Meta GET URI = {runtime.BaseUrl.TrimEnd('/')}/{runtime.ApiVersion.Trim('/')}/1413441637074832/message_templates?fields=id,name,status,language,category,components");
Console.WriteLine("HTTP Request Actually Sent = YES");
using var client = new HttpClient();
using var request = new HttpRequestMessage(HttpMethod.Get, $"{runtime.BaseUrl.TrimEnd('/')}/{runtime.ApiVersion.Trim('/')}/1413441637074832/message_templates?fields=id,name,status,language,category,components");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", runtime.AccessToken);
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
Console.WriteLine($"HTTP Status = {(int)response.StatusCode} {response.StatusCode}");
var raw = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Response Body Parsed = {(string.IsNullOrWhiteSpace(raw) ? "NO" : "YES")}");
if (response.IsSuccessStatusCode)
{
    Console.WriteLine("Safe Error Type = NONE");
    Console.WriteLine("Safe Error Details = SUCCESS");
}
else
{
    var safe = ExtractSafeMetaError(raw);
    Console.WriteLine($"Safe Error Type = {safe ?? "UNKNOWN"}");
    Console.WriteLine($"Safe Error Details = {safe ?? "NO_ERROR_DETAIL"}");
}

static string? ExtractSafeMetaError(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;
    try
    {
        using var doc = JsonDocument.Parse(raw);
        if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : null;
            var code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
            var sub = error.TryGetProperty("error_subcode", out var s) ? s.ToString() : null;
            return string.Join(" | ", new[]{message, code != null ? $"code:{code}" : null, sub != null ? $"subcode:{sub}" : null}.Where(v => !string.IsNullOrWhiteSpace(v)));
        }
    }
    catch { }
    return raw.Length > 400 ? raw.Substring(0, 400) + "..." : raw;
}
