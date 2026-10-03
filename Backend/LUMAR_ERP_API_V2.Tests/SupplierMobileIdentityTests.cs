using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierMobileIdentityTests
{
    [Fact]
    public void Customer_account_requires_customer_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Customer", 11, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", 11, 22, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", null, null, 44));
    }

    [Fact]
    public void Employee_account_requires_employee_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Employee", null, 7, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", 4, 7, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", null, null, 44));
    }

    [Fact]
    public void Supplier_account_requires_supplier_owner_only()
    {
        Assert.True(MobileAccountIdentityValidator.IsValid("Supplier", null, null, 88));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", 4, null, 88));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", null, 9, 88));
    }

    [Fact]
    public void Missing_owner_is_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", null, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", null, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", null, null, null));
    }

    [Fact]
    public void Multiple_owners_are_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Customer", 1, 2, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Employee", 1, 2, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("Supplier", 1, null, 2));
    }

    [Fact]
    public void Unsupported_account_types_are_rejected()
    {
        Assert.False(MobileAccountIdentityValidator.IsValid("Admin", 1, null, null));
        Assert.False(MobileAccountIdentityValidator.IsValid("", 1, null, null));
    }

    [Fact]
    public void Account_type_dto_supports_supplier_identity()
    {
        var dto = new AccountTypeDto("Supplier", null, null, 332);

        Assert.Equal("Supplier", dto.AccountType);
        Assert.Null(dto.CustomerId);
        Assert.Null(dto.EmployeeId);
        Assert.Equal(332, dto.SupplierId);
    }

    [Fact]
    public void Mobile_session_dto_supports_supplier_identity()
    {
        var dto = new MobileSessionDto("token", DateTime.UtcNow.AddHours(1), new CurrentUserDto(1, "supplier", "Supplier User", "Supplier", true, null), "Supplier", null, null, 12);

        Assert.Equal("Supplier", dto.AccountType);
        Assert.Equal(12, dto.SupplierId);
    }
}
