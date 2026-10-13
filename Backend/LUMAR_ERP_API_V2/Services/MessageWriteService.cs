using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class MessageWriteService(IMessageRepository repository, IMessageTemplateRenderer renderer) : IMessageWriteService
{
    public async Task<MessageWriteResult> TryCreateAsync(MessageWriteRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0)
            return new MessageWriteResult(false, null, null, request.TemplateKey, string.Empty, string.Empty);

        var templateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? "OrderCreated" : request.TemplateKey.Trim();
        var template = await repository.GetActiveTemplateAsync(templateKey, cancellationToken);
        if (template is null)
            return new MessageWriteResult(false, null, null, templateKey, string.Empty, string.Empty);

        var idempotencyKey = BuildIdempotencyKey(request);
        if (await repository.HasProcessedAsync(idempotencyKey, cancellationToken))
            return new MessageWriteResult(false, null, null, templateKey, template.Title, template.Body);

        var values = request.Context.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var title = renderer.Render(template.Title, values);
        var body = renderer.Render(template.Body, values);

        var created = await repository.CreateCustomerMessageAndNotificationAsync(
            request.CustomerId,
            request.OrderId,
            request.EventType,
            template.TemplateKey,
            template.Channel,
            title,
            body,
            request.PhoneNumber,
            request.ReferenceId,
            request.ReferenceType,
            idempotencyKey,
            cancellationToken);

        var messageId = created.MessageId;
        var notificationId = created.NotificationId;

        if (messageId is null or <= 0)
            return new MessageWriteResult(false, null, null, templateKey, title, body);

        return new MessageWriteResult(true, messageId, notificationId, templateKey, title, body);
    }

    private static string BuildIdempotencyKey(MessageWriteRequest request)
    {
        var reference = string.IsNullOrWhiteSpace(request.ReferenceId) ? request.OrderId?.ToString() ?? "0" : request.ReferenceId.Trim();
        return $"{request.EventType}:{request.ReferenceType}:{reference}:{request.CustomerId}:{request.OrderId ?? 0}";
    }
}
