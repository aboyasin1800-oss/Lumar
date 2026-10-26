using System.Text.Json;
using System.Text.Json.Serialization;

namespace LUMAR_ERP_API_V2.DTOs.WhatsApp;

[JsonConverter(typeof(WhatsAppProviderJsonConverter))]
public enum ActiveWhatsAppProvider
{
    None,
    Meta,
    Local
}

public sealed class WhatsAppProviderJsonConverter : JsonConverter<ActiveWhatsAppProvider>
{
    public override ActiveWhatsAppProvider Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String ? reader.GetString() switch
        {
            "None" => ActiveWhatsAppProvider.None,
            "Meta" => ActiveWhatsAppProvider.Meta,
            "Local" => ActiveWhatsAppProvider.Local,
            _ => throw new JsonException("A single valid WhatsApp provider is required.")
        } : throw new JsonException("A provider string is required.");

    public override void Write(Utf8JsonWriter writer, ActiveWhatsAppProvider value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException("Invalid WhatsApp provider.");
        writer.WriteStringValue(value.ToString());
    }
}

public static class WhatsAppProviderChannels
{
    public static string? Channel(this ActiveWhatsAppProvider provider) => provider switch
    {
        ActiveWhatsAppProvider.Meta => "WhatsApp",
        ActiveWhatsAppProvider.Local => "LocalWhatsApp",
        ActiveWhatsAppProvider.None => null,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static ActiveWhatsAppProvider FromStoredValue(string? value) => value switch
    {
        null => ActiveWhatsAppProvider.Meta,
        "Meta" => ActiveWhatsAppProvider.Meta,
        "Local" => ActiveWhatsAppProvider.Local,
        "None" => ActiveWhatsAppProvider.None,
        _ => ActiveWhatsAppProvider.None
    };
}

/// <summary>One mutually exclusive provider; extra activation flags are rejected.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetWhatsAppProviderRequest([property: JsonRequired] ActiveWhatsAppProvider Provider);

/// <summary>Activation is independent from the local session's readiness.</summary>
public sealed record WhatsAppProviderResponse(ActiveWhatsAppProvider ActiveProvider);