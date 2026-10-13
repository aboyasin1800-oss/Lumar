using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            new FixedHttpClientFactory(new HttpClient(new StubHandler("{}"))),
            Options.Create(new WhatsAppOptions { Enabled = false, BaseUrl = "https://graph.facebook.com", ApiVersion = "v20.0", PhoneNumberId = "123", AccessToken = "token" }));

        var result = await channel.SendAsync(new CustomerMessageDto(
            7, 17, 42, "OrderCreated", "OrderCreated", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "966500000000", "ORD-42", "Order", "key"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.PermanentFailure, result.Outcome);
        Assert.Contains("disabled", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhatsAppCloudMessageChannel_SuccessfulProviderResponse_UsesProviderMessageId()
    {
        var handler = new StubHandler("{\"messages\":[{\"id\":\"wamid.123456789\"}]}");
        var channel = new WhatsAppCloudMessageChannel(
            new FixedHttpClientFactory(new HttpClient(handler)),
            Options.Create(new WhatsAppOptions
            {
                Enabled = true,
                BaseUrl = "https://graph.facebook.com",
                ApiVersion = "v20.0",
                PhoneNumberId = "123456",
                AccessToken = "token-value",
                VerifyToken = "verify-token",
                AppSecret = "secret"
            }));

        var result = await channel.SendAsync(new CustomerMessageDto(
            8, 18, 44, "OrderReady", "OrderReady", "WhatsApp", "Title", "Body", "Pending", DateTime.UtcNow, null, null, 0, null, null, null, null, "+966500000001", "ORD-44", "Order", "key-2"), CancellationToken.None);

        Assert.Equal(OutboundDispatchOutcome.Sent, result.Outcome);
        Assert.Equal("wamid.123456789", result.ProviderMessageId);
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

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
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
        public Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int customerMessageId, CancellationToken c) => Task.FromResult(0);
    }
}
