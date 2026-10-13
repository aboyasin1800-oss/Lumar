using LUMAR_ERP_API_V2.DTOs.Messages;

namespace LUMAR_ERP_API_V2.Services;

public interface IOutboundMessageDispatcher
{
    Task<OutboundDispatchResult?> DispatchNextAsync(CancellationToken cancellationToken);
}
