using LUMAR_ERP_API_V2.DTOs.Messages;

namespace LUMAR_ERP_API_V2.Services;

public interface IOutboundMessageChannel
{
    string ChannelName { get; }
    Task<OutboundAdapterResult> SendAsync(CustomerMessageDto message, CancellationToken cancellationToken);
}
