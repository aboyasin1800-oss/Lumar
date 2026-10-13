using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("whatsapp")]
public sealed class WhatsAppWebhookController(
    IMessageRepository messageRepository,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    private readonly WhatsAppOptions _options = options.Value;

    [HttpGet("webhook")]
    public ActionResult VerifyWebhook([FromQuery] string? mode, [FromQuery] string? challenge, [FromQuery] string? verify_token)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.IsNullOrWhiteSpace(challenge) || string.IsNullOrWhiteSpace(verify_token))
            return BadRequest();

        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (!string.Equals(verify_token, _options.VerifyToken, StringComparison.Ordinal))
            return Unauthorized();

        return Content(challenge);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
            return Unauthorized();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!IsValidSignature(rawBody, signatureHeader.ToString()))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(rawBody))
            return BadRequest();

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            if (document.RootElement.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes))
                        continue;

                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value))
                            continue;

                        if (value.TryGetProperty("statuses", out var statuses))
                        {
                            foreach (var status in statuses.EnumerateArray())
                            {
                                var providerMessageId = status.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                                var statusName = status.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
                                var timestamp = status.TryGetProperty("timestamp", out var ts) ? ts.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                                if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(statusName))
                                    continue;

                                var occurredAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
                                string? failureReason = null;
                                if (status.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                                {
                                    failureReason = string.Join("; ", errors.EnumerateArray().Select(e => e.TryGetProperty("title", out var title) ? title.GetString() : e.ToString()));
                                }

                                await messageRepository.UpdateDeliveryStatusByProviderIdAsync(providerMessageId, MapStatus(statusName), occurredAtUtc, failureReason, cancellationToken);
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Received invalid WhatsApp webhook payload.");
            return BadRequest();
        }

        return Ok();
    }

    private bool IsValidSignature(string rawBody, string receivedSignature)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret))
            return true;

        if (string.IsNullOrWhiteSpace(receivedSignature))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var expected = $"sha256={hash}";
        return string.Equals(receivedSignature.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string MapStatus(string status)
    {
        return status.Trim() switch
        {
            "sent" => "Sent",
            "delivered" => "Delivered",
            "read" => "Read",
            "failed" => "Failed",
            _ => status.Trim()
        };
    }
}
