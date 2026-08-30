namespace LUMAR_ERP_API_V2.DTOs.Messages;
public sealed record CustomerMessageDto(int CustomerMessageId,int CustomerId,int? OrderId,string EventType,string TemplateKey,string Channel,string Title,string Body,string DeliveryStatus,DateTime CreatedAtUtc,DateTime? SentAtUtc,DateTime? ReadAtUtc,int AttemptCount,DateTime? DeliveredAtUtc);
public sealed record CustomerNotificationDto(int CustomerNotificationId,int CustomerId,int? OrderId,string EventType,string Title,string Body,string Status,bool IsRead,DateTime CreatedAtUtc,DateTime? ReadAtUtc,int? CustomerMessageId);
public sealed record MessageTemplateDto(int TemplateId,string SourceTable,string? TemplateKey,string? TemplateName,string? Title,string? Channel,string? Body,bool? IsActive,DateTime? CreatedAtUtc,DateTime? UpdatedAtUtc);
public sealed record SentMessageDto(int MessageId,int? TrackingCode,int? CustomerId,string? MessageText,DateTime? SentDate);
public sealed record SentLogDto(int Id,int? MessageId);