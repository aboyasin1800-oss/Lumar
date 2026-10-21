using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Push;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class FirebasePushSender : IPushSender
{
    private readonly FirebasePushOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FirebasePushSender>? _logger;
    private readonly Func<string, Task<string>> _accessTokenResolver;

    public FirebasePushSender(IOptions<FirebasePushOptions> options, IHttpClientFactory httpClientFactory, ILogger<FirebasePushSender>? logger = null)
        : this(options.Value, httpClientFactory.CreateClient("FirebasePushSender"), logger, ResolveAccessTokenAsync)
    {
    }

    public static FirebasePushSender CreateForTesting(
        FirebasePushOptions options,
        HttpClient httpClient,
        ILogger<FirebasePushSender>? logger = null,
        Func<string, Task<string>>? accessTokenResolver = null)
        => new FirebasePushSender(options, httpClient, logger, accessTokenResolver ?? ResolveAccessTokenAsync);

    private FirebasePushSender(FirebasePushOptions options, HttpClient httpClient, ILogger<FirebasePushSender>? logger, Func<string, Task<string>> accessTokenResolver)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
        _accessTokenResolver = accessTokenResolver;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));
    }

    public string Provider => "FCM";

    public async Task<PushSendOutcome> SendAsync(PushDeviceTokenRecord device, string title, string body, string? referenceType, string? referenceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.TokenValue))
            return new PushSendOutcome(false, null, "Token is empty or unreadable.", false, "Failed");

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ProjectId) || string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            _logger?.LogWarning("FCM is not configured in this backend environment. Device {DeviceId} token hash {TokenHash} was accepted but not sent.", device.DeviceId, device.TokenHash);
            return new PushSendOutcome(false, null, "FCM credentials are not configured in this environment. Push is backend-ready but not live-enabled.", false, "NotConfigured");
        }

        if (!File.Exists(_options.ServiceAccountJsonPath))
        {
            _logger?.LogWarning("FCM service account file was configured but missing at {ServiceAccountPath}.", _options.ServiceAccountJsonPath);
            return new PushSendOutcome(false, null, "FCM service account file is missing from the configured local secure path.", false, "NotConfigured");
        }

        try
        {
            var accessToken = await _accessTokenResolver(_options.ServiceAccountJsonPath);
            var payload = new
            {
                message = new
                {
                    token = device.TokenValue,
                    notification = new
                    {
                        title,
                        body
                    },
                    data = new Dictionary<string, string>
                    {
                        ["referenceType"] = referenceType ?? string.Empty,
                        ["referenceId"] = referenceId ?? string.Empty,
                        ["channel"] = "Push"
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.FcmSendUrl.TrimEnd('/')}/projects/{_options.ProjectId}/messages:send");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = string.IsNullOrWhiteSpace(responseBody) ? response.ReasonPhrase ?? "FCM rejected the request." : responseBody;
                _logger?.LogError("FCM send failed for device {DeviceId} with HTTP {StatusCode}: {Reason}", device.DeviceId, response.StatusCode, reason);
                return new PushSendOutcome(false, null, reason, response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout, "Failed");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var messageName = doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() : null;
            _logger?.LogInformation("FCM live push sent successfully for device {DeviceId}. ProviderMessageId={ProviderMessageId}", device.DeviceId, messageName);
            return new PushSendOutcome(true, messageName, null, false, "Success");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Live FCM send failed for device {DeviceId}.", device.DeviceId);
            return new PushSendOutcome(false, null, ex.Message, true, "Failed");
        }
    }

    private static async Task<string> ResolveAccessTokenAsync(string serviceAccountJsonPath)
    {
        var json = await File.ReadAllTextAsync(serviceAccountJsonPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var clientEmail = root.GetProperty("client_email").GetString();
        var privateKeyPem = root.GetProperty("private_key").GetString();
        var projectId = root.GetProperty("project_id").GetString();

        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKeyPem) || string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("Firebase service account JSON is missing required values.");

        var privateKey = privateKeyPem
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("-----BEGIN PRIVATE KEY-----", "-----BEGIN PRIVATE KEY-----\n", StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", "\n-----END PRIVATE KEY-----", StringComparison.Ordinal);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var now = DateTimeOffset.UtcNow;
        var headerJson = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(new
        {
            iss = clientEmail,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = "https://oauth2.googleapis.com/token",
            iat = now.AddSeconds(-30).ToUnixTimeSeconds(),
            exp = now.AddMinutes(59).ToUnixTimeSeconds()
        });

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = header + "." + payload;
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var assertion = signingInput + "." + Base64UrlEncode(signature);

        using var client = new HttpClient();
        using var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new KeyValuePair<string, string>("assertion", assertion)
        });

        using var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Unable to acquire Firebase OAuth token: {tokenBody}");

        using var tokenDocument = JsonDocument.Parse(tokenBody);
        if (!tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            throw new InvalidOperationException("Firebase OAuth response did not include access_token.");

        return accessTokenElement.GetString() ?? throw new InvalidOperationException("Firebase OAuth token was empty.");
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
