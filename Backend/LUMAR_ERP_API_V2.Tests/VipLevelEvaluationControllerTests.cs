using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class VipLevelEvaluationControllerTests
{
    [Fact]
    public async Task GetCustomer_UsesReadOnlyEvaluationPath()
    {
        var service = new SpyVipLevelEvaluationService();
        var controller = new VipLevelEvaluationController(service);

        var result = await controller.EvaluateCustomer(78, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, service.ReadCount);
        Assert.Equal(0, service.WriteCount);
    }

    private sealed class SpyVipLevelEvaluationService : IVipLevelEvaluationService
    {
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }

        public Task<IReadOnlyList<VipLevelEvaluationCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VipLevelEvaluationCriteriaDto>>([]);

        public Task<IReadOnlyList<VipCustomerListItemDto>> GetClassifiedCustomersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VipCustomerListItemDto>>([]);

        public Task<VipLevelEvaluationDto?> GetCustomerEvaluationAsync(int customerId, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<VipLevelEvaluationDto?>(new VipLevelEvaluationDto(
                customerId, "C78", "عميل الاختبار", null, null, null, 1, "BRONZE", "برونزي",
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, "اختبار قراءة", DateTime.UtcNow, false));
        }

        public Task<VipLevelEvaluationDto?> EvaluateCustomerAsync(int customerId, CancellationToken cancellationToken)
        {
            WriteCount++;
            throw new InvalidOperationException("GET must not use the write path.");
        }

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>([]);

        public Task<IReadOnlyList<VipLevelEvaluationDto>> EvaluateNetworkAsync(int customerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VipLevelEvaluationDto>>([]);
    }
}