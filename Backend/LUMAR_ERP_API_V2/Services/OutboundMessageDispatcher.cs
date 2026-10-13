using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OutboundMessageDispatcher(
    IMessageRepository repository,
    IEnumerable<IOutboundMessageChannel> channels,
    ILogger<OutboundMessageDispatcher> logger,
    IConfiguration configuration) : IOutboundMessageDispatcher
{
    public async Task<OutboundDispatchResult?> DispatchNextAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = configuration.GetValue("OutboundMessaging:MaxAttempts", 5);
        var staleProcessingMinutes = configuration.GetValue("OutboundMessaging:StaleProcessingMinutes", 30);

        var channelMap = channels.ToDictionary(x => x.ChannelName, x => x, StringComparer.OrdinalIgnoreCase);
        if (channelMap.Count == 0)
        {
            logger.LogDebug("No outbound message channels are registered.");
            return null;
        }

        foreach (var channelName in channelMap.Keys)
        {
            var message = await repository.ClaimNextAsync(channelName, maxAttempts, TimeSpan.FromMinutes(staleProcessingMinutes), cancellationToken);
            if (message is null)
                continue;

            var adapter = channelMap[channelName];
            try
            {
                var result = await adapter.SendAsync(message, cancellationToken);
                if (result.Outcome == OutboundDispatchOutcome.Sent)
                {
                    await repository.MarkSentAsync(message.CustomerMessageId, result.ProviderMessageId, DateTime.UtcNow, cancellationToken);
                    logger.LogInformation("Delivered outbound message {MessageId} via {Channel}.", message.CustomerMessageId, channelName);
                    return new OutboundDispatchResult(message.CustomerMessageId, message.EventType, channelName, true, result.ProviderMessageId, null, message.AttemptCount, "Sent");
                }

                var failureReason = string.IsNullOrWhiteSpace(result.FailureReason)
                    ? (result.Retryable ? "Transient outbound failure" : "Permanent outbound failure")
                    : result.FailureReason;

                await repository.MarkFailedAsync(message.CustomerMessageId, failureReason, result.Retryable, cancellationToken);
                logger.LogWarning("Outbound message {MessageId} failed via {Channel}: {FailureReason}.", message.CustomerMessageId, channelName, failureReason);
                return new OutboundDispatchResult(message.CustomerMessageId, message.EventType, channelName, false, result.ProviderMessageId, failureReason, message.AttemptCount, "Failed");
            }
            catch (Exception exception)
            {
                var failureReason = exception.Message;
                await repository.MarkFailedAsync(message.CustomerMessageId, failureReason, true, cancellationToken);
                logger.LogError(exception, "Unhandled outbound dispatch failure for message {MessageId}.", message.CustomerMessageId);
                return new OutboundDispatchResult(message.CustomerMessageId, message.EventType, channelName, false, null, failureReason, message.AttemptCount, "Failed");
            }
        }

        return null;
    }
}
