using System.Text.Json;
using LUMAR_ERP_API_V2.DTOs.Settings;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class TestDataResetResultDtoTests
{
    [Fact]
    public void TestDataResetExecutionResultDto_UsesExpectedJsonContract()
    {
        var dto = new TestDataResetExecutionResultDto(
            Success: false,
            Message: "تعذر إكمال تنظيف بيانات الاختبار.",
            DatabaseName: "LUMAR_ERP_ES_VALIDATION_R3",
            Details: "Execute failed.",
            Verified: false,
            FailureStage: "EXECUTE_GUARD",
            FailureCode: "EXECUTE_STAGE_FAILED",
            CorrelationId: "abc123",
            TimestampUtc: new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero));

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("success", out var success));
        Assert.False(success.GetBoolean());
        Assert.True(root.TryGetProperty("verified", out var verified));
        Assert.False(verified.GetBoolean());
        Assert.True(root.TryGetProperty("databaseName", out var db));
        Assert.Equal("LUMAR_ERP_ES_VALIDATION_R3", db.GetString());
        Assert.True(root.TryGetProperty("failureStage", out var stage));
        Assert.Equal("EXECUTE_GUARD", stage.GetString());
        Assert.True(root.TryGetProperty("failureCode", out var code));
        Assert.Equal("EXECUTE_STAGE_FAILED", code.GetString());
        Assert.True(root.TryGetProperty("correlationId", out var correlationId));
        Assert.Equal("abc123", correlationId.GetString());
        Assert.True(root.TryGetProperty("details", out var details));
        Assert.Equal("Execute failed.", details.GetString());
        Assert.True(root.TryGetProperty("message", out var message));
        Assert.NotNull(message.GetString());
    }

    [Fact]
    public void TestDataResetExecutionResultDto_UsesExecuteFailureStage()
    {
        var dto = new TestDataResetExecutionResultDto(
            Success: false,
            Message: "تنظيف بيانات الاختبار فشل.",
            DatabaseName: "LUMAR_ERP_ES_VALIDATION_R3",
            Details: "Execute stage failed.",
            Verified: false,
            FailureStage: "Execute",
            FailureCode: "EXECUTE_FAILED",
            CorrelationId: "corr-execute",
            TimestampUtc: DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        Assert.Equal("Execute", doc.RootElement.GetProperty("failureStage").GetString());
        Assert.Equal("EXECUTE_FAILED", doc.RootElement.GetProperty("failureCode").GetString());
        Assert.NotEqual(string.Empty, doc.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public void TestDataResetExecutionResultDto_UsesVerifyFailureStage()
    {
        var dto = new TestDataResetExecutionResultDto(
            Success: false,
            Message: "فشل التحقق من النتيجة.",
            DatabaseName: "LUMAR_ERP_ES_VALIDATION_R3",
            Details: "Verify stage failed.",
            Verified: false,
            FailureStage: "Verify",
            FailureCode: "VERIFY_FAILED",
            CorrelationId: "corr-verify",
            TimestampUtc: DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        Assert.Equal("Verify", doc.RootElement.GetProperty("failureStage").GetString());
        Assert.Equal("VERIFY_FAILED", doc.RootElement.GetProperty("failureCode").GetString());
        Assert.NotEqual(string.Empty, doc.RootElement.GetProperty("correlationId").GetString());
    }
}
