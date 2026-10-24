using LUMAR_ERP_API_V2.DTOs.WhatsApp;

namespace LUMAR_ERP_API_V2.Services;

public interface IMetaWhatsAppTemplateCatalogClient
{
    Task<IReadOnlyList<MetaWhatsAppTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<MetaWhatsAppTemplateDto?> GetTemplateAsync(string templateName, CancellationToken cancellationToken);
}
