using LUMAR_ERP_API_V2.Repositories;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class InventoryToolCodeGenerationTests
{
    [Fact]
    public void BuildNextToolCode_GeneratesNextSequentialCodeAcrossLegacyFormats()
    {
        var existing = new[]
        {
            "AT0001",
            "AT0002",
            "AT-0003",
            "AT_0004",
            "ATX0005",
            "AT9999"
        };

        var next = InventoryRepository.BuildNextToolCode("AT", existing);

        Assert.Equal("AT10000", next);
    }

    [Fact]
    public void BuildNextToolCode_UsesDefaultPrefixWhenPrefixIsBlank()
    {
        var next = InventoryRepository.BuildNextToolCode("", new[] { "AT0001", "AT0002" });

        Assert.Equal("AT0003", next);
    }
}
