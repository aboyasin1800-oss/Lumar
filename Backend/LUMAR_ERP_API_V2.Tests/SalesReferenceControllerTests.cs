using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.SalesReference;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SalesReferenceControllerTests
{
    [Fact]
    public async Task GetVersion_ReturnsTheReferenceVersion()
    {
        var expected = new SalesReferenceVersionResponse("version-1", 1);
        var controller = new SalesReferenceController(new StubService(version: expected));

        var response = await controller.GetVersion(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task GetSnapshot_ReturnsTheReferenceSnapshot()
    {
        var expected = new SalesReferenceSnapshotResponse(
            "version-1",
            1,
            [],
            [],
            []);
        var controller = new SalesReferenceController(new StubService(snapshot: expected));

        var response = await controller.GetSnapshot(CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, result.Value);
    }

    private sealed class StubService(
        SalesReferenceVersionResponse? version = null,
        SalesReferenceSnapshotResponse? snapshot = null) : ISalesReferenceService
    {
        public Task<SalesReferenceVersionResponse> GetVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(version ?? new SalesReferenceVersionResponse("version", 1));

        public Task<SalesReferenceSnapshotResponse> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot ?? new SalesReferenceSnapshotResponse("version", 1, [], [], []));
    }
}