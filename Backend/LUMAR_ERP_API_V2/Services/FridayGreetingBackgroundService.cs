using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class FridayGreetingBackgroundService(
    ICustomerRepository customerRepository,
    IMessageWriteService messageWriteService,
    ISettingsRepository settingsRepository,
    IHijriDateProvider hijriDateProvider,
    ILogger<FridayGreetingBackgroundService> logger) : BackgroundService
{
    public static readonly string SanaaTimeZoneId = "Asia/Aden (UTC+03:00)";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            await TrySendFridayGreetingAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static DateTimeOffset GetSanaaNow(DateTimeOffset utcNow)
        => utcNow.ToOffset(TimeSpan.FromHours(3));

    public static bool ShouldSendForSanaaDate(DayOfWeek dayOfWeek, TimeSpan timeOfDay)
        => dayOfWeek == DayOfWeek.Friday && timeOfDay >= TimeSpan.FromHours(1);

    private async Task TrySendFridayGreetingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sanaaNow = GetSanaaNow(DateTimeOffset.UtcNow);
            if (!ShouldSendForSanaaDate(sanaaNow.DayOfWeek, sanaaNow.TimeOfDay))
                return;

            var settings = await settingsRepository.GetMessageSettingsAsync(cancellationToken);
            if (!settings.MessageMasterEnabled || !settings.FridayGreetingEnabled)
                return;

            var localDate = DateOnly.FromDateTime(sanaaNow.Date);
            var fridayReference = $"Friday:{localDate:yyyy-MM-dd}";
            var customers = await customerRepository.GetListAsync(cancellationToken);
            foreach (var customer in customers)
            {
                var details = await customerRepository.GetByIdAsync(customer.CustomerId, cancellationToken);
                if (details is null || !details.IsActive || string.IsNullOrWhiteSpace(details.CustomerName) || string.IsNullOrWhiteSpace(details.PhoneNumber))
                    continue;

                await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                    details.CustomerId,
                    null,
                    "FridayGreeting",
                    "friday-greeting",
                    details.PhoneNumber,
                    fridayReference,
                    "FridayGreeting",
                    new Dictionary<string, object?>
                    {
                        ["CustomerName"] = details.CustomerName,
                        ["customer_name"] = details.CustomerName,
                        ["HijriDate"] = hijriDateProvider.FormatForSanaa(localDate),
                        ["CustomerCode"] = details.CustomerCode,
                    }), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Friday greeting scheduling cycle failed safely.");
        }
    }
}
