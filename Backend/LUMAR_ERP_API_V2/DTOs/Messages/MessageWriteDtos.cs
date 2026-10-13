namespace LUMAR_ERP_API_V2.DTOs.Messages;

public sealed record MessageTemplateRecord(
    int TemplateId,
    string TemplateKey,
    string Title,
    string Channel,
    string Body);

public sealed record MessageWriteRequest(
    int CustomerId,
    int? OrderId,
    string EventType,
    string TemplateKey,
    string PhoneNumber,
    string ReferenceId,
    string ReferenceType,
    IReadOnlyDictionary<string, object?> Context);

public sealed record MessageWriteResult(
    bool Created,
    int? CustomerMessageId,
    int? CustomerNotificationId,
    string TemplateKey,
    string Title,
    string Body);
