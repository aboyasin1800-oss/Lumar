using LUMAR_ERP_API_V2.DTOs.Messages;

namespace LUMAR_ERP_API_V2.Services;

public sealed class InAppMessageChannel : IOutboundMessageChannel
{
    public string ChannelName => "InApp";

    public Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken)
        => Task.FromResult(new OutboundAdapterResult(OutboundDispatchOutcome.Sent, ProviderMessageId: null, FailureReason: null, Retryable: false));
}
