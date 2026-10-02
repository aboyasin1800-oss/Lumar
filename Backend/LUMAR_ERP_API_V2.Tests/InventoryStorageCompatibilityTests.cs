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

    [Fact]
    public void PendingStorageQuery_UsesFallbackWhenColumnsAreMissing()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(false, false, false, false, false, false, false);

        Assert.Contains("CAST(N'Legacy' AS nvarchar(30)) AS ItemType", sql);
        Assert.Contains("CAST(NULL AS int) AS RollCount", sql);
        Assert.Contains("1 = 0", sql);
    }

    [Fact]
    public void PendingStorageQuery_IncludesOfficialImportedProductMetadataWhenAvailable()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, true, true, true, true);

        Assert.Contains("COALESCE(i.ProductType, sil.ProductType)", sql);
        Assert.Contains("COALESCE(i.UnitCode, sil.UnitCode)", sql);
        Assert.Contains("ItemCount", sql);
        Assert.Contains("ReceivedItemCount", sql);
    }
}
