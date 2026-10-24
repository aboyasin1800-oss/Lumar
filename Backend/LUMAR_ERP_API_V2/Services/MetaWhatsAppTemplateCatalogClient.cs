using System.Net.Http.Headers;
using System.Text.Json;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;

namespace LUMAR_ERP_API_V2.Services;

public sealed class MetaWhatsAppTemplateCatalogClient(
    IHttpClientFactory httpClientFactory,
    IWhatsAppConnectionSettingsStore settingsStore,
    ILogger<MetaWhatsAppTemplateCatalogClient>? logger = null) : IMetaWhatsAppTemplateCatalogClient
{
    private const string MessagingAccountId = "1413441637074832";

    public async Task<IReadOnlyList<MetaWhatsAppTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        var runtime = await settingsStore.GetRuntimeSettingsAsync(cancellationToken);

        if (!runtime.Enabled)
            return Array.Empty<MetaWhatsAppTemplateDto>();

        if (string.IsNullOrWhiteSpace(runtime.AccessToken) || string.IsNullOrWhiteSpace(runtime.PhoneNumberId))
            return Array.Empty<MetaWhatsAppTemplateDto>();

        var baseUrl = string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl.TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion.Trim('/');
        var endpoint = $"{baseUrl}/{apiVersion}/{MessagingAccountId}/message_templates?fields=id,name,status,language,category,components";

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", runtime.AccessToken);

        var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger?.LogWarning(
                "MetaTemplateCatalogFetch Failed HttpStatusCode={HttpStatusCode} Endpoint={Endpoint} Message={Message}",
                (int)response.StatusCode,
                endpoint,
                ExtractSafeMetaError(raw));

            return Array.Empty<MetaWhatsAppTemplateDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<MetaWhatsAppTemplateDto>();

            var templates = new List<MetaWhatsAppTemplateDto>();
            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                var status = item.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty;
                var language = item.TryGetProperty("language", out var languageElement) ? languageElement.GetString() ?? string.Empty : string.Empty;
                var category = item.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? string.Empty : string.Empty;

                var components = new List<MetaWhatsAppTemplateComponentDto>();
                if (item.TryGetProperty("components", out var componentsElement) && componentsElement.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var component in componentsElement.EnumerateArray())
                    {
                        if (component.ValueKind != JsonValueKind.Object)
                            continue;

                        var componentType = component.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
                        var text = component.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
                        var format = component.TryGetProperty("format", out var formatElement) ? formatElement.GetString() : null;
                        var example = component.TryGetProperty("example", out var exampleElement) ? exampleElement.GetString() : null;
                        var input = component.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array
                            ? parametersElement
                            : default;

                        var parameterName = default(string?);
                        if (component.TryGetProperty("parameter_name", out var parameterNameElement))
                        {
                            parameterName = parameterNameElement.GetString();
                        }

                        var imageUrl = default(string?);
                        if (component.TryGetProperty("image", out var imageElement) && imageElement.ValueKind == JsonValueKind.Object)
                        {
                            if (imageElement.TryGetProperty("link", out var linkElement))
                            {
                                imageUrl = linkElement.GetString();
                            }
                        }

                        components.Add(new MetaWhatsAppTemplateComponentDto(
                            componentType,
                            text,
                            parameterName,
                            format,
                            example,
                            imageUrl,
                            component.TryGetProperty("id", out var componentIdElement) ? componentIdElement.GetString() : null,
                            index));

                        index++;
                    }
                }

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                {
                    templates.Add(new MetaWhatsAppTemplateDto(id, name, status, language, category, components));
                }
            }

            return templates;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "MetaTemplateCatalogFetch JSON parse failed.");
            return Array.Empty<MetaWhatsAppTemplateDto>();
        }
    }

    public async Task<MetaWhatsAppTemplateDto?> GetTemplateAsync(string templateName, CancellationToken cancellationToken)
    {
        var templates = await GetTemplatesAsync(cancellationToken);
        return templates.FirstOrDefault(x => string.Equals(x.Name, templateName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractSafeMetaError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? string.Empty
                    : string.Empty;
                var code = error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number
                    ? codeElement.GetRawText()
                    : null;
                var subcode = error.TryGetProperty("error_subcode", out var subcodeElement) && subcodeElement.ValueKind == JsonValueKind.Number
                    ? subcodeElement.GetRawText()
                    : null;

                return string.Join(" | ", new[]
                {
                    message,
                    code is null ? null : $"code:{code}",
                    subcode is null ? null : $"subcode:{subcode}"
                }.Where(v => !string.IsNullOrWhiteSpace(v)));
            }
        }
        catch
        {
        }

        return raw.Length > 400 ? raw.Substring(0, 400) + "..." : raw;
    }
}
