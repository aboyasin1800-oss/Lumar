using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class FabricCodeAllocatorTests
{
    [Fact]
    public void ShouldReturnNextCode_AfterExistingFa0027()
    {
        var next = FabricCodeAllocator.GetNextAvailableCode(new[] { "FA0027" });
        Assert.Equal("FA0028", next);
    }

    [Fact]
    public void ShouldIgnoreImportedAndFabricCodeDuplicates()
    {
        var next = FabricCodeAllocator.GetNextAvailableCode(new[] { "FA0027", "FA0015", "FA0099" });
        Assert.Equal("FA0100", next);
    }

    [Fact]
    public void ShouldNotReuseInactiveOrExpiredCodes()
    {
        var next = FabricCodeAllocator.GetNextAvailableCode(new[] { "FA0001", "FA0003", "FA0027", "FA0099" });
        Assert.Equal("FA0100", next);
    }

    [Fact]
    public void ShouldGenerateSequentialCodes_ForThreeRolls()
    {
        var result = FabricCodeAllocator.GetSequentialCodes(3, new[] { "FA0027" });
        Assert.Equal(new[] { "FA0028", "FA0029", "FA0030" }, result);
    }

    [Fact]
    public void ShouldSkipGaps_WhenDeterminingNextCode()
    {
        var next = FabricCodeAllocator.GetNextAvailableCode(new[] { "FA0025", "FA0027" });
        Assert.Equal("FA0028", next);
    }

    [Fact]
    public void ShouldKeepFabricAndCatalogCountersIndependent()
    {
        var fabricNext = FabricCodeAllocator.GetNextAvailableCode(new[] { "FA0025", "FA0027" }, "FA");
        var catalogNext = FabricCodeAllocator.GetNextAvailableCode(new[] { "CAT0008", "CAT0011" }, "CAT");

        Assert.Equal("FA0028", fabricNext);
        Assert.Equal("CAT0012", catalogNext);
    }
}
