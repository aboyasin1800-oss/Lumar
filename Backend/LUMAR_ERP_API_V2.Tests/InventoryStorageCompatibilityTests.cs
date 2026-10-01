using LUMAR_ERP_API_V2.Repositories;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class InventoryStorageCompatibilityTests
{
    [Fact]
    public void NullSourceType_IsCompatibleWithFabricStorage()
    {
        Assert.True(InventoryRepository.IsStorageItemTypeCompatible(null, "Fabric"));
    }

    [Fact]
    public void MatchingSourceType_IsCompatibleWithFabricStorage()
    {
        Assert.True(InventoryRepository.IsStorageItemTypeCompatible("Fabric", "Fabric"));
    }

    [Fact]
    public void DifferentSourceType_IsRejected()
    {
        Assert.False(InventoryRepository.IsStorageItemTypeCompatible("UsedTool", "Fabric"));
    }
}
