using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Purchasing;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class Es7SecurityFoundationTests
{
    [Fact]
    public void Reversal_is_allowed_only_for_approved_roles()
    {
        Assert.True(Es7PermissionPolicy.CanReverse(User("System Administrator")));
        Assert.True(Es7PermissionPolicy.CanReverse(User("Authorized Financial Manager")));
        Assert.False(Es7PermissionPolicy.CanReverse(User("Purchasing Manager")));
        Assert.False(Es7PermissionPolicy.CanReverse(User(null)));
    }

    [Fact]
    public void Administrator_has_all_es7_permissions_and_manager_has_reversal_permissions()
    {
        Assert.True(Es7PermissionPolicy.HasPermission(User("System Administrator"), Es7Permission.FinanceAdmin));
        Assert.True(Es7PermissionPolicy.HasPermission(User("Authorized Financial Manager"), Es7Permission.PurchasingReverse));
        Assert.True(Es7PermissionPolicy.HasPermission(User("Authorized Financial Manager"), Es7Permission.FinanceSupplierReverse));
        Assert.False(Es7PermissionPolicy.HasPermission(User("Purchasing Manager"), Es7Permission.PurchasingReverse));
    }

    [Fact]
    public void Actor_identity_is_not_part_of_es7_boundary_requests()
    {
        var requestTypes = new[]
        {
            typeof(CreateSupplierInvoiceRequestDto),
            typeof(CreateSupplierPaymentRequestDto),
            typeof(AllocateSupplierPaymentRequestDto),
            typeof(ReverseSupplierFinancialRequestDto),
            typeof(CreateGoodsReceiptRequestDto),
            typeof(ReverseGoodsReceiptRequestDto)
        };

        foreach (var requestType in requestTypes)
            Assert.DoesNotContain(requestType.GetProperties(), property => property.Name is "CreatedBy" or "ReversedBy");
    }

    [Fact]
    public void Goods_receipt_output_preserves_nullable_purchase_order_contract()
    {
        Assert.Equal(typeof(int?), typeof(GoodsReceiptDto).GetProperty(nameof(GoodsReceiptDto.PurchaseOrderId))!.PropertyType);
    }

    private static CurrentUserDto User(string? role) => new(7, "user", "User", role, true, null);
}