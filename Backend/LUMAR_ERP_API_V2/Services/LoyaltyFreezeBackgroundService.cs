using Microsoft.Extensions.DependencyInjection;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LoyaltyFreezeBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<LoyaltyFreezeBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<ILoyaltyAccountLifecycleService>();
                await lifecycle.EvaluateAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Loyalty account lifecycle evaluation failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
