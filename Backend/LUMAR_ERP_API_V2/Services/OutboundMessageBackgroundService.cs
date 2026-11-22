using LUMAR_ERP_API_V2.Repositories;
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var gateway = scope.ServiceProvider.GetRequiredService<LocalWhatsAppGatewayClient>();
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

                var messages = await messageRepository.GetCustomerMessagesAsync(stoppingToken);
                foreach (var message in messages.Where(message =>
                    message.Channel == "LocalWhatsApp"
                    && (message.DeliveryStatus == "Sent" || message.DeliveryStatus == "Unconfirmed")))
                {
                    LocalWhatsAppAckStatusResponse? ack;
                    if (!string.IsNullOrWhiteSpace(message.ProviderMessageId))
                    {
                        ack = await gateway.AckStatusAsync(message.ProviderMessageId!, stoppingToken);
                    }
                    else
                    {
                        var providerStatus = await gateway.ProviderStatusAsync(message.CustomerMessageId, stoppingToken);
                        if (string.IsNullOrWhiteSpace(providerStatus?.ProviderMessageId)) continue;
                        await messageRepository.MarkSentAsync(message.CustomerMessageId, providerStatus.ProviderMessageId, providerStatus.OccurredAtUtc ?? DateTime.UtcNow, stoppingToken);
                        ack = new LocalWhatsAppAckStatusResponse(
                            providerStatus.ProviderMessageId,
                            providerStatus.AckLevel,
                            providerStatus.Status,
                            providerStatus.OccurredAtUtc);
                    }
                    if (ack?.AckLevel is null || string.IsNullOrWhiteSpace(ack.Status) || string.IsNullOrWhiteSpace(ack.ProviderMessageId)) continue;

                    var normalizedStatus = ack.Status switch
                    {
                        "Delivered" => "Delivered",
                        "Sent" => "Sent",
                        _ => null
                    };
                    if (normalizedStatus is null) continue;

                    await messageRepository.UpdateDeliveryStatusByProviderIdAsync(
                        message.ProviderMessageId!,
                        normalizedStatus,
                        ack.OccurredAtUtc ?? DateTime.UtcNow,
                        null,
                        stoppingToken);
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
