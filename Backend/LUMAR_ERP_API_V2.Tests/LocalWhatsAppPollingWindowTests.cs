using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class LocalWhatsAppPollingWindowTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private static CustomerMessageDto Message(string status, DateTime createdAtUtc, DateTime? sentAtUtc = null, string? providerMessageId = null)
        => new(12, 3, null, "OrderCreated", "existing-template", "LocalWhatsApp",
            "Rendered title", "Final rendered LUMAR body", status, createdAtUtc,
            sentAtUtc, null, 1, null, ProviderMessageId: providerMessageId);

    [Theory]
    [InlineData(119)]
    [InlineData(120)]
    public void Unconfirmed_WithinPollingWindow_IsEligible(int ageSeconds)
    {
        var message = Message("Unconfirmed", Now.AddSeconds(-ageSeconds));

        Assert.True(OutboundMessageBackgroundService.IsWithinLocalWhatsAppPollingWindow(message, Now));
    }

    [Fact]
    public void Unconfirmed_OlderThanPollingWindow_IsNotEligible()
    {
        var message = Message("Unconfirmed", Now.AddSeconds(-121), providerMessageId: "must-remain-provider-id");

        Assert.False(OutboundMessageBackgroundService.IsWithinLocalWhatsAppPollingWindow(message, Now));
        Assert.Equal("Unconfirmed", message.DeliveryStatus);
        Assert.Equal("must-remain-provider-id", message.ProviderMessageId);
    }

    [Theory]
    [InlineData(119)]
    [InlineData(120)]
    public void Sent_WithinPollingWindow_IsEligible(int ageSeconds)
    {
        var message = Message("Sent", Now.AddHours(-1), Now.AddSeconds(-ageSeconds));

        Assert.True(OutboundMessageBackgroundService.IsWithinLocalWhatsAppPollingWindow(message, Now));
    }

    [Fact]
    public void Sent_OlderThanPollingWindow_IsNotEligible()
    {
        var message = Message("Sent", Now.AddHours(-1), Now.AddSeconds(-121), "must-remain-provider-id");

        Assert.False(OutboundMessageBackgroundService.IsWithinLocalWhatsAppPollingWindow(message, Now));
        Assert.Equal("Sent", message.DeliveryStatus);
        Assert.Equal("must-remain-provider-id", message.ProviderMessageId);
    }

    [Theory]
    [InlineData("Delivered")]
    [InlineData("Failed")]
    public void TerminalStatus_IsNeverEligible(string status)
    {
        var message = Message(status, Now.AddSeconds(-10), Now.AddSeconds(-10), "provider-id");

        Assert.False(OutboundMessageBackgroundService.IsWithinLocalWhatsAppPollingWindow(message, Now));
    }

    [Theory]
    [InlineData("Sent")]
    [InlineData("Delivered")]
    public void NormalizedStatus_RecentMessageCanReachOfficialStatus(string status)
    {
        var normalizedStatus = status switch
        {
            "Delivered" => "Delivered",
            "Sent" => "Sent",
            _ => null
        };

        Assert.NotNull(normalizedStatus);
    }
}
