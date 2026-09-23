using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ScannerWriteInfrastructureTests
{
    [Fact]
    public void CreateScanner_Valid_Request_Passes_Validation()
    {
        var request = new CreateScannerDto
        {
            ScannerCode = "SC-1001",
            ScannerName = "ورشة القطع",
            Description = "جهاز مسح رئيسي",
            IsActive = true
        };

        var exception = Record.Exception(() => ScannerWriteValidator.ValidateForCreate(request));

        Assert.Null(exception);
    }

    [Fact]
    public void CreateScanner_Rejects_Missing_Data()
    {
        var request = new CreateScannerDto
        {
            ScannerCode = "   ",
            ScannerName = "",
            Description = "",
            IsActive = false
        };

        var exception = Assert.Throws<ArgumentException>(() => ScannerWriteValidator.ValidateForCreate(request));

        Assert.Contains("ScannerCode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_ScannerCode_Validation_Rejects_Existing_Code()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ScannerWriteValidator.EnsureUniqueCode("SC-1001", new[] { "SC-1001" }, null));

        Assert.Contains("ScannerCode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateScanner_Valid_Request_Passes_Validation()
    {
        var request = new UpdateScannerDto
        {
            ScannerCode = "SC-1002",
            ScannerName = "ورشة الخياطة",
            Description = "ماسح احتياطي",
            IsActive = true
        };

        var exception = Record.Exception(() => ScannerWriteValidator.ValidateForUpdate(request));

        Assert.Null(exception);
    }

    [Fact]
    public void ActivateScanner_Uses_Active_State()
    {
        Assert.True(ScannerWriteValidator.NormalizeIsActive(true));
    }

    [Fact]
    public void DeactivateScanner_Uses_Inactive_State()
    {
        Assert.False(ScannerWriteValidator.NormalizeIsActive(false));
    }
}
