using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public static class LocalWhatsAppFoundationRegistration
{
    public static IServiceCollection AddWhatsAppProviderIntegration(this IServiceCollection services)
    {
        services.AddScoped<IWhatsAppProviderStore, SettingsRepository>();
        services.AddScoped<WhatsAppProviderService>();
        services.AddScoped<IOutboundMessageChannel>(provider => provider.GetRequiredService<LocalWhatsAppMessageChannel>());
        return services;
    }

    public static IServiceCollection AddLocalWhatsAppFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = new Uri(configuration["LocalWhatsAppGateway:BaseUrl"] ?? "http://127.0.0.1:5094/");
        if (endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" || endpoint.AbsolutePath != "/"
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new InvalidOperationException("Local WhatsApp gateway must use an IPv4 loopback HTTP origin.");

        services.AddTransient<LocalWhatsAppGatewayAuthenticationHandler>();
        services.AddHttpClient<LocalWhatsAppGatewayClient>(http =>
        {
            http.BaseAddress = endpoint;
            http.Timeout = TimeSpan.FromSeconds(30);
            http.MaxResponseContentBufferSize = 65536;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
          .AddHttpMessageHandler<LocalWhatsAppGatewayAuthenticationHandler>();
        services.AddScoped<LocalWhatsAppMessageChannel>();
        return services;
    }
}

public sealed class LocalWhatsAppGatewayAuthenticationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LUMAR", "LocalWhatsAppGateway", "gateway-token");
        try
        {
            var token = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
            if (!Regex.IsMatch(token, "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                throw new HttpRequestException("Local gateway authentication unavailable.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch (IOException)
        {
            throw new HttpRequestException("Local gateway authentication unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new HttpRequestException("Local gateway authentication unavailable.");
        }
        return await base.SendAsync(request, cancellationToken);
    }
}