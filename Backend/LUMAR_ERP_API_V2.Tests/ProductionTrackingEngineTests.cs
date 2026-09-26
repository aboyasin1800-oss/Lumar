using System.Reflection;
using System.Text.Json;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class ProductionTrackingEngineTests : IDisposable
{
    private readonly string? originalConnectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString");

    public void Dispose() => Environment.SetEnvironmentVariable("Lumar__ConnectionString", originalConnectionString);

    [Fact]
    public void ShouldNotUseDefaultRoutesWhenDatabaseConfigurationIsUnavailable()
    {
        Environment.SetEnvironmentVariable("Lumar__ConnectionString", "Data Source=127.0.0.1,1;Initial Catalog=InvalidDb;User Id=invalid;Password=invalid;TrustServerCertificate=True;");

        Assert.Empty(ProductionTrackingEngine.GetRoute(4));
        Assert.Empty(ProductionTrackingEngine.GetRoute("SHIRT"));
    }

    [Fact]
    public void ShouldResolveOfficialProductTypeIdentityForArabicAndCodeValues()
    {
        Environment.SetEnvironmentVariable("Lumar__ConnectionString", "Data Source=127.0.0.1,1;Initial Catalog=InvalidDb;User Id=invalid;Password=invalid;TrustServerCertificate=True;");

        Assert.Equal("4", ProductionTrackingEngine.ResolveRouteKey("4"));
        Assert.Equal("7", ProductionTrackingEngine.ResolveRouteKey("7"));
        Assert.Equal(string.Empty, ProductionTrackingEngine.ResolveRouteKey("SHIRT"));
        Assert.Equal(string.Empty, ProductionTrackingEngine.ResolveRouteKey("قميص"));
    }

    [Fact]
    public void ShouldResolvePrintingAsNextStageForNewPiece()
    {
        Environment.SetEnvironmentVariable("Lumar__ConnectionString", "Data Source=127.0.0.1,1;Initial Catalog=InvalidDb;User Id=invalid;Password=invalid;TrustServerCertificate=True;");

        var nextStage = ProductionTrackingEngine.GetNextStage("SHIRT", "New");
        var validation = ProductionTrackingEngine.ValidateTransition("SHIRT", "New", "Printing");

        Assert.Null(nextStage);
        Assert.False(validation.IsAllowed);
    }

    [Fact]
    public void ShouldHonorCustomRouteByRemovingButtonsFromShirt()
    {
        var method = typeof(SettingsRepository).GetMethod("ParseRouteEntry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        {
          "productTypeId": 4,
          "stages": ["Printing", "FabricPrep", "Cutting", "Sewing", "Ironing", "Quality", "Assembly"]
        }
        """);

        var result = method.Invoke(null, [document.RootElement]);
        Assert.NotNull(result);

        var productTypeId = (int)result!.GetType().GetProperty("ProductTypeId")!.GetValue(result)!;
        var stages = (System.Collections.Generic.IReadOnlyList<string>)result.GetType().GetProperty("Stages")!.GetValue(result)!;

        Assert.Equal(4, productTypeId);
        Assert.DoesNotContain("Buttons", stages);
        Assert.Equal(new[] { "Printing", "FabricPrep", "Cutting", "Sewing", "Ironing", "Quality", "Assembly" }, stages);
    }

    [Fact]
    public void ShouldPreserveExplicitlyDisabledProductionRoute()
    {
        var method = typeof(SettingsRepository).GetMethod("ParseRouteEntry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        {
          "productTypeId": 4,
          "isEnabled": false,
          "stages": []
        }
        """);

        var result = method.Invoke(null, [document.RootElement]);
        Assert.NotNull(result);

        var productTypeId = (int)result!.GetType().GetProperty("ProductTypeId")!.GetValue(result)!;
        var stages = (System.Collections.Generic.IReadOnlyList<string>)result.GetType().GetProperty("Stages")!.GetValue(result)!;

        Assert.Equal(4, productTypeId);
        Assert.Empty(stages);
    }

    [Fact]
    public void ShouldNotAssignProductTypeIdToLegacyTextRouteEntries()
    {
        var method = typeof(SettingsRepository).GetMethod("ParseRouteEntry", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        {
          "pieceType": "قميص",
          "stages": ["Printing", "FabricPrep", "Cutting", "Sewing", "Ironing", "Quality", "Assembly"]
        }
        """);

        var result = method.Invoke(null, [document.RootElement]);
        Assert.NotNull(result);

        var productTypeId = (int)result!.GetType().GetProperty("ProductTypeId")!.GetValue(result)!;
        var stages = (System.Collections.Generic.IReadOnlyList<string>)result.GetType().GetProperty("Stages")!.GetValue(result)!;

        Assert.Equal(0, productTypeId);
        Assert.Contains("Printing", stages);
    }

    [Fact]
    public void ShouldAllowZeroRateForNonPayableStage()
    {
        var rate = PieceWageEngine.ResolveRate("SHIRT", "Printing", new[]
        {
            new LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto(1, "SHIRT", "Printing", 0m, true, "No payment for this stage.", DateTime.UtcNow, null)
        });

        var validation = PieceWageEngine.ValidateRecord("SHIRT", "Printing", 0m, "EMP-001", 1m, 0);

        Assert.True(rate.IsValid);
        Assert.Equal(0m, rate.WageRate);
        Assert.True(validation.IsValid);
        Assert.Equal(0m, validation.TotalWage);
    }

    [Fact]
    public void ShouldRejectSkippingFabricPrep()
    {
        Environment.SetEnvironmentVariable("Lumar__ConnectionString", "Data Source=127.0.0.1,1;Initial Catalog=InvalidDb;User Id=invalid;Password=invalid;TrustServerCertificate=True;");

        var validation = ProductionTrackingEngine.ValidateTransition("4", "New", "Cutting");

        Assert.False(validation.IsAllowed);
        Assert.Contains("No active production route", validation.Message);
    }

    [Fact]
    public void ShouldAllowButtonsForConfiguredButtonTypesOnly()
    {
        Environment.SetEnvironmentVariable("Lumar__ConnectionString", "Data Source=127.0.0.1,1;Initial Catalog=InvalidDb;User Id=invalid;Password=invalid;TrustServerCertificate=True;");

        Assert.Empty(ProductionTrackingEngine.GetRoute("SHIRT"));
        Assert.Empty(ProductionTrackingEngine.GetRoute("PANTS"));

        var shirtValidation = ProductionTrackingEngine.ValidateTransition("SHIRT", "Sewing", "Buttons");
        var pantsValidation = ProductionTrackingEngine.ValidateTransition("PANTS", "Sewing", "Ironing");

        Assert.False(shirtValidation.IsAllowed);
        Assert.False(pantsValidation.IsAllowed);
    }

    [Fact]
    public void ShouldRejectDuplicateOrRepeatedStage()
    {
        var duplicate = ProductionTrackingEngine.ValidateTransition("PANTS", "Printing", "Printing");
        var skip = ProductionTrackingEngine.ValidateTransition("PANTS", "Printing", "Cutting");

        Assert.False(duplicate.IsAllowed);
        Assert.False(skip.IsAllowed);
    }

    [Fact]
    public void ShouldSetReadyForDeliveryOnlyWhenAllPiecesReady()
    {
        var result = ProductionTrackingEngine.DetermineOrderStatus(
            completedPieceCount: 3,
            totalRequiredPieces: 3,
            cancelledPieces: 0,
            hasBlockedPieces: false,
            orderStatus: "New");

        Assert.Equal("ReadyForDelivery", result);
    }

    [Fact]
    public void ShouldKeepOrderUnreadyWhenPieceIsStillIncomplete()
    {
        var result = ProductionTrackingEngine.DetermineOrderStatus(
            completedPieceCount: 2,
            totalRequiredPieces: 3,
            cancelledPieces: 0,
            hasBlockedPieces: false,
            orderStatus: "InProduction");

        Assert.Equal("InProduction", result);
    }
}
