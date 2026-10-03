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
    public void PendingStorageQuery_FallsBackWhenItemCountColumnsAreMissing()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, false, false, false, false, false, false, false);

        Assert.DoesNotContain("i.ItemCount", sql);
        Assert.DoesNotContain("sil.ItemCount", sql);
        Assert.DoesNotContain("i.ReceivedItemCount", sql);
        Assert.DoesNotContain("sil.ReceivedItemCount", sql);
        Assert.Contains("COALESCE(i.ReceivedQuantity, sil.Quantity)", sql);
    }

    [Fact]
    public void PendingStorageQuery_IncludesOfficialImportedProductMetadataWhenAvailable()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, true, true, true, true, true, true, true);

        Assert.Contains("COALESCE(i.ProductType, sil.ProductType, i.ItemType, sil.ItemType)", sql);
        Assert.Contains("COALESCE(i.UnitCode, sil.UnitCode)", sql);
        Assert.Contains("COALESCE(i.ItemCount, sil.ItemCount, i.ReceivedQuantity, sil.Quantity)", sql);
        Assert.Contains("COALESCE(i.ReceivedItemCount, sil.ItemCount, i.ReceivedQuantity, sil.Quantity)", sql);
    }

    [Fact]
    public void PendingStorageQuery_DoesNotUseGenericImportedProductAsProductTypeFallback()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, true, true, true, true, true, true, true);

        Assert.DoesNotContain("CAST(N'ImportedProduct' AS nvarchar(50))", sql);
        Assert.Contains("COALESCE(i.ProductType, sil.ProductType, i.ItemType, sil.ItemType)", sql);
    }
}
