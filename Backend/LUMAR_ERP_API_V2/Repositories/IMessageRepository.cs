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
    Task<IReadOnlyList<SentMessageDto>> GetSentAsync(CancellationToken c);
    Task<IReadOnlyList<SentLogDto>> GetSentLogAsync(CancellationToken c);
    Task<MessageTemplateRecord?> GetActiveTemplateAsync(string templateKey, CancellationToken c);
    Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken c);
    Task<(int? MessageId, int? NotificationId)> CreateCustomerMessageAndNotificationAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c);
    Task<int?> CreateCustomerMessageAsync(int customerId, int? orderId, string eventType, string templateKey, string channel, string title, string body, string phoneNumber, string referenceId, string referenceType, string idempotencyKey, CancellationToken c);
    Task<int> CreateCustomerNotificationAsync(int customerId, int? orderId, string eventType, string title, string body, int customerMessageId, CancellationToken c);
}