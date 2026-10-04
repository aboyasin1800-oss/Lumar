using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierMessagingFoundationTests
{
    [Fact]
    public async Task Supplier_messages_are_scoped_to_authenticated_supplier_only()
    {
        var service = new SupplierMessagingService(
            new FakeSupplierMessagingRepository(),
            new FakeSupplierOwnershipResolver(77));

        var messages = await service.GetMessagesAsync(1, 20, CancellationToken.None);

        Assert.NotEmpty(messages);
        Assert.All(messages, message => Assert.Equal(77, message.SupplierId));
    }

    [Fact]
    public async Task Supplier_notifications_are_scoped_to_authenticated_supplier_only()
    {
        var service = new SupplierMessagingService(
            new FakeSupplierMessagingRepository(),
            new FakeSupplierOwnershipResolver(77));

        var notifications = await service.GetNotificationsAsync(1, 20, CancellationToken.None);

        Assert.NotEmpty(notifications);
        Assert.All(notifications, notification => Assert.Equal(77, notification.SupplierId));
    }

    [Fact]
    public async Task Supplier_announcements_are_scoped_to_authenticated_supplier_only()
    {
        var service = new SupplierMessagingService(
            new FakeSupplierMessagingRepository(),
            new FakeSupplierOwnershipResolver(77));

        var announcements = await service.GetAnnouncementsAsync(1, 20, CancellationToken.None);

        Assert.NotEmpty(announcements);
        Assert.All(announcements, announcement => Assert.Equal(77, announcement.SupplierId));
    }

    [Fact]
    public async Task Supplier_messaging_service_rejects_non_supplier_accounts()
    {
        var service = new SupplierMessagingService(
            new FakeSupplierMessagingRepository(),
            new FakeSupplierOwnershipResolver(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetMessagesAsync(1, 20, CancellationToken.None));
    }

    private sealed class FakeSupplierOwnershipResolver(int? supplierId) : ISupplierOwnershipResolver
    {
        public Task<int?> ResolveCurrentSupplierAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(supplierId);

        public Task<int?> ResolveCurrentSupplier(CancellationToken cancellationToken = default)
            => Task.FromResult(supplierId);

        public Task<int> RequireCurrentSupplierAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(supplierId ?? throw new InvalidOperationException("Missing supplier identity."));

        public bool CanAccessSupplier(int? currentSupplierId, int? requestedSupplierId)
            => currentSupplierId == requestedSupplierId;
    }

    private sealed class FakeSupplierMessagingRepository : ISupplierMessagingRepository
    {
        public Task<IReadOnlyList<SupplierMessageDto>> GetMessagesAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SupplierMessageDto>>(
                new[] { new SupplierMessageDto(1, supplierId, 15, "Payment", "Invoice paid", "Your invoice has been paid.", "InApp", "Invoice", 101, "Delivered", true, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-30), "msg-id-1") });

        public Task<SupplierMessageDto?> GetMessageAsync(int supplierId, int supplierMessageId, CancellationToken cancellationToken)
            => Task.FromResult<SupplierMessageDto?>(new SupplierMessageDto(1, supplierId, 15, "Payment", "Invoice paid", "Your invoice has been paid.", "InApp", "Invoice", 101, "Delivered", true, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-30), "msg-id-1"));

        public Task<IReadOnlyList<SupplierNotificationDto>> GetNotificationsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SupplierNotificationDto>>(
                new[] { new SupplierNotificationDto(1, supplierId, 15, "Payment", "Payment cleared", "Your payment has been cleared.", false, null, DateTime.UtcNow.AddMinutes(-20), null, "Payment", 101) });

        public Task<SupplierNotificationDto?> GetNotificationAsync(int supplierId, int supplierNotificationId, CancellationToken cancellationToken)
            => Task.FromResult<SupplierNotificationDto?>(new SupplierNotificationDto(1, supplierId, 15, "Payment", "Payment cleared", "Your payment has been cleared.", false, null, DateTime.UtcNow.AddMinutes(-20), null, "Payment", 101));

        public Task<IReadOnlyList<SupplierAnnouncementDto>> GetAnnouncementsAsync(int supplierId, int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SupplierAnnouncementDto>>(
                new[] { new SupplierAnnouncementDto(1, supplierId, "Supplier notice", "Quarterly supplier statement available.", true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(-2)) });

        public Task<SupplierAnnouncementDto?> GetAnnouncementAsync(int supplierId, int supplierAnnouncementId, CancellationToken cancellationToken)
            => Task.FromResult<SupplierAnnouncementDto?>(new SupplierAnnouncementDto(1, supplierId, "Supplier notice", "Quarterly supplier statement available.", true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(-2)));
    }
}
