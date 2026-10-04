using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("mobile/supplier")]
public sealed class SupplierMessagingController(ISupplierMessagingService service, ISupplierOwnershipResolver ownershipResolver) : ControllerBase
{
    [HttpGet("messages")]
    public async Task<ActionResult<IReadOnlyList<SupplierMessageDto>>> GetMessages([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var messages = await service.GetMessagesAsync(safePage, safePageSize, cancellationToken);
        return Ok(messages);
    }

    [HttpGet("messages/{messageId:int}")]
    public async Task<ActionResult<SupplierMessageDto>> GetMessage(int messageId, CancellationToken cancellationToken)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var message = await service.GetMessageAsync(messageId, cancellationToken);
        return message is null ? NotFound() : Ok(message);
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<SupplierNotificationDto>>> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var notifications = await service.GetNotificationsAsync(safePage, safePageSize, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("notifications/{notificationId:int}")]
    public async Task<ActionResult<SupplierNotificationDto>> GetNotification(int notificationId, CancellationToken cancellationToken)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var notification = await service.GetNotificationAsync(notificationId, cancellationToken);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<SupplierAnnouncementDto>>> GetAnnouncements([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var announcements = await service.GetAnnouncementsAsync(safePage, safePageSize, cancellationToken);
        return Ok(announcements);
    }

    [HttpGet("announcements/{announcementId:int}")]
    public async Task<ActionResult<SupplierAnnouncementDto>> GetAnnouncement(int announcementId, CancellationToken cancellationToken)
    {
        if (!await IsSupplierAuthenticatedAsync(cancellationToken))
            return Forbid();

        var announcement = await service.GetAnnouncementAsync(announcementId, cancellationToken);
        return announcement is null ? NotFound() : Ok(announcement);
    }

    private async Task<bool> IsSupplierAuthenticatedAsync(CancellationToken cancellationToken)
        => (await ownershipResolver.ResolveCurrentSupplierAsync(cancellationToken)).HasValue;
}
