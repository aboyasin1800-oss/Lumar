using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

Console.WriteLine("START");
var securePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "com.example", "tailoring_system", "flutter_secure_storage.dat");
Console.WriteLine($"SECURE_PATH={securePath}");
var bytes = File.ReadAllBytes(securePath);
Console.WriteLine($"BYTES={bytes.Length}");
var plain = Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
Console.WriteLine("DECRYPTED");
var obj = JsonDocument.Parse(plain).RootElement;
if (!obj.TryGetProperty("lumar_auth_token", out var tokenProp)) throw new Exception("NO_AUTH_TOKEN_FOUND");
var token = tokenProp.GetString();
Console.WriteLine($"TOKEN_LEN={token?.Length ?? 0}");
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
Console.WriteLine("REQUESTING");
try
{
    var response = await client.GetAsync("http://127.0.0.1:5093/settings/test-data-reset/readiness");
    Console.WriteLine($"HTTP_STATUS={(int)response.StatusCode}");
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"BODY={body.Substring(0, Math.Min(800, body.Length))}");
}
catch (Exception ex)
{
    Console.WriteLine($"EXCEPTION={ex.GetType().Name}");
    Console.WriteLine($"MESSAGE={ex.Message}");
}
Console.WriteLine("END");
