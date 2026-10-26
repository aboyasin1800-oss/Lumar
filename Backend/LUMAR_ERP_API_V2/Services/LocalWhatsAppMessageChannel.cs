using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.DTOs.Messages;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LocalWhatsAppMessageChannel(LocalWhatsAppGatewayClient gateway) : IOutboundMessageChannel
{
    public string ChannelName => "LocalWhatsApp";

    public Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(message.PhoneNumber)
            || !Regex.IsMatch(message.PhoneNumber, @"\A[1-9][0-9]{7,14}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            || string.IsNullOrWhiteSpace(message.Body) || message.Body.Length > 4096)
        {
            return Task.FromResult(new OutboundAdapterResult(OutboundDispatchOutcome.PermanentFailure,
                FailureReason: "Invalid local WhatsApp destination or final message body.", Retryable: false));
        }

        return gateway.SendAsync(message.PhoneNumber, message.Body, message.CampaignImageUrl, cancellationToken);
    }
}