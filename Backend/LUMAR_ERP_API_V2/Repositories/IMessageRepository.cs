using LUMAR_ERP_API_V2.DTOs.Messages;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IMessageRepository
{
    Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(CancellationToken c);
    Task<CustomerMessageDto?> GetCustomerMessageAsync(int id, CancellationToken c);
    Task<IReadOnlyList<CustomerMessageDto>> GetCustomerMessagesAsync(int customerId, CancellationToken c);
    Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(CancellationToken c);
    Task<IReadOnlyList<CustomerNotificationDto>> GetNotificationsAsync(int customerId, CancellationToken c);
    Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesAsync(CancellationToken c);
    Task<IReadOnlyList<MessageTemplateDto>> GetTemplatesByIdAsync(int id, CancellationToken c);
    Task<MessageTemplateDto?> CreateTemplateAsync(CreateMessageTemplateDto request, CancellationToken c);
    Task<MessageTemplateDto?> UpdateTemplateAsync(int id, UpdateMessageTemplateDto request, CancellationToken c);
    Task<MessageTemplateDto?> ActivateTemplateAsync(int id, CancellationToken c);
    Task<MessageTemplateDto?> DeactivateTemplateAsync(int id, CancellationToken c);
    Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c);
    Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c);
    Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c);
    Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c);
    Task<CustomerMessageDto?> ClaimNextAsync(string channel, int maxAttempts, TimeSpan staleProcessingWindow, CancellationToken cancellationToken);
    Task<CustomerMessageDto?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken);
    Task UpdateDeliveryStatusByProviderIdAsync(string providerMessageId, string status, DateTime occurredAtUtc, string? failureReason, CancellationToken cancellationToken);
    Task MarkSentAsync(int customerMessageId, string? providerMessageId, DateTime sentAtUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(int customerMessageId, string failureReason, bool retryable, CancellationToken cancellationToken);
    Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, string? campaignImageUrl, string? rawCampaignText = null, CancellationToken c = default);
    Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c);
    Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int? customerMessageId, CancellationToken c);
}