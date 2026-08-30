using LUMAR_ERP_API_V2.Data; using LUMAR_ERP_API_V2.DTOs.Finance; using Microsoft.Data.SqlClient;
namespace LUMAR_ERP_API_V2.Repositories;
public sealed class FinanceRepository(ReadOnlySqlConnectionFactory connections) : IFinanceRepository
{
    public Task<IReadOnlyList<FinancialTransactionDto>> GetTransactionsAsync(CancellationToken ct) => QueryAsync("SELECT FinancialTransactionId, ReferenceNumber, TransactionType, Amount, Description, CreatedAt FROM dbo.FinancialTransactions ORDER BY CreatedAt DESC, FinancialTransactionId DESC", null, r => new FinancialTransactionDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.NullableString("Description"), r.GetDateTime(5)), ct);
    public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken ct) => QueryAsync("SELECT JournalEntryId, ReferenceNumber, Description, EntryDate, CreatedAt FROM dbo.JournalEntries ORDER BY EntryDate DESC, JournalEntryId DESC", null, MapJournal, ct);
    public async Task<JournalEntryDto?> GetJournalEntryAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT JournalEntryId, ReferenceNumber, Description, EntryDate, CreatedAt FROM dbo.JournalEntries WHERE JournalEntryId = @id", id, MapJournal, ct)).FirstOrDefault();
    public Task<IReadOnlyList<JournalEntryLineDto>> GetJournalLinesAsync(int id, CancellationToken ct) => QueryAsync("SELECT JournalEntryLineId, JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description FROM dbo.JournalEntryLines WHERE JournalEntryId = @id ORDER BY JournalEntryLineId", id, r => new JournalEntryLineDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4), r.NullableString("Description")), ct);
    public Task<IReadOnlyList<LedgerAccountDto>> GetLedgerAccountsAsync(CancellationToken ct) => QueryAsync("SELECT LedgerAccountId, AccountCode, AccountName, AccountType, IsActive, CreatedAt, UpdatedAt FROM dbo.LedgerAccounts ORDER BY AccountCode, LedgerAccountId", null, r => new LedgerAccountDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetDateTime(5), r.NullableDateTime("UpdatedAt")), ct);
    public Task<IReadOnlyList<CashAccountDto>> GetCashAccountsAsync(CancellationToken ct) => QueryAsync("SELECT CashAccountId, AccountName, CurrentBalance, IsActive, CreatedAt FROM dbo.CashAccounts ORDER BY AccountName, CashAccountId", null, r => new CashAccountDto(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetBoolean(3), r.GetDateTime(4)), ct);
    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetCustomerLedgerAsync(int id, CancellationToken ct) => QueryAsync("SELECT CustomerLedgerEntryId, CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.CustomerLedgerEntries WHERE CustomerID = @id ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC", id, r => new CustomerLedgerEntryDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDateTime(6)), ct);
    public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetSupplierLedgerAsync(int id, CancellationToken ct) => QueryAsync("SELECT SupplierLedgerEntryId, SupplierId, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.SupplierLedgerEntries WHERE SupplierId = @id ORDER BY CreatedAt DESC, SupplierLedgerEntryId DESC", id, r => new SupplierLedgerEntryDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDateTime(6)), ct);
    public Task<IReadOnlyList<SupplierPaymentDto>> GetSupplierPaymentsAsync(CancellationToken ct) => QueryAsync("SELECT SupplierPaymentId, SupplierId, PaymentNumber, PaymentDate, Amount, PaymentMethod, ReferenceNumber, Notes, CreatedAt, JournalEntryId FROM dbo.SupplierPayments ORDER BY PaymentDate DESC, SupplierPaymentId DESC", null, r => new SupplierPaymentDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDateTime(3), r.GetDecimal(4), r.NullableString("PaymentMethod"), r.NullableString("ReferenceNumber"), r.NullableString("Notes"), r.GetDateTime(8), r.NullableInt32("JournalEntryId")), ct);
    public Task<IReadOnlyList<SupplierInvoiceDto>> GetSupplierInvoicesAsync(CancellationToken ct) => QueryAsync("SELECT SupplierInvoiceId, SupplierId, PurchaseOrderId, InvoiceNumber, InvoiceDate, DueDate, TotalAmount, AmountPaid, Status, Notes, CreatedAt FROM dbo.SupplierInvoices ORDER BY InvoiceDate DESC, SupplierInvoiceId DESC", null, r => new SupplierInvoiceDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetDateTime(4), r.GetDateTime(5), r.GetDecimal(6), r.GetDecimal(7), r.GetString(8), r.NullableString("Notes"), r.GetDateTime(10)), ct);
    public async Task<FinancialReconciliationDto> GetReconciliationAsync(CancellationToken ct)
    {
        const string sql = """
            WITH JournalTotals AS (
                SELECT je.JournalEntryId,
                       COALESCE(SUM(jel.DebitAmount), 0) AS DebitTotal,
                       COALESCE(SUM(jel.CreditAmount), 0) AS CreditTotal
                FROM dbo.JournalEntries je
                LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
                GROUP BY je.JournalEntryId
            ), FinancialReferences AS (
                SELECT DISTINCT ReferenceNumber FROM dbo.FinancialTransactions
            ), JournalReferences AS (
                SELECT DISTINCT ReferenceNumber FROM dbo.JournalEntries
            )
            SELECT
                (SELECT COUNT(*) FROM dbo.FinancialTransactions),
                (SELECT COUNT(*) FROM dbo.JournalEntries),
                (SELECT COUNT(*) FROM JournalTotals WHERE DebitTotal = CreditTotal),
                (SELECT COUNT(*) FROM JournalTotals WHERE DebitTotal <> CreditTotal),
                (SELECT COUNT(*) FROM FinancialReferences f INNER JOIN JournalReferences j ON j.ReferenceNumber = f.ReferenceNumber),
                (SELECT COUNT(*) FROM FinancialReferences f LEFT JOIN JournalReferences j ON j.ReferenceNumber = f.ReferenceNumber WHERE j.ReferenceNumber IS NULL),
                (SELECT COUNT(*) FROM JournalReferences j LEFT JOIN FinancialReferences f ON f.ReferenceNumber = j.ReferenceNumber WHERE f.ReferenceNumber IS NULL),
                (SELECT COUNT(*) FROM dbo.JournalEntryLines l LEFT JOIN dbo.JournalEntries e ON e.JournalEntryId = l.JournalEntryId WHERE e.JournalEntryId IS NULL)
            """;
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new FinancialReconciliationDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7));
    }
    private static JournalEntryDto MapJournal(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.NullableString("Description"), reader.GetDateTime(3), reader.GetDateTime(4));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, int? id, Func<SqlDataReader, T> map, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id.HasValue) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}