namespace LUMAR_ERP_API_V2.DTOs.Suppliers;

public sealed record SupplierMessageDto(
    int SupplierMessageId,
    int SupplierId,
    int MobileAccountId,
    string MessageType,
    string Subject,
    string Body,
    string Channel,
    string? RelatedEntityType,
    int? RelatedEntityId,
    string DeliveryStatus,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    string IdempotencyKey);

public sealed record SupplierNotificationDto(
    int SupplierNotificationId,
    int SupplierId,
    int MobileAccountId,
    string NotificationType,
    string Title,
    string Body,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    string? ReferenceType,
    int? ReferenceId);

public sealed record SupplierAnnouncementDto(
    int SupplierAnnouncementId,
    int SupplierId,
    string Title,
    string Body,
    bool IsActive,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime CreatedAtUtc);
