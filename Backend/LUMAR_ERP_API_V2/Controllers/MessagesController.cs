using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController(IMessageService service, ICustomerService customers) : ControllerBase
{
	[HttpGet("customer-messages")]
	public async Task<ActionResult<IReadOnlyList<CustomerMessageDto>>> GetCustomerMessages(CancellationToken ct) => Ok(await service.GetCustomerMessagesAsync(ct));

	[HttpGet("customer-messages/{id:int}")]
	public async Task<ActionResult<CustomerMessageDto>> GetCustomerMessage(int id, CancellationToken ct)
	{
		if (id <= 0) return BadRequest("Customer message id must be positive.");
		var item = await service.GetCustomerMessageAsync(id, ct);
		return item is null ? NotFound() : Ok(item);
	}

	[HttpGet("customers/{customerId:int}/messages")]
	public async Task<ActionResult<IReadOnlyList<CustomerMessageDto>>> GetCustomerMessages(int customerId, CancellationToken ct)
	{
		var error = await Customer(customerId, ct);
		return error ?? Ok(await service.GetCustomerMessagesAsync(customerId, ct));
	}

	[HttpGet("notifications")]
	public async Task<ActionResult<IReadOnlyList<CustomerNotificationDto>>> GetNotifications(CancellationToken ct) => Ok(await service.GetNotificationsAsync(ct));

	[HttpGet("customers/{customerId:int}/notifications")]
	public async Task<ActionResult<IReadOnlyList<CustomerNotificationDto>>> GetCustomerNotifications(int customerId, CancellationToken ct)
	{
		var error = await Customer(customerId, ct);
		return error ?? Ok(await service.GetNotificationsAsync(customerId, ct));
	}

	[HttpGet("templates")]
	public async Task<ActionResult<IReadOnlyList<MessageTemplateDto>>> GetTemplates(CancellationToken ct) => Ok(await service.GetTemplatesAsync(ct));

	[HttpGet("templates/{id:int}")]
	public async Task<ActionResult<MessageTemplateDto>> GetTemplate(int id, CancellationToken ct)
	{
		if (id <= 0) return BadRequest("Template id must be positive.");
		var items = await service.GetTemplatesByIdAsync(id, ct);
		return items.Count == 0 ? NotFound() : items.Count > 1 ? StatusCode(501, "Template id is ambiguous across independent template tables.") : Ok(items[0]);
	}

	[HttpGet("sent")]
	public async Task<ActionResult<IReadOnlyList<SentMessageDto>>> GetSent(CancellationToken ct) => Ok(await service.GetSentAsync(ct));

	[HttpGet("sent-log")]
	public async Task<ActionResult<IReadOnlyList<SentLogDto>>> GetSentLog(CancellationToken ct) => Ok(await service.GetSentLogAsync(ct));

	[HttpPost]
	[HttpPut("{id:int}")]
	[HttpDelete("{id:int}")]
	public IActionResult WriteDisabled() => StatusCode(405, "Message writes are disabled while LUMAR_ERP is read-only.");

	private async Task<ActionResult?> Customer(int id, CancellationToken ct)
	{
		if (id <= 0) return BadRequest("Customer id must be positive.");
		return await customers.GetByIdAsync(id, ct) is null ? NotFound() : null;
	}
}