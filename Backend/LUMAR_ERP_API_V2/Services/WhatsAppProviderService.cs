using LUMAR_ERP_API_V2.DTOs.WhatsApp;

namespace LUMAR_ERP_API_V2.Services;

public interface IWhatsAppProviderStore
{
    Task<IWhatsAppProviderLease> AcquireAsync(bool exclusive, CancellationToken cancellationToken);
}

public interface IWhatsAppProviderLease : IAsyncDisposable
{
    ActiveWhatsAppProvider Provider { get; }
    Task SetAsync(ActiveWhatsAppProvider provider, CancellationToken cancellationToken);
}

public sealed class WhatsAppProviderConflictException(string message) : Exception(message);

public sealed class WhatsAppProviderService(IWhatsAppProviderStore store, LocalWhatsAppGatewayClient gateway)
{
    public async Task<WhatsAppProviderResponse> GetAsync(CancellationToken cancellationToken)
    {
        await using var lease = await store.AcquireAsync(false, cancellationToken);
        return new(lease.Provider);
    }

    public async Task<WhatsAppProviderResponse> SetAsync(ActiveWhatsAppProvider provider, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentException("قيمة مزود واتساب غير صالحة.");
        await using var lease = await store.AcquireAsync(true, cancellationToken);
        if (provider == ActiveWhatsAppProvider.Local)
        {
            try
            {
                var health = await gateway.HealthAsync(cancellationToken);
                var session = await gateway.StatusAsync(cancellationToken);
                if (!health.Healthy || session.State != "Connected" || !session.Linked || string.IsNullOrWhiteSpace(session.CurrentNumber))
                    throw new WhatsAppProviderConflictException("تعذر تفعيل واتساب المحلي: الخدمة أو الجلسة غير جاهزة. لم يتغير المزود النشط.");
            }
            catch (LocalWhatsAppGatewayException)
            {
                throw new WhatsAppProviderConflictException("تعذر الاتصال بخدمة واتساب المحلي. لم يتغير المزود النشط.");
            }
        }
        await lease.SetAsync(provider, cancellationToken);
        return new(provider);
    }
}