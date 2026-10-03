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
    public void PendingStorageQuery_UsesProductTypeAsSourceOfTruthForCommercialType()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, true, true, true, true, true, true, true);

        Assert.Contains("COALESCE(i.ProductType, sil.ProductType)", sql);
        Assert.DoesNotContain("COALESCE(i.ProductType, sil.ProductType, i.ItemType, sil.ItemType)", sql);
        Assert.DoesNotContain("COALESCE(i.ProductType, i.ItemType)", sql);
        Assert.DoesNotContain("COALESCE(sil.ProductType, sil.ItemType)", sql);
        Assert.Contains("COALESCE(i.UnitCode, sil.UnitCode)", sql);
        Assert.Contains("COALESCE(i.ItemCount, sil.ItemCount, i.ReceivedQuantity, sil.Quantity)", sql);
        Assert.Contains("COALESCE(i.ReceivedItemCount, sil.ItemCount, i.ReceivedQuantity, sil.Quantity)", sql);
    }

    [Fact]
    public void PendingStorageQuery_DoesNotUseTechnicalItemTypeAsCommercialProductTypeFallback()
    {
        var sql = InventoryRepository.BuildPendingGoodsReceiptStorageQuery(true, true, true, true, true, true, true, true, true, true);

        Assert.Contains("COALESCE(i.ProductType, sil.ProductType) AS ProductType", sql);
        Assert.DoesNotContain("COALESCE(i.ItemType, sil.ItemType) AS ProductType", sql);
        Assert.DoesNotContain("COALESCE(sil.ItemType, i.ItemType) AS ProductType", sql);
    }

    [Fact]
    public void UsedToolInventoryQuery_UsesOfficialStorageAllocationIdentity()
    {
        var sql = InventoryRepository.BuildOfficialUsedToolItemPredicate("i");

        Assert.Contains("dbo.GoodsReceiptItemStorageAllocations sga", sql);
        Assert.Contains("sga.ItemType = N'UsedTool'", sql);
        Assert.DoesNotContain("Category LIKE", sql);
        Assert.DoesNotContain("ItemName LIKE", sql);
    }

    [Fact]
    public void UsedToolInventoryQuery_ExposesCommercialCategoryValuesWithoutFilteringOnThem()
    {
        var sql = InventoryRepository.BuildOfficialUsedToolItemPredicate("i");

        Assert.Contains("sga.InventoryItemId = i.InventoryItemID", sql);
        Assert.DoesNotContain("i.Category = N'UsedTool'", sql);
        Assert.DoesNotContain("i.Category LIKE", sql);
    }
}
