using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.DTOs.Messages;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class WhatsAppCloudMessageChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppOptions> options) : IOutboundMessageChannel
{
    private readonly WhatsAppOptions _options = options.Value;

    public string ChannelName => "WhatsApp";

    public async Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "WhatsApp provider is disabled in configuration.", Retryable: false);

        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId))
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "WhatsApp provider is not configured.", Retryable: false);

        var destination = NormalizePhoneNumber(message.PhoneNumber);
        if (string.IsNullOrWhiteSpace(destination))
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "Invalid WhatsApp destination.", Retryable: false);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "text",
            text = new { body = message.Body ?? string.Empty }
        };

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://graph.facebook.com" : _options.BaseUrl.TrimEnd('/');
        var endpoint = $"{baseUrl}/{_options.ApiVersion.Trim('/')}/{_options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var client = httpClientFactory.CreateClient("WhatsAppCloud");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var hint = ExtractErrorHint(raw);
                return new OutboundAdapterResult(
                    response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == (HttpStatusCode)429
                        ? OutboundDispatchOutcome.TransientFailure
                        : OutboundDispatchOutcome.PermanentFailure,
                    FailureReason: string.IsNullOrWhiteSpace(hint) ? $"WhatsApp API returned {(int)response.StatusCode}." : hint,
                    Retryable: response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == (HttpStatusCode)429);
            }

            var doc = JsonDocument.Parse(raw);
            var providerId = doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(providerId))
                return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: "WhatsApp response missing provider message identifier.", Retryable: false);

            return new OutboundAdapterResult(OutboundDispatchOutcome.Sent, ProviderMessageId: providerId, FailureReason: null, Retryable: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure, FailureReason: "WhatsApp request timed out or failed due to network conditions.", Retryable: true);
        }
        catch (HttpRequestException ex)
        {
            return new OutboundAdapterResult(OutboundDispatchOutcome.TransientFailure, FailureReason: ex.Message, Retryable: true);
        }
        catch (Exception ex)
        {
            return new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure, FailureReason: ex.Message, Retryable: false);
        }
    }

    private static string? NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (cleaned.Length < 8)
            return null;

        if (cleaned.StartsWith("966", StringComparison.Ordinal))
            return "+" + cleaned;

        if (cleaned.Length == 10 && cleaned.StartsWith("5", StringComparison.Ordinal))
            return "+966" + cleaned.TrimStart('0');

        if (cleaned.Length > 10)
            return "+" + cleaned.TrimStart('0');

        return null;
    }

    private static string? ExtractErrorHint(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("message", out var message))
                    return message.GetString();

                if (error.TryGetProperty("type", out var type) && error.TryGetProperty("code", out var code))
                    return $"{type.GetString()} ({code.GetInt32()})";
            }
        }
        catch
        {
            // ignore malformed JSON; keep a safe fallback
        }

        return raw.Length > 250 ? raw[..247] + "..." : raw;
    }
}
