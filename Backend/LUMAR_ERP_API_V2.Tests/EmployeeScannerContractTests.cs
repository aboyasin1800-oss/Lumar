using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeScannerContractTests
{
    [Fact]
    public void Employee_scan_requires_authenticated_employee_account()
    {
        var user = new CurrentUserDto(9, "admin01", "Admin", "Admin", true, DateTime.UtcNow.AddMinutes(-5), "Admin");

        var result = EmployeeScannerRequestGuard.ValidateEmployeeAccount(user);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Employee_scan_requires_mapped_employee_record()
    {
        var user = new CurrentUserDto(10, "emp01", "Employee", "Employee", true, DateTime.UtcNow.AddMinutes(-5), "Employee", null, null, null);

        var result = EmployeeScannerRequestGuard.ValidateEmployeeAccount(user);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Employee_scan_accepts_valid_authenticated_employee()
    {
        var user = new CurrentUserDto(11, "emp02", "Employee Two", "Employee", true, DateTime.UtcNow.AddMinutes(-5), "Employee", null, 21, null);

        var result = EmployeeScannerRequestGuard.ValidateEmployeeAccount(user);

        Assert.Null(result);
    }

    [Fact]
    public void Employee_scan_rejects_cutting_without_piece_stage_assignment()
    {
        var result = EmployeeScannerRequestGuard.ValidatePieceStageAssignment(21, "KOT", "Cutting", hasAssignment: false);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Employee_scan_accepts_authorized_piece_stage_assignment()
    {
        var result = EmployeeScannerRequestGuard.ValidatePieceStageAssignment(21, "KOT", "Sewing", hasAssignment: true);

        Assert.Null(result);
    }

    [Fact]
    public void Scanner_permission_rejects_unapproved_stage_for_product_type()
    {
        var permissions = new[]
        {
            new ScannerStagePermission(3, "Cutting", 12),
            new ScannerStagePermission(3, "Sewing", 12)
        };

        var isAllowed = ScannerPermissionValidator.IsAllowed(12, "Ironing", permissions);

        Assert.False(isAllowed);
    }

    [Fact]
    public void Scanner_permission_accepts_approved_stage_for_product_type()
    {
        var permissions = new[]
        {
            new ScannerStagePermission(3, "Cutting", 12),
            new ScannerStagePermission(3, "Sewing", 12)
        };

        var isAllowed = ScannerPermissionValidator.IsAllowed(12, "Sewing", permissions);

        Assert.True(isAllowed);
    }
}
