using LUMAR_ERP_API_V2.DTOs.Finance; using LUMAR_ERP_API_V2.Services; using Microsoft.AspNetCore.Mvc;
namespace LUMAR_ERP_API_V2.Controllers;
[ApiController] [Route("finance")] public sealed class FinanceController(IFinanceService finance) : ControllerBase
{
    [HttpGet("transactions")] public async Task<ActionResult<IReadOnlyList<FinancialTransactionDto>>> GetTransactions(CancellationToken ct) => Ok(await finance.GetTransactionsAsync(ct));
    [HttpGet("journal-entries")] public async Task<ActionResult<IReadOnlyList<JournalEntryDto>>> GetJournals(CancellationToken ct) => Ok(await finance.GetJournalEntriesAsync(ct));
    [HttpGet("journal-entries/{id:int}")] public async Task<ActionResult<JournalEntryDto>> GetJournal(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Journal entry id must be positive."); var item = await finance.GetJournalEntryAsync(id, ct); return item is null ? NotFound() : Ok(item); }
    [HttpGet("journal-entries/{id:int}/lines")] public async Task<ActionResult<IReadOnlyList<JournalEntryLineDto>>> GetJournalLines(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Journal entry id must be positive."); if (await finance.GetJournalEntryAsync(id, ct) is null) return NotFound(); return Ok(await finance.GetJournalLinesAsync(id, ct)); }
    [HttpGet("ledger-accounts")] public async Task<ActionResult<IReadOnlyList<LedgerAccountDto>>> GetLedgerAccounts(CancellationToken ct) => Ok(await finance.GetLedgerAccountsAsync(ct));
    [HttpGet("cash-accounts")] public async Task<ActionResult<IReadOnlyList<CashAccountDto>>> GetCashAccounts(CancellationToken ct) => Ok(await finance.GetCashAccountsAsync(ct));
    [HttpGet("cash-movements")] public async Task<ActionResult<IReadOnlyList<CashMovementDto>>> GetCashMovements(CancellationToken ct) => Ok(await finance.GetCashMovementsAsync(ct));
    [HttpGet("customers/{id:int}/ledger")] public async Task<ActionResult<IReadOnlyList<CustomerLedgerEntryDto>>> GetCustomerLedger(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Customer id must be positive."); return Ok(await finance.GetCustomerLedgerAsync(id, ct)); }
    [HttpGet("suppliers/{id:int}/ledger")] public async Task<ActionResult<IReadOnlyList<SupplierLedgerEntryDto>>> GetSupplierLedger(int id, CancellationToken ct) { if (id <= 0) return BadRequest("Supplier id must be positive."); return Ok(await finance.GetSupplierLedgerAsync(id, ct)); }
    [HttpGet("supplier-payments")] public async Task<ActionResult<IReadOnlyList<SupplierPaymentDto>>> GetSupplierPayments(CancellationToken ct) => Ok(await finance.GetSupplierPaymentsAsync(ct));
    [HttpGet("supplier-invoices")] public async Task<ActionResult<IReadOnlyList<SupplierInvoiceDto>>> GetSupplierInvoices(CancellationToken ct) => Ok(await finance.GetSupplierInvoicesAsync(ct));
    [HttpGet("reconciliation")] public async Task<ActionResult<FinancialReconciliationDto>> GetReconciliation(CancellationToken ct) => Ok(await finance.GetReconciliationAsync(ct));
    [HttpGet("dashboard")] public async Task<ActionResult<FinancialDashboardDto>> GetDashboard(CancellationToken ct) => Ok(await finance.GetDashboardAsync(ct));
    [HttpGet("cash-reconciliation")] public async Task<ActionResult<CashReconciliationDto>> GetCashReconciliation(CancellationToken ct) => Ok(await finance.GetCashReconciliationAsync(ct));
    [HttpGet("statements")] public async Task<ActionResult<FinancialStatementsDto>> GetStatements(CancellationToken ct) => Ok(await finance.GetFinancialStatementsAsync(ct));
    [HttpPost] [HttpPut("{id:int}")] [HttpDelete("{id:int}")] [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)] public IActionResult WriteDisabled() => StatusCode(StatusCodes.Status405MethodNotAllowed, "Finance writes are disabled while LUMAR_ERP is read-only.");
}