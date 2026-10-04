using LUMAR_ERP_API_V2.DTOs.Suppliers;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierMessagingService
{
    Task<IReadOnlyList<SupplierMessageDto>> GetMessagesAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SupplierMessageDto?> GetMessageAsync(int supplierMessageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierNotificationDto>> GetNotificationsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SupplierNotificationDto?> GetNotificationAsync(int supplierNotificationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierAnnouncementDto>> GetAnnouncementsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SupplierAnnouncementDto?> GetAnnouncementAsync(int supplierAnnouncementId, CancellationToken cancellationToken = default);
}
