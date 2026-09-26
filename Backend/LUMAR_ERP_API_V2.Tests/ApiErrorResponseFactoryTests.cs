using LUMAR_ERP_API_V2.ErrorHandling;
using Microsoft.Data.SqlClient;
using System.Runtime.CompilerServices;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ApiErrorResponseFactoryTests
{
    [Theory]
    [InlineData("/orders/42/delivery/confirm", "DLV-001")]
    [InlineData("/orders/42/delivery/revenue-recognize", "REV-001")]
    [InlineData("/orders/42/settle", "PAY-001")]
    [InlineData("/orders", "GEN-001")]
    public void Create_MapsOperationalPathsWithoutLeakingExceptionDetails(string path, string expectedCode)
    {
        var response = ApiErrorResponseFactory.Create(path, "correlation-1", new InvalidOperationException("OrderRepository technical detail"));

        Assert.False(response.Success);
        Assert.Equal(expectedCode, response.ErrorCode);
        Assert.Equal("correlation-1", response.CorrelationId);
        Assert.DoesNotContain("OrderRepository", response.UserFriendlyMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", response.UserFriendlyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_MapsSqlExceptionToDatabaseSafeMessage()
    {
        var sqlException = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
        var response = ApiErrorResponseFactory.Create("/orders/42/delivery/confirm", "correlation-2", sqlException);

        Assert.Equal("DB-001", response.ErrorCode);
        Assert.DoesNotContain("Sql", response.UserFriendlyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_MapsTimeoutToNetworkSafeMessage()
    {
        var response = ApiErrorResponseFactory.Create("/orders/42/delivery/confirm", "correlation-3", new TimeoutException("technical timeout"));

        Assert.Equal("NET-001", response.ErrorCode);
        Assert.DoesNotContain("timeout", response.UserFriendlyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateForStatus_MapsExplicitControllerErrorWithoutUsingItsBody()
    {
        var response = ApiErrorResponseFactory.CreateForStatus("/orders/42/delivery/confirm", "correlation-4");

        Assert.False(response.Success);
        Assert.Equal("DLV-001", response.ErrorCode);
        Assert.DoesNotContain("Controller", response.UserFriendlyMessage, StringComparison.OrdinalIgnoreCase);
    }
}