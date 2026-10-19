using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("whatsapp")]
public sealed class WhatsAppWebhookController(
    IMessageRepository messageRepository,
    IWhatsAppConnectionSettingsStore settingsStore,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    [HttpGet("webhook")]
    public async Task<ActionResult> VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.IsNullOrWhiteSpace(challenge) || string.IsNullOrWhiteSpace(verifyToken))
            return BadRequest();

        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        var runtimeSettings = await settingsStore.GetRuntimeSettingsAsync(HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(runtimeSettings.VerifyToken))
        {
            logger.LogWarning("Rejected WhatsApp webhook verification because the runtime VerifyToken is empty.");
            return Unauthorized();
        }

        if (!string.Equals(verifyToken, runtimeSettings.VerifyToken, StringComparison.Ordinal))
            return Unauthorized();

        return Content(challenge);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        var signatureHeaderPresent = Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader);
        logger.LogInformation("WhatsApp webhook POST arrived. SignatureHeaderPresent={SignatureHeaderPresent}", signatureHeaderPresent);

        if (!signatureHeaderPresent)
            return Unauthorized();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
            return BadRequest();

        var runtimeSettings = await settingsStore.GetRuntimeSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(runtimeSettings.AppSecret))
        {
            logger.LogWarning("Rejected WhatsApp webhook because the runtime AppSecret is empty.");
            return Unauthorized();
        }

        var signatureValid = IsValidSignature(rawBody, signatureHeader.ToString(), runtimeSettings.AppSecret);
        logger.LogInformation("WhatsApp webhook signature validation completed. SignatureValid={SignatureValid}", signatureValid);
        if (!signatureValid)
            return Unauthorized();

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var statusWebhookPresent = false;
            var inboundMessagePresent = false;
            var inboundMessageType = "not_proven";
            var phoneNumberIdMatchedRuntime = false;
            var senderPresent = false;

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

                        if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array && statuses.GetArrayLength() > 0)
                        {
                            statusWebhookPresent = true;
                            foreach (var status in statuses.EnumerateArray())
                            {
                                var providerMessageId = status.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                                var statusName = status.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
                                var timestamp = status.TryGetProperty("timestamp", out var ts) ? ts.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                                if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(statusName))
                                    continue;

                                var occurredAtUtc = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                                string? failureReason = null;
                                if (status.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                                {
                                    failureReason = string.Join("; ", errors.EnumerateArray().Select(e => e.TryGetProperty("title", out var title) ? title.GetString() : e.ToString()));
                                }

                                await messageRepository.UpdateDeliveryStatusByProviderIdAsync(providerMessageId, MapStatus(statusName), occurredAtUtc, failureReason, cancellationToken);
                            }
                        }

                        if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
                        {
                            inboundMessagePresent = true;
                            var metadataPhoneNumberId = value.TryGetProperty("metadata", out var metadata)
                                && metadata.TryGetProperty("phone_number_id", out var phoneElement)
                                ? phoneElement.GetString()
                                : null;
                            phoneNumberIdMatchedRuntime = !string.IsNullOrWhiteSpace(metadataPhoneNumberId)
                                && string.Equals(metadataPhoneNumberId.Trim(), runtimeSettings.PhoneNumberId.Trim(), StringComparison.Ordinal);

                            foreach (var message in messages.EnumerateArray())
                            {
                                if (!message.TryGetProperty("from", out var fromElement) || string.IsNullOrWhiteSpace(fromElement.GetString()))
                                    continue;

                                senderPresent = true;
                            }

                            if (messages[0].TryGetProperty("type", out var messageType))
                            {
                                var typeValue = messageType.GetString();
                                inboundMessageType = string.IsNullOrWhiteSpace(typeValue) ? "other" : typeValue.Trim().ToLowerInvariant();
                            }
                        }
                    }
                }
            }

            logger.LogInformation(
                "WhatsApp webhook payload classified. SignatureValid={SignatureValid} StatusWebhookPresent={StatusWebhookPresent} InboundMessagePresent={InboundMessagePresent} InboundMessageType={InboundMessageType} PhoneNumberIdMatchedRuntime={PhoneNumberIdMatchedRuntime} SenderPresent={SenderPresent}",
                signatureValid,
                statusWebhookPresent,
                inboundMessagePresent,
                inboundMessageType,
                phoneNumberIdMatchedRuntime,
                senderPresent);

            if (statusWebhookPresent && !inboundMessagePresent)
            {
                logger.LogInformation("WhatsApp webhook status-only payload accepted. InboundMessagePresent={InboundMessagePresent}", false);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Received invalid WhatsApp webhook payload.");
            return BadRequest();
        }

        return Ok();
    }

    private static bool IsValidSignature(string rawBody, string receivedSignature, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(appSecret))
            return false;

        if (string.IsNullOrWhiteSpace(receivedSignature))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
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
