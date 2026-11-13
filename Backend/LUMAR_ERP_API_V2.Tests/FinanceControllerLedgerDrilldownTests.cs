using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.DTOs.Finance;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FinanceControllerLedgerDrilldownTests
{
    [Fact]
    public async Task GetLedgerAccountDetails_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var controller = new FinanceController(new FakeFinanceService());

        var result = await controller.GetLedgerAccountDetails(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class FakeFinanceService : IFinanceService
    {
        public Task<IReadOnlyList<FinancialTransactionDto>> GetTransactionsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FinancialTransactionDto>>(Array.Empty<FinancialTransactionDto>());
        public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<JournalEntryDto>>(Array.Empty<JournalEntryDto>());
        public Task<JournalEntryDto?> GetJournalEntryAsync(int id, CancellationToken ct) => Task.FromResult<JournalEntryDto?>(null);
        public Task<IReadOnlyList<JournalEntryLineDto>> GetJournalLinesAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<JournalEntryLineDto>>(Array.Empty<JournalEntryLineDto>());
        public Task<IReadOnlyList<LedgerAccountDto>> GetLedgerAccountsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LedgerAccountDto>>(Array.Empty<LedgerAccountDto>());
        public Task<LedgerAccountDto?> GetLedgerAccountAsync(int id, CancellationToken ct) => Task.FromResult<LedgerAccountDto?>(null);
        public Task<IReadOnlyList<JournalEntryLineDto>> GetLedgerAccountLinesAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<JournalEntryLineDto>>(Array.Empty<JournalEntryLineDto>());
        public Task<IReadOnlyList<CashAccountDto>> GetCashAccountsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CashAccountDto>>(Array.Empty<CashAccountDto>());
        public Task<IReadOnlyList<CashMovementDto>> GetCashMovementsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CashMovementDto>>(Array.Empty<CashMovementDto>());
        public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetCustomerLedgerAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<CustomerLedgerEntryDto>>(Array.Empty<CustomerLedgerEntryDto>());
        public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetSupplierLedgerAsync(int id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierLedgerEntryDto>>(Array.Empty<SupplierLedgerEntryDto>());
        public Task<IReadOnlyList<SupplierPaymentDto>> GetSupplierPaymentsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierPaymentDto>>(Array.Empty<SupplierPaymentDto>());
        public Task<IReadOnlyList<SupplierInvoiceDto>> GetSupplierInvoicesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupplierInvoiceDto>>(Array.Empty<SupplierInvoiceDto>());
        public Task<FinancialReconciliationDto> GetReconciliationAsync(CancellationToken ct) => Task.FromResult(new FinancialReconciliationDto(0, 0, 0, 0, 0, 0, 0, 0));
        public Task<FinancialStatementsDto> GetFinancialStatementsAsync(CancellationToken ct) => Task.FromResult(new FinancialStatementsDto(new FinancialBalanceSheetDto(0m, 0m, 0m, 0m, 0m, 0m), new FinancialCashFlowDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, true), new FinancialProfitLossDto(0m, 0m, 0m, 0m, 0m)));
        public Task<FinancialDashboardDto> GetDashboardAsync(CancellationToken ct) => Task.FromResult(new FinancialDashboardDto(0m, 0m, 0m, 0m, 0, 0, 0, 0m, 0m, Array.Empty<FinanceCustomerMetricDto>(), Array.Empty<FinanceCustomerMetricDto>(), Array.Empty<FinancialActivityDto>()));
        public Task<CashReconciliationDto> GetCashReconciliationAsync(CancellationToken ct) => Task.FromResult(new CashReconciliationDto(DateTime.UtcNow, 0m, 0m, 0m, true, "OK"));
    }
}
