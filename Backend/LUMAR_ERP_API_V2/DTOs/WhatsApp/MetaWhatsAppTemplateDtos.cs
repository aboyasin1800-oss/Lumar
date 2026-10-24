namespace LUMAR_ERP_API_V2.DTOs.WhatsApp;

public sealed record MetaWhatsAppTemplateDto(
    string Id,
    string Name,
    string Status,
    string Language,
    string Category,
    IReadOnlyList<MetaWhatsAppTemplateComponentDto> Components);

public sealed record MetaWhatsAppTemplateComponentDto(
    string Type,
    string? Text,
    string? ParameterName,
    string? Format,
    string? Example,
    string? ImageUrl,
    string? Id,
    int? Index);
