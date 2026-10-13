using Microsoft.Extensions.DependencyInjection;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OutboundMessageBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboundMessageBackgroundService> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollIntervalSeconds = configuration.GetValue("OutboundMessaging:PollIntervalSeconds", 30);
        var maxBatchSize = configuration.GetValue("OutboundMessaging:BatchSize", 20);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboundMessageDispatcher>();
                for (var i = 0; i < maxBatchSize; i++)
                {
                    var result = await dispatcher.DispatchNextAsync(stoppingToken);
                    if (result is null)
                        break;

                    if (result.Sent)
                    {
                        logger.LogInformation("Outbound message {MessageId} for event {EventType} was sent via {Channel}.", result.CustomerMessageId, result.EventType, result.Channel);
                    }
                    else
                    {
                        logger.LogWarning("Outbound message {MessageId} for event {EventType} ended in {Status} with reason {FailureReason}.", result.CustomerMessageId, result.EventType, result.DeliveryStatus, result.FailureReason ?? "none");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbound message dispatch loop crashed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
