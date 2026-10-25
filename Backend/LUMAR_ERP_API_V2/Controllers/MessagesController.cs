using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("messages")]
public sealed class MessagesController(
    IMessageService service,
    ICustomerService customers,
    IManualCampaignService manualCampaigns,
    IAuthenticatedUserContext userContext,
    CampaignMediaStorageService campaignMediaStorage) : ControllerBase
{
	[HttpGet("customer-messages")]
	public async Task<ActionResult<IReadOnlyList<CustomerMessageDto>>> GetCustomerMessages(CancellationToken ct)
	{
		var authError = await EnforceCustomerMobileIsolationAsync(ct);
		return authError ?? Ok(await service.GetCustomerMessagesAsync(ct));
	}

	[HttpGet("customer-messages/{id:int}")]
	public async Task<ActionResult<CustomerMessageDto>> GetCustomerMessage(int id, CancellationToken ct)
	{
		if (id <= 0) return BadRequest("Customer message id must be positive.");

		var authError = await EnforceCustomerOwnershipAsync(null, ct);
		if (authError is not null)
			return authError;

		var item = await service.GetCustomerMessageAsync(id, ct);
		if (item is null)
			return NotFound();

		var currentUser = await userContext.GetCurrentUserAsync(ct);
		if (currentUser is not null && string.Equals(currentUser.AccountType, "Customer", StringComparison.OrdinalIgnoreCase))
		{
			var authorizedCustomerIds = ResolveAuthorizedCustomerIds(currentUser);
			if (!authorizedCustomerIds.Contains(item.CustomerId))
				return Forbid();
		}

		return Ok(item);
	}

	[HttpGet("customers/{customerId:int}/messages")]
	public async Task<ActionResult<IReadOnlyList<CustomerMessageDto>>> GetCustomerMessages(int customerId, CancellationToken ct)
	{
		var error = await Customer(customerId, ct);
		if (error is not null)
			return error;

		var authError = await EnforceCustomerOwnershipAsync(customerId, ct);
		return authError ?? Ok(await service.GetCustomerMessagesAsync(customerId, ct));
	}

	[HttpGet("notifications")]
	public async Task<ActionResult<IReadOnlyList<CustomerNotificationDto>>> GetNotifications(CancellationToken ct)
	{
		var authError = await EnforceCustomerMobileIsolationAsync(ct);
		return authError ?? Ok(await service.GetNotificationsAsync(ct));
	}

	[HttpGet("customers/{customerId:int}/notifications")]
	public async Task<ActionResult<IReadOnlyList<CustomerNotificationDto>>> GetCustomerNotifications(int customerId, CancellationToken ct)
	{
		var error = await Customer(customerId, ct);
		if (error is not null)
			return error;

		var authError = await EnforceCustomerOwnershipAsync(customerId, ct);
		return authError ?? Ok(await service.GetNotificationsAsync(customerId, ct));
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

	[HttpPost("templates")]
	public async Task<ActionResult<MessageTemplateDto>> CreateTemplate([FromBody] CreateMessageTemplateDto request, CancellationToken ct)
	{
		try
		{
			var created = await service.CreateTemplateAsync(request, ct);
			return created is null ? StatusCode(500, "Unable to create message template.") : CreatedAtAction(nameof(GetTemplate), new { id = created.TemplateId }, created);
		}
		catch (ArgumentException exception)
		{
			return BadRequest(exception.Message);
		}
	}

	[HttpPut("templates/{id:int}")]
	public async Task<ActionResult<MessageTemplateDto>> UpdateTemplate(int id, [FromBody] UpdateMessageTemplateDto request, CancellationToken ct)
	{
		try
		{
			var updated = await service.UpdateTemplateAsync(id, request, ct);
			return updated is null ? NotFound() : Ok(updated);
		}
		catch (ArgumentException exception)
		{
			return BadRequest(exception.Message);
		}
	}

	[HttpPut("templates/{id:int}/activate")]
	public async Task<ActionResult<MessageTemplateDto>> ActivateTemplate(int id, CancellationToken ct)
	{
		var template = await service.ActivateTemplateAsync(id, ct);
		return template is null ? NotFound() : Ok(template);
	}

	[HttpPut("templates/{id:int}/deactivate")]
	public async Task<ActionResult<MessageTemplateDto>> DeactivateTemplate(int id, CancellationToken ct)
	{
		var template = await service.DeactivateTemplateAsync(id, ct);
		return template is null ? NotFound() : Ok(template);
	}

	[HttpGet("sent")]
	public async Task<ActionResult<IReadOnlyList<SentMessageDto>>> GetSent(CancellationToken ct) => Ok(await service.GetSentAsync(ct));

	[HttpGet("sent-log")]
	public async Task<ActionResult<IReadOnlyList<SentLogDto>>> GetSentLog(CancellationToken ct) => Ok(await service.GetSentLogAsync(ct));

	[HttpPost("campaign-image-upload")]
	public async Task<ActionResult<CampaignMediaUploadResult>> UploadCampaignImage(IFormFile? file, CancellationToken ct)
	{
		if (file is null || file.Length == 0)
			return BadRequest("Campaign image file is required.");

		try
		{
			var result = await campaignMediaStorage.SaveAsync(file, ct);
			return Ok(result);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(ex.Message);
		}
		catch (IOException ex)
		{
			return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
		}
	}

	[HttpPost("manual-campaigns")]
	[HttpPost("campaigns")]
	public async Task<ActionResult<ManualCampaignResult>> TriggerManualCampaign([FromBody] ManualCampaignRequest request, CancellationToken ct)
	{
		if (!Enum.IsDefined(typeof(ManualCampaignRecipientMode), request.RecipientMode))
			return BadRequest("RecipientMode must be 0 (SingleCustomer) or 1 (AllEligibleCustomers).");

		if (string.IsNullOrWhiteSpace(request.CampaignReference)) return BadRequest("CampaignReference is required.");
		if (string.IsNullOrWhiteSpace(request.TemplateKey)) return BadRequest("TemplateKey is required.");
		if (string.IsNullOrWhiteSpace(request.MessageText)) return BadRequest("MessageText is required.");

		var user = await userContext.GetCurrentUserAsync(ct);
		if (user is not null && string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
			return StatusCode(StatusCodes.Status403Forbidden, "Employee accounts cannot send manual campaigns.");

		var result = await manualCampaigns.ProcessAsync(request, ct);
		return Ok(result);
	}

	[HttpPost]
	[HttpPut("{id:int}")]
	[HttpDelete("{id:int}")]
	public IActionResult WriteDisabled() => StatusCode(405, "Message writes are disabled while LUMAR_ERP is read-only.");

	private async Task<ActionResult?> Customer(int id, CancellationToken ct)
	{
		if (id <= 0) return BadRequest("Customer id must be positive.");
		return await customers.GetByIdAsync(id, ct) is null ? NotFound() : null;
	}

	private async Task<ActionResult?> EnforceCustomerMobileIsolationAsync(CancellationToken ct)
	{
		var currentUser = await userContext.GetCurrentUserAsync(ct);
		if (currentUser is null)
			return Unauthorized();

		if (!string.Equals(currentUser.AccountType, "Customer", StringComparison.OrdinalIgnoreCase))
			return null;

		return StatusCode(StatusCodes.Status403Forbidden);
	}

	private async Task<ActionResult?> EnforceCustomerOwnershipAsync(int? customerId, CancellationToken ct)
	{
		var currentUser = await userContext.GetCurrentUserAsync(ct);
		if (currentUser is null)
			return Unauthorized();

		if (!string.Equals(currentUser.AccountType, "Customer", StringComparison.OrdinalIgnoreCase))
			return null;

		if (customerId is null || customerId <= 0)
			return StatusCode(StatusCodes.Status403Forbidden);

		var authorizedCustomerIds = ResolveAuthorizedCustomerIds(currentUser);
		return authorizedCustomerIds.Contains(customerId.Value) ? null : StatusCode(StatusCodes.Status403Forbidden);
	}

	private static HashSet<int> ResolveAuthorizedCustomerIds(CurrentUserDto currentUser)
	{
		var authorized = new HashSet<int>();
		if (currentUser.CustomerId is > 0)
			authorized.Add(currentUser.CustomerId.Value);
		foreach (var managedCustomerId in currentUser.ManagedCustomerIds)
		{
			if (managedCustomerId > 0)
				authorized.Add(managedCustomerId);
		}
		return authorized;
	}
}