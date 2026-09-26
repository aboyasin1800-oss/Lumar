using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeSalesServiceTests
{
    [Fact]
    public async Task CreateAsync_ProcessesLoyaltyAfterRepositoryCompletes()
    {
        var events = new List<string>();
        var repository = new FakeReadyMadeSalesRepository(events, null);
        var loyalty = new FakeOrderLoyaltyIntegrationService(events);
        var service = new ReadyMadeSalesService(repository, loyalty);

        var result = await service.CreateAsync(new CreateReadyMadeSaleDto
        {
            CustomerId = 7,
            PaymentType = "Cash",
            PaidAmount = 100m,
            Items = [new CreateReadyMadeSaleItemDto { ImportedReadyMadeProductId = 12, Quantity = 1, UnitPrice = 100m }]
        }, CancellationToken.None);

        Assert.Equal(42, result.OrderId);
        Assert.Equal(["repository", "loyalty:42"], events);
    }

    [Fact]
    public async Task CreateAsync_DoesNotProcessLoyaltyWhenRepositoryFails()
    {
        var events = new List<string>();
        var failure = new InvalidOperationException("repository failure");
        var repository = new FakeReadyMadeSalesRepository(events, failure);
        var loyalty = new FakeOrderLoyaltyIntegrationService(events);
        var service = new ReadyMadeSalesService(repository, loyalty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CreateReadyMadeSaleDto
            {
                CustomerId = 7,
                PaymentType = "Cash",
                PaidAmount = 100m,
                Items = [new CreateReadyMadeSaleItemDto { ImportedReadyMadeProductId = 12, Quantity = 1, UnitPrice = 100m }]
            },
            CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(["repository"], events);
    }

    [Fact]
    public async Task CreateAsync_PreservesSelectedEmployeeCodeForRepositorySave()
    {
        var events = new List<string>();
        var repository = new FakeReadyMadeSalesRepository(events, null);
        var loyalty = new FakeOrderLoyaltyIntegrationService(events);
        var service = new ReadyMadeSalesService(repository, loyalty);

        await service.CreateAsync(new CreateReadyMadeSaleDto
        {
            CustomerId = 7,
            EmployeeCode = "EMP-001",
            PaymentType = "Cash",
            PaidAmount = 100m,
            Items = [new CreateReadyMadeSaleItemDto { ImportedReadyMadeProductId = 12, Quantity = 1, UnitPrice = 100m }]
        }, CancellationToken.None);

        Assert.Equal("EMP-001", repository.CapturedEmployeeCode);
    }

    [Fact]
    public void InvoiceDetailInsert_SuppliesManualDetailIdAndLeavesComputedTotalPrice()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "ReadyMadeSalesRepository.cs");
        Assert.True(File.Exists(sourcePath),
            "ReadyMadeSalesRepository.cs was not copied to the test output. Verify the test project content item.");
        var source = File.ReadAllText(sourcePath);
        var start = source.LastIndexOf("INSERT INTO dbo.Invoice_Details", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("WHERE NOT EXISTS", start, StringComparison.Ordinal);
        var insert = source[start..end];
        var generation = source[Math.Max(0, start - 220)..start];

        Assert.Contains("DetailID", insert, StringComparison.Ordinal);
        Assert.Contains("@detailId", insert, StringComparison.Ordinal);
        Assert.Contains("MAX(DetailID)", generation, StringComparison.Ordinal);
        Assert.DoesNotContain("TotalPrice", insert, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@totalPrice", insert, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeReadyMadeSalesRepository(List<string> events, Exception? failure) : IReadyMadeSalesRepository
    {
        public string? CapturedEmployeeCode { get; private set; }

        public Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
        {
            events.Add("repository");
            CapturedEmployeeCode = sale.EmployeeCode;
            if (failure is not null) throw failure;

            return Task.FromResult(new ReadyMadeSaleResultDto(
                42,
                "ORD-000042",
                24,
                "INV-RMS-000042",
                sale.CustomerId,
                "Test customer",
                sale.SaleReference ?? "RMS-test",
                sale.PaymentType,
                100m,
                sale.DiscountAmount,
                100m - sale.DiscountAmount,
                sale.PaidAmount,
                0m,
                []));
        }
    }

    private sealed class FakeOrderLoyaltyIntegrationService(List<string> events) : IOrderLoyaltyIntegrationService
    {
        public Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken)
        {
            events.Add($"loyalty:{orderId}");
            return Task.FromResult<OrderLoyaltyProcessingResult?>(null);
        }

        public Task<OrderLoyaltyProcessingResult> ProcessOrderAsync(int orderId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}