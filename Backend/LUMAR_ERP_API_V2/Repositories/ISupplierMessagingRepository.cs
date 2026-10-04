using LUMAR_ERP_API_V2.DTOs.Suppliers;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ISupplierMessagingRepository
{
    Task<IReadOnlyList<SupplierMessageDto>> GetMessagesAsync(int supplierId, int skip, int take, CancellationToken cancellationToken);
    Task<SupplierMessageDto?> GetMessageAsync(int supplierId, int supplierMessageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierNotificationDto>> GetNotificationsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken);
    Task<SupplierNotificationDto?> GetNotificationAsync(int supplierId, int supplierNotificationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierAnnouncementDto>> GetAnnouncementsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken);
    Task<SupplierAnnouncementDto?> GetAnnouncementAsync(int supplierId, int supplierAnnouncementId, CancellationToken cancellationToken);
}
