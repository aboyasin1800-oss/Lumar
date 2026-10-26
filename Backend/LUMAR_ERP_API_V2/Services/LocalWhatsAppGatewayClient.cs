using System.Net.Http.Json;
using System.Text.Json;
using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LocalWhatsAppGatewayClient(HttpClient http)
{
    private static readonly HashSet<string> SessionStates = new(StringComparer.Ordinal)
    {
        "NotLinked", "Linking", "Connected", "Disconnected", "Reconnecting", "LoggedOut", "Unavailable", "Error"
    };

    public async Task<LocalWhatsAppHealthResponse> HealthAsync(CancellationToken cancellationToken)
    {
        var result = await RequestAsync<LocalWhatsAppHealthResponse>(HttpMethod.Get, "health", cancellationToken);
        if (result.ContractVersion != 1) throw new LocalWhatsAppGatewayException();
        return result with { OperationalActivation = false };
    }

    public async Task<LocalWhatsAppSessionResponse> StatusAsync(CancellationToken cancellationToken)
        => Sanitize(await RequestAsync<LocalWhatsAppSessionResponse>(HttpMethod.Get, "session/status", cancellationToken));

    public async Task<LocalWhatsAppSessionResponse> CommandAsync(string action, CancellationToken cancellationToken)
    {
        if (action is not ("link" or "disconnect" or "logout" or "reconnect" or "replace"))
            throw new ArgumentException("Unsupported local session action.", nameof(action));
        return Sanitize(await RequestAsync<LocalWhatsAppSessionResponse>(HttpMethod.Post, $"session/{action}", cancellationToken));
    }

    public async Task<LocalWhatsAppLinkResponse> LinkMaterialAsync(CancellationToken cancellationToken)
    {
        var result = await RequestAsync<LocalWhatsAppLinkResponse>(HttpMethod.Get, "session/link-material", cancellationToken);
        if (result.Kind != "qr" || string.IsNullOrWhiteSpace(result.Value) || result.Value.Length > 4096
            || result.ExpiresAt is null || result.ExpiresAt <= DateTimeOffset.UtcNow
            || result.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return new LocalWhatsAppLinkResponse(null, null, null);
        return result;
    }

    private static LocalWhatsAppSessionResponse Sanitize(LocalWhatsAppSessionResponse result)
    {
        if (!SessionStates.Contains(result.State)) throw new LocalWhatsAppGatewayException();
        var number = result.State == "Connected" && result.CurrentNumber is { Length: >= 8 and <= 15 } value
            && value[0] != '0' && value.All(character => character is >= '0' and <= '9') ? result.CurrentNumber : null;
        var error = result.LastError switch
        {
            null => null,
            "SESSION_STORAGE_ERROR" or "CURRENT_NUMBER_UNCONFIRMED" or "SESSION_LOGGED_OUT"
                or "CONNECTION_LOST" or "SESSION_CONNECTION_FAILED" or "SEND_UNCONFIRMED" => result.LastError,
            _ => "GATEWAY_ERROR"
        };
        return result with { CurrentNumber = number, LastError = error, OperationalActivation = false };
    }

    private async Task<T> RequestAsync<T>(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post) request.Content = JsonContent.Create(new { });
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new LocalWhatsAppGatewayException((int)response.StatusCode == 409 ? 409 : 503);
            await response.Content.LoadIntoBufferAsync(65536, cancellationToken);
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new LocalWhatsAppGatewayException();
        }
        catch (HttpRequestException) { throw new LocalWhatsAppGatewayException(); }
        catch (JsonException) { throw new LocalWhatsAppGatewayException(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new LocalWhatsAppGatewayException(); }
    }

    public async Task<OutboundAdapterResult> SendAsync(string recipient, string text, string? imageUrl, CancellationToken cancellationToken)
    {
        return await SendInternalAsync(recipient, text, imageUrl, null, cancellationToken);
    }

    public async Task<OutboundAdapterResult> SendAsync(string recipient, string text, LocalCampaignMediaPayload? media, CancellationToken cancellationToken)
    {
        return await SendInternalAsync(recipient, text, null, media, cancellationToken);
    }

    private static LocalWhatsAppSendRequest CreateSendPayload(string recipient, string text, string? imageUrl, LocalCampaignMediaPayload? media)
        => new(recipient, text, imageUrl, media);

    private async Task<OutboundAdapterResult> SendInternalAsync(string recipient, string text, string? imageUrl, LocalCampaignMediaPayload? media, CancellationToken cancellationToken)
    {
        try
        {
            var payload = CreateSendPayload(recipient, text, imageUrl, media);

            using var response = await http.PostAsJsonAsync("messages/send", payload, cancellationToken);
            await response.Content.LoadIntoBufferAsync(65536, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<GatewaySendResponse>(cancellationToken);

            if (response.IsSuccessStatusCode && result?.Outcome == "Sent" && !string.IsNullOrWhiteSpace(result.ProviderMessageId) && result.ProviderMessageId.Length <= 200)
            {
                return new OutboundAdapterResult(OutboundDispatchOutcome.Sent,
                    ProviderMessageId: result.ProviderMessageId,
                    Retryable: false,
                    HttpStatusCode: (int)response.StatusCode);
            }

            if (response.IsSuccessStatusCode && result is not null && result.Outcome == "Sent")
            {
                return new OutboundAdapterResult(OutboundDispatchOutcome.Unconfirmed,
                    FailureReason: "Local WhatsApp outcome is unconfirmed; automatic retry is unsafe.",
                    Retryable: false,
                    HttpStatusCode: (int)response.StatusCode);
            }

            if (result?.Code == "SEND_UNCONFIRMED" || result?.Outcome == "Unconfirmed" || result?.Outcome == "SendUnconfirmed")
            {
                return new OutboundAdapterResult(OutboundDispatchOutcome.Unconfirmed,
                    FailureReason: "Local WhatsApp outcome is unconfirmed; automatic retry is unsafe.",
                    Retryable: false,
                    HttpStatusCode: (int)response.StatusCode);
            }

            var retryable = !response.IsSuccessStatusCode && result?.Code is "NOT_CONNECTED" or "BUSY";
            var reason = result?.Code switch
            {
                "NOT_CONNECTED" => "Local WhatsApp session is not connected; no send attempted.",
                "BUSY" => "Local WhatsApp session is busy; no send attempted.",
                "TEST_RECIPIENT_REQUIRED" => "Local foundation permits only the owner-approved test recipient.",
                "INVALID_MESSAGE" => "Invalid local WhatsApp transport payload.",
                "MEDIA_REJECTED" => "Local WhatsApp media was rejected before sending.",
                _ => "Local WhatsApp outcome is unconfirmed; automatic retry is unsafe."
            };

            return new OutboundAdapterResult(
                retryable ? OutboundDispatchOutcome.TransientFailure : OutboundDispatchOutcome.PermanentFailure,
                FailureReason: reason,
                Retryable: retryable,
                HttpStatusCode: (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unconfirmed();
        }
        catch (HttpRequestException)
        {
            return Unconfirmed();
        }
        catch (JsonException)
        {
            return Unconfirmed();
        }
    }

    private static OutboundAdapterResult Unconfirmed() => new(OutboundDispatchOutcome.Unconfirmed,
        FailureReason: "Local WhatsApp outcome is unconfirmed; automatic retry is unsafe.", Retryable: false);

    private sealed record LocalWhatsAppSendRequest(string Recipient, string Text, string? ImageUrl, LocalCampaignMediaPayload? Media);

    private sealed record GatewaySendResponse(string? Outcome, string? ProviderMessageId, string? Code);
}

public sealed class LocalWhatsAppGatewayException(int statusCode = 503) : Exception("Local WhatsApp gateway is unavailable or the session action cannot be completed.")
{
    public int StatusCode { get; } = statusCode;
}