using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class SupplierMessagingService(
    ISupplierMessagingRepository repository,
    ISupplierOwnershipResolver supplierOwnershipResolver) : ISupplierMessagingService
{
    public async Task<IReadOnlyList<SupplierMessageDto>> GetMessagesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (safePage - 1) * safePageSize;
        return await repository.GetMessagesAsync(currentSupplierId, skip, safePageSize, cancellationToken);
    }

    public async Task<SupplierMessageDto?> GetMessageAsync(int supplierMessageId, CancellationToken cancellationToken = default)
    {
        if (supplierMessageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierMessageId));

        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        return await repository.GetMessageAsync(currentSupplierId, supplierMessageId, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierNotificationDto>> GetNotificationsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (safePage - 1) * safePageSize;
        return await repository.GetNotificationsAsync(currentSupplierId, skip, safePageSize, cancellationToken);
    }

    public async Task<SupplierNotificationDto?> GetNotificationAsync(int supplierNotificationId, CancellationToken cancellationToken = default)
    {
        if (supplierNotificationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierNotificationId));

        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        return await repository.GetNotificationAsync(currentSupplierId, supplierNotificationId, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierAnnouncementDto>> GetAnnouncementsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (safePage - 1) * safePageSize;
        return await repository.GetAnnouncementsAsync(currentSupplierId, skip, safePageSize, cancellationToken);
    }

    public async Task<SupplierAnnouncementDto?> GetAnnouncementAsync(int supplierAnnouncementId, CancellationToken cancellationToken = default)
    {
        if (supplierAnnouncementId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierAnnouncementId));

        var currentSupplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        return await repository.GetAnnouncementAsync(currentSupplierId, supplierAnnouncementId, cancellationToken);
    }

    private async Task<int> ResolveCurrentSupplierAsync(CancellationToken cancellationToken)
    {
        var currentSupplierId = await supplierOwnershipResolver.ResolveCurrentSupplierAsync(cancellationToken);
        if (!currentSupplierId.HasValue)
            throw new InvalidOperationException("Only a supplier account may access supplier messaging data.");

        return currentSupplierId.Value;
    }
}
