using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class WhatsAppCloudIntegrationTests
{
    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenDisabled_DoesNotFakeSuccess()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = false,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123",
                AccessToken = "token"
            }));

        var result = await channel.SendAsync(new CustomerMessageDto(
            7, 17, 42, "OrderCreated", "OrderCreated", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "966500000000", "ORD-42", "Order", "key"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("disabled", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_SuccessfulProviderResponse_UsesProviderMessageId()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"messages\":[{\"id\":\"wamid.123456789\"}]}");
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value",
                VerifyToken = "verify-token",
                AppSecret = "secret"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            8, 18, 44, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000001", "ORD-44", "Order", "key-2"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal("wamid.123456789", result.ProviderMessageId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaReturns200WithoutMessageId_FailsSafely()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            9, 19, 45, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000002", "ORD-45", "Order", "key-3"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Contains("Meta HTTP 200", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaRejectsWithErrorDetails_RecordsSafeMetadata()
    {
        var response = "{\"error\":{\"type\":\"OAuthException\",\"code\":100,\"error_subcode\":400010,\"message\":\"Auth error: AccessToken=SECRET_TOKEN_123 Authorization: Bearer SECRET_BEARER\"}}";
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.BadRequest, response))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            10, 20, 46, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000003", "ORD-46", "Order", "key-4"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(100, result.ProviderErrorCode);
        Assert.Equal(400010, result.ProviderErrorSubcode);
        Assert.Equal("OAuthException", result.SafeProviderErrorType);
        Assert.DoesNotContain("SECRET_TOKEN_123", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_WhenMetaRejectsWith429_ReportsTransientFailure()
    {
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, "{\"error\":{\"type\":\"RateLimit\",\"code\":190,\"message\":\"Rate limit exceeded\"}}"))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            NullLogger<WhatsAppCloudMessageChannel>.Instance);

        var result = await channel.SendAsync(new CustomerMessageDto(
            11, 21, 47, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000004", "ORD-47", "Order", "key-5"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.TransientFailure, result.Outcome);
        Assert.Equal(429, result.HttpStatusCode);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_SanitizesAccessTokenAndAuthorizationTokensAcrossFailureReasonAndLogger()
    {
        var logger = new CaptureLogger<WhatsAppCloudMessageChannel>();
        var response = "{\"error\":{\"type\":\"OAuthException\",\"code\":100,\"error_subcode\":400010,\"message\":\"AccessToken=SECRET_TOKEN_123 Authorization: Bearer SECRET_BEARER AppSecret=APP_SECRET_VALUE VerifyToken: VERIFY_SECRET_VALUE\"}}";
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(new StubHandler(HttpStatusCode.BadRequest, response))),
            new FakeWhatsAppConnectionSettingsStore(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value"
            }),
            logger);

        var result = await channel.SendAsync(new CustomerMessageDto(
            12, 22, 48, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000005", "ORD-48", "Order", "key-6"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(100, result.ProviderErrorCode);
        Assert.Equal(400010, result.ProviderErrorSubcode);
        Assert.Equal("OAuthException", result.SafeProviderErrorType);
        Assert.True(result.FailureReason.Length <= 500, $"FailureReason length exceeded safe cap: {result.FailureReason.Length}");
        Assert.DoesNotContain("SECRET_TOKEN_123", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("APP_SECRET_VALUE", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("VERIFY_SECRET_VALUE", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", result.FailureReason, StringComparison.OrdinalIgnoreCase);

        var logText = string.Join(" ", logger.Messages);
        Assert.DoesNotContain("SECRET_TOKEN_123", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_BEARER", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("APP_SECRET_VALUE", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("VERIFY_SECRET_VALUE", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", logText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token-value", logText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhatsAppWebhookController_ValidVerificationChallenge_ReturnsChallenge()
    {
        var options = Options.Create(new WhatsAppOptions { Enabled = true, VerifyToken = "valid-token" });
        var controller = new WhatsAppWebhookController(new StubMessageRepository(), options, NullLogger<WhatsAppWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = controller.VerifyWebhook("subscribe", "challenge-abc", "valid-token");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("challenge-abc", content.Content);
    }

    private sealed class FakeWhatsAppConnectionSettingsStore(WhatsAppOptions options) : IWhatsAppConnectionSettingsStore
    {
        public Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppConnectionStatusDto(
                Enabled: options.Enabled,
                PhoneNumberId: options.PhoneNumberId,
                TokenConfigured: !string.IsNullOrWhiteSpace(options.AccessToken),
                ConnectionConfigured: options.Enabled && !string.IsNullOrWhiteSpace(options.PhoneNumberId) && !string.IsNullOrWhiteSpace(options.AccessToken),
                Message: "test"));

        public Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppRuntimeSettings(
                Enabled: options.Enabled,
                BaseUrl: string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://graph.facebook.com" : options.BaseUrl,
                ApiVersion: string.IsNullOrWhiteSpace(options.ApiVersion) ? "v20.0" : options.ApiVersion,
                PhoneNumberId: options.PhoneNumberId,
                AccessToken: options.AccessToken,
                VerifyToken: options.VerifyToken,
                AppSecret: options.AppSecret,
                TimeoutSeconds: options.TimeoutSeconds));

        public Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken)
        {
            options.Enabled = request.Enabled;
            if (!string.IsNullOrWhiteSpace(request.PhoneNumberId)) options.PhoneNumberId = request.PhoneNumberId;
            if (!string.IsNullOrWhiteSpace(request.AccessToken)) options.AccessToken = request.AccessToken;
            if (!string.IsNullOrWhiteSpace(request.VerifyToken)) options.VerifyToken = request.VerifyToken;
            if (!string.IsNullOrWhiteSpace(request.AppSecret)) options.AppSecret = request.AppSecret;
            if (!string.IsNullOrWhiteSpace(request.BaseUrl)) options.BaseUrl = request.BaseUrl;
            if (!string.IsNullOrWhiteSpace(request.ApiVersion)) options.ApiVersion = request.ApiVersion;
            if (request.TimeoutSeconds > 0) options.TimeoutSeconds = request.TimeoutSeconds;

            return GetStatusAsync(cancellationToken);
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StubHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubMessageRepository : IMessageRepository
    {
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Array.Empty<CustomerMessageDto>());
        public Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c) => Task.FromResult<CustomerMessageDto?>(null);
        public Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerMessageDto>>(Array.Empty<CustomerMessageDto>());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Array.Empty<CustomerNotificationDto>());
        public Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c) => Task.FromResult<IReadOnlyList<CustomerNotificationDto>>(Array.Empty<CustomerNotificationDto>());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(Array.Empty<MessageTemplateDto>());
        public Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c) => Task.FromResult<IReadOnlyList<MessageTemplateDto>>(Array.Empty<MessageTemplateDto>());
        public Task<MessageTemplateDto?> CreateTemplateAsync(CreateMessageTemplateDto request, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> UpdateTemplateAsync(int id, UpdateMessageTemplateDto request, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> ActivateTemplateAsync(int id, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<MessageTemplateDto?> DeactivateTemplateAsync(int id, CancellationToken c) => Task.FromResult<MessageTemplateDto?>(null);
        public Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentMessageDto>>(Array.Empty<SentMessageDto>());
        public Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<SentLogDto>>(Array.Empty<SentLogDto>());
        public Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c) => Task.FromResult<MessageTemplateRecord?>(null);
        public Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c) => Task.FromResult(false);
        public Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken) => Task.FromResult<CustomerMessageDto?>(null);
        public Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<(int? MessageId, int? NotificationId)>((null, null));
        public Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c) => Task.FromResult<int?>(null);
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c) => Task.FromResult(0);
    }
}
