using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.Repositories;
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
    public void Mobile_employee_scanner_keeps_stage_assignment_guard_for_cutting()
    {
        var result = EmployeeScannerRequestGuard.ValidatePieceStageAssignment(164, "كوت", "Cutting", hasAssignment: false);

        Assert.NotNull(result);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void Mobile_employee_scanner_is_treated_as_authenticated_employee_identity_path()
    {
        Assert.True(EmployeeScannerRequestGuard.IsMobileEmployeeScanner("MOBILE-EMPLOYEE"));
        Assert.True(EmployeeScannerRequestGuard.IsMobileEmployeeScanner("mobile-employee"));
        Assert.False(EmployeeScannerRequestGuard.IsMobileEmployeeScanner("SCN-001"));
    }

    [Fact]
    public void Mobile_employee_scan_uses_authenticated_employee_identity_when_no_physical_scanner_code_is_sent()
    {
        var user = new CurrentUserDto(11, "emp02", "Employee Two", "Employee", true, DateTime.UtcNow.AddMinutes(-5), "Employee", null, 21, null);

        var effectiveScannerCode = EmployeeScannerRequestGuard.NormalizeScannerCodeForAuthenticatedEmployee(null, user);

        Assert.Equal(EmployeeScannerRequestGuard.MobileEmployeeScannerCode, effectiveScannerCode);
        Assert.True(EmployeeScannerRequestGuard.IsMobileEmployeeScanner(effectiveScannerCode));
    }

    [Fact]
    public void Employee_identifier_accepts_numeric_employee_id_and_employee_code_values()
    {
        Assert.Equal(201, ProductionRepository.ResolveEmployeeIdentifier("201"));
        Assert.Equal(21, ProductionRepository.ResolveEmployeeIdentifier("21"));
        Assert.Null(ProductionRepository.ResolveEmployeeIdentifier("MO11"));
        Assert.Null(ProductionRepository.ResolveEmployeeIdentifier("   "));
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
