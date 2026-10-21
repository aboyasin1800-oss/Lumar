using System.Net;
using System.Text;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Push;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FirebasePushSenderTests
{
    [Fact]
    public async Task SendAsync_WhenServiceAccountIsConfigured_UsesFcmApiAndReturnsSuccess()
    {
        var credentialPath = Path.Combine(Path.GetTempPath(), $"lumar-fcm-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(credentialPath, """
{
  "type": "service_account",
  "project_id": "demo-project-id",
  "private_key_id": "demo-key-id",
  "private_key": "-----BEGIN PRIVATE KEY-----\\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDLh8n8k9rX8H5mK4mQK3s/9h9mVwPqRZf7mXhM=\\n-----END PRIVATE KEY-----\\n",
  "client_email": "firebase-adminsdk@demo-project-id.iam.gserviceaccount.com",
  "client_id": "1234567890",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token",
  "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
  "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk%40demo-project-id.iam.gserviceaccount.com"
}
""");

        try
        {
            var handler = new FakeHttpMessageHandler((request, cancellationToken) =>
            {
                Assert.Equal("https://fcm.googleapis.com/v1/projects/demo-project-id/messages:send", request.RequestUri!.ToString());
                Assert.NotNull(request.Headers.Authorization);
                Assert.StartsWith("Bearer ", request.Headers.Authorization!.Scheme + " " + request.Headers.Authorization.Parameter);

                var body = request.Content is null ? string.Empty : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                Assert.Contains("\"token\":\"token-abc\"", body, StringComparison.Ordinal);
                Assert.Contains("\"title\":\"Hello\"", body, StringComparison.Ordinal);
                Assert.Contains("\"body\":\"World\"", body, StringComparison.Ordinal);
                Assert.Contains("\"referenceType\":\"CustomerNotification\"", body, StringComparison.Ordinal);
                Assert.Contains("\"referenceId\":\"C-123\"", body, StringComparison.Ordinal);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"name\":\"projects/demo-project-id/messages/abc123\"}")
                };
            });

            var sender = new FirebasePushSender(
                Options.Create(new FirebasePushOptions
                {
                    Enabled = true,
                    ProjectId = "demo-project-id",
                    ServiceAccountJsonPath = credentialPath
                }),
                new HttpClient(handler),
                NullLogger<FirebasePushSender>.Instance,
                _ => Task.FromResult("fake-access-token"));

            var outcome = await sender.SendAsync(
                new PushDeviceTokenRecord(1, 42, "FCM", "Android", "device-1", "cipher", "hash", "token-abc", true, false, DateTime.UtcNow, null, null),
                "Hello",
                "World",
                "CustomerNotification",
                "C-123",
                CancellationToken.None);

            Assert.True(outcome.Success);
            Assert.Equal("projects/demo-project-id/messages/abc123", outcome.ProviderMessageId);
            Assert.Equal("Success", outcome.Status);
        }
        finally
        {
            if (File.Exists(credentialPath)) File.Delete(credentialPath);
        }
    }

    [Fact]
    public async Task SendAsync_WhenCredentialsAreMissing_ReturnsNotConfigured()
    {
        var sender = new FirebasePushSender(
            Options.Create(new FirebasePushOptions { Enabled = true, ProjectId = string.Empty, ServiceAccountJsonPath = string.Empty }),
            new HttpClient(new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            NullLogger<FirebasePushSender>.Instance,
            _ => Task.FromResult("fake-access-token"));

        var outcome = await sender.SendAsync(
            new PushDeviceTokenRecord(1, 42, "FCM", "Android", "device-1", "cipher", "hash", "token-abc", true, false, DateTime.UtcNow, null, null),
            "Hi",
            "Body",
            "CustomerNotification",
            "C-1",
            CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("NotConfigured", outcome.Status);
        Assert.Contains("credentials", outcome.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(action(request, cancellationToken));
    }
}
