using LUMAR_ERP_API_V2.Data; using LUMAR_ERP_API_V2.DTOs.Finance; using Microsoft.Data.SqlClient;
namespace LUMAR_ERP_API_V2.Repositories;
public sealed class FinanceRepository(ReadOnlySqlConnectionFactory connections) : IFinanceRepository
{
    public Task<IReadOnlyList<FinancialTransactionDto>> GetTransactionsAsync(CancellationToken ct) => QueryAsync("SELECT FinancialTransactionId, ReferenceNumber, TransactionType, Amount, Description, CreatedAt FROM dbo.FinancialTransactions ORDER BY CreatedAt DESC, FinancialTransactionId DESC", null, r => new FinancialTransactionDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.NullableString("Description"), r.GetDateTime(5)), ct);
    public Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken ct) => QueryAsync(JournalSummarySql + " ORDER BY je.EntryDate DESC, je.JournalEntryId DESC", null, MapJournal, ct);
    public async Task<JournalEntryDto?> GetJournalEntryAsync(int id, CancellationToken ct) => (await QueryAsync(JournalSummarySql + " HAVING je.JournalEntryId = @id", id, MapJournal, ct)).FirstOrDefault();
    public Task<IReadOnlyList<JournalEntryLineDto>> GetJournalLinesAsync(int id, CancellationToken ct) => QueryAsync("SELECT JournalEntryLineId, JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description FROM dbo.JournalEntryLines WHERE JournalEntryId = @id ORDER BY JournalEntryLineId", id, r => new JournalEntryLineDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetDecimal(4), r.NullableString("Description")), ct);
    public Task<IReadOnlyList<LedgerAccountDto>> GetLedgerAccountsAsync(CancellationToken ct) => QueryAsync("SELECT LedgerAccountId, AccountCode, AccountName, AccountType, IsActive, CreatedAt, UpdatedAt FROM dbo.LedgerAccounts ORDER BY AccountCode, LedgerAccountId", null, r => new LedgerAccountDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.GetDateTime(5), r.NullableDateTime("UpdatedAt")), ct);
    public Task<IReadOnlyList<CashAccountDto>> GetCashAccountsAsync(CancellationToken ct) => QueryAsync("""
        SELECT ca.CashAccountId, ca.AccountName,
               COALESCE(SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END), 0),
               ca.CurrentBalance, ca.IsActive,
               CASE WHEN ca.IsActive = 1 AND ca.CashAccountType = 1 AND ca.AllowsReceipts = 1 AND ca.CurrencyCode IS NOT NULL AND ca.LedgerControlAccountId IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
               ca.CurrencyCode, ca.CreatedAt
        FROM dbo.CashAccounts ca
        LEFT JOIN dbo.CashMovements cm ON cm.CashAccountId = ca.CashAccountId
        GROUP BY ca.CashAccountId, ca.AccountName, ca.CurrentBalance, ca.IsActive, ca.CashAccountType, ca.AllowsReceipts, ca.CurrencyCode, ca.LedgerControlAccountId, ca.CreatedAt
        ORDER BY ca.AccountName, ca.CashAccountId
        """, null, r => new CashAccountDto(r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetBoolean(4), r.GetBoolean(5), r.NullableString("CurrencyCode"), r.GetDateTime(7)), ct);
    public Task<IReadOnlyList<CashMovementDto>> GetCashMovementsAsync(CancellationToken ct) => QueryAsync("SELECT cm.CashMovementId, cm.CashAccountId, ca.AccountName, cm.CashDirection, cm.Amount, cm.OccurredAt, cm.CreatedAt FROM dbo.CashMovements cm INNER JOIN dbo.CashAccounts ca ON ca.CashAccountId = cm.CashAccountId ORDER BY cm.OccurredAt DESC, cm.CashMovementId DESC", null, r => new CashMovementDto(r.GetInt64(0), r.GetInt32(1), r.GetString(2), r.GetByte(3), r.GetDecimal(4), r.GetDateTime(5), r.GetDateTime(6)), ct);
    public async Task<CashReconciliationDto> GetCashReconciliationAsync(CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        const string sql = """
            DECLARE @cutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
                (SELECT CAST(value AS nvarchar(128))
                 FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));
            SELECT @cutoverUtc,
                (SELECT COALESCE(SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END), 0)
                 FROM dbo.CashMovements cm WHERE cm.CreatedAt >= @cutoverUtc),
                (SELECT COALESCE(SUM(jel.DebitAmount - jel.CreditAmount), 0)
                 FROM dbo.JournalEntryLines jel
                 INNER JOIN dbo.JournalEntries je ON je.JournalEntryId = jel.JournalEntryId
                 INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId = je.AccountingEventId
                 INNER JOIN dbo.CashMovements cm ON cm.AccountingEventId = ae.AccountingEventId
                 INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
                 WHERE la.AccountCode = N'1000' AND cm.CreatedAt >= @cutoverUtc);
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var cutoverUtc = reader.GetDateTime(0);
        var cashMovements = reader.GetDecimal(1);
        var generalLedger = reader.GetDecimal(2);
        var difference = cashMovements - generalLedger;
        return new CashReconciliationDto(cutoverUtc, cashMovements, generalLedger, difference, Math.Abs(difference) < 0.005m, "GoLiveCashMovementsOnly");
    }

    public async Task<FinancialDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        const string sql = """
            DECLARE @cashMovementCutoverUtc datetime2(7) = TRY_CONVERT(datetime2(7),
                (SELECT CAST(value AS nvarchar(128))
                 FROM fn_listextendedproperty(N'CashMovementFoundationCutoverUtc', N'SCHEMA', N'dbo', N'TABLE', N'CashMovements', NULL, NULL)));

            SELECT
                COALESCE((SELECT SUM(CASE WHEN ft.TransactionType = N'RevenueRecognized' THEN ft.Amount WHEN ft.TransactionType = N'RevenueReversal' THEN -ft.Amount ELSE 0 END) FROM dbo.FinancialTransactions ft), 0),
                COALESCE((SELECT SUM(p.Amount) FROM dbo.Payments p WHERE p.PaymentKind NOT IN (N'Refund')), 0),
                COALESCE((SELECT SUM(x.BalanceAfterTransaction) FROM (SELECT cle.BalanceAfterTransaction, ROW_NUMBER() OVER (PARTITION BY cle.CustomerID ORDER BY cle.CreatedAt DESC, cle.CustomerLedgerEntryId DESC) AS RowNumber FROM dbo.CustomerLedgerEntries cle) x WHERE x.RowNumber = 1 AND x.BalanceAfterTransaction > 0), 0),
                COALESCE((SELECT SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END) FROM dbo.CashMovements cm WHERE cm.CreatedAt >= @cashMovementCutoverUtc), 0),
                (SELECT COUNT(*) FROM dbo.JournalEntries),
                (SELECT COUNT(*) FROM dbo.FinancialTransactions),
                (SELECT COUNT(DISTINCT CustomerID) FROM dbo.CustomerLedgerEntries),
                COALESCE((SELECT SUM(CASE WHEN ft.TransactionType = N'RevenueRecognized' THEN ft.Amount WHEN ft.TransactionType = N'RevenueReversal' THEN -ft.Amount ELSE 0 END) FROM dbo.FinancialTransactions ft WHERE CAST(ft.CreatedAt AS date) = CAST(SYSUTCDATETIME() AS date)), 0),
                COALESCE((SELECT SUM(CASE WHEN ft.TransactionType = N'RevenueRecognized' THEN ft.Amount WHEN ft.TransactionType = N'RevenueReversal' THEN -ft.Amount ELSE 0 END) FROM dbo.FinancialTransactions ft WHERE YEAR(ft.CreatedAt) = YEAR(SYSUTCDATETIME()) AND MONTH(ft.CreatedAt) = MONTH(SYSUTCDATETIME())), 0);

            WITH LatestBalance AS (
                SELECT cle.CustomerID, cle.BalanceAfterTransaction,
                       ROW_NUMBER() OVER (PARTITION BY cle.CustomerID ORDER BY cle.CreatedAt DESC, cle.CustomerLedgerEntryId DESC) AS RowNumber
                FROM dbo.CustomerLedgerEntries cle
            )
            SELECT TOP (5) c.CustomerID, c.CustomerCode, c.CustomerName, lb.BalanceAfterTransaction
            FROM LatestBalance lb
            INNER JOIN dbo.Customers c ON c.CustomerID = lb.CustomerID
            WHERE lb.RowNumber = 1 AND lb.BalanceAfterTransaction > 0
            ORDER BY lb.BalanceAfterTransaction DESC, c.CustomerID;

            SELECT TOP (5) c.CustomerID, c.CustomerCode, c.CustomerName, SUM(p.Amount)
            FROM dbo.Payments p
            INNER JOIN dbo.Orders o ON o.OrderID = p.OrderID
            INNER JOIN dbo.Customers c ON c.CustomerID = o.CustomerID
            WHERE p.PaymentKind <> N'Refund'
            GROUP BY c.CustomerID, c.CustomerCode, c.CustomerName
            ORDER BY SUM(p.Amount) DESC, c.CustomerID;

            SELECT TOP (12) FinancialTransactionId, ReferenceNumber, TransactionType, Amount, Description, CreatedAt
            FROM dbo.FinancialTransactions
            ORDER BY CreatedAt DESC, FinancialTransactionId DESC;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var revenue = reader.GetDecimal(0);
        var collections = reader.GetDecimal(1);
        var receivables = reader.GetDecimal(2);
        var cashBalance = reader.GetDecimal(3);
        var journalEntries = reader.GetInt32(4);
        var financialTransactions = reader.GetInt32(5);
        var financialCustomers = reader.GetInt32(6);
        var dailyRevenue = reader.GetDecimal(7);
        var monthlyRevenue = reader.GetDecimal(8);

        var topDebtors = new List<FinanceCustomerMetricDto>();
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) topDebtors.Add(new FinanceCustomerMetricDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3)));

        var topCollections = new List<FinanceCustomerMetricDto>();
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) topCollections.Add(new FinanceCustomerMetricDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3)));

        var activities = new List<FinancialActivityDto>();
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) activities.Add(new FinancialActivityDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.NullableString("Description"), reader.GetDateTime(5)));
        return new FinancialDashboardDto(revenue, collections, receivables, cashBalance, journalEntries, financialTransactions, financialCustomers, dailyRevenue, monthlyRevenue, topDebtors, topCollections, activities);
    }
    public Task<IReadOnlyList<CustomerLedgerEntryDto>> GetCustomerLedgerAsync(int id, CancellationToken ct) => QueryAsync("SELECT CustomerLedgerEntryId, CustomerID, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.CustomerLedgerEntries WHERE CustomerID = @id ORDER BY CreatedAt DESC, CustomerLedgerEntryId DESC", id, r => new CustomerLedgerEntryDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDateTime(6)), ct);
    public Task<IReadOnlyList<SupplierLedgerEntryDto>> GetSupplierLedgerAsync(int id, CancellationToken ct) => QueryAsync("SELECT SupplierLedgerEntryId, SupplierId, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt FROM dbo.SupplierLedgerEntries WHERE SupplierId = @id ORDER BY CreatedAt DESC, SupplierLedgerEntryId DESC", id, r => new SupplierLedgerEntryDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDateTime(6)), ct);
    public Task<IReadOnlyList<SupplierPaymentDto>> GetSupplierPaymentsAsync(CancellationToken ct) => QueryAsync("SELECT SupplierPaymentId, SupplierId, PaymentNumber, PaymentDate, Amount, PaymentMethod, ReferenceNumber, Notes, CreatedAt, JournalEntryId FROM dbo.SupplierPayments ORDER BY PaymentDate DESC, SupplierPaymentId DESC", null, r => new SupplierPaymentDto(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetDateTime(3), r.GetDecimal(4), r.NullableString("PaymentMethod"), r.NullableString("ReferenceNumber"), r.NullableString("Notes"), r.GetDateTime(8), r.NullableInt32("JournalEntryId")), ct);
    public Task<IReadOnlyList<SupplierInvoiceDto>> GetSupplierInvoicesAsync(CancellationToken ct) => QueryAsync("SELECT SupplierInvoiceId, SupplierId, PurchaseOrderId, InvoiceNumber, InvoiceDate, DueDate, TotalAmount, AmountPaid, Status, Notes, CreatedAt FROM dbo.SupplierInvoices ORDER BY InvoiceDate DESC, SupplierInvoiceId DESC", null, r => new SupplierInvoiceDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetDateTime(4), r.GetDateTime(5), r.GetDecimal(6), r.GetDecimal(7), r.GetString(8), r.NullableString("Notes"), r.GetDateTime(10)), ct);
    public async Task<FinancialStatementsDto> GetFinancialStatementsAsync(CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);

        const string ledgerSql = @"
            SELECT la.AccountCode, la.AccountType,
                   COALESCE(SUM(jl.DebitAmount), 0),
                   COALESCE(SUM(jl.CreditAmount), 0)
            FROM dbo.LedgerAccounts la
            LEFT JOIN dbo.JournalEntryLines jl ON jl.LedgerAccountId = la.LedgerAccountId
            WHERE la.IsActive = 1
            GROUP BY la.AccountCode, la.AccountType;";
        var balances = new Dictionary<string, (string Type, decimal Balance)>(StringComparer.OrdinalIgnoreCase);
        await using (var ledgerCommand = new SqlCommand(ledgerSql, connection))
        await using (var reader = await ledgerCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var accountCode = reader.GetString(0);
                var accountType = reader.GetString(1);
                var debit = reader.GetDecimal(2);
                var credit = reader.GetDecimal(3);
                var normalBalance = accountType.Equals("Asset", StringComparison.OrdinalIgnoreCase)
                    || accountType.Equals("Expense", StringComparison.OrdinalIgnoreCase)
                    ? debit - credit
                    : credit - debit;
                balances[accountCode] = (accountType, normalBalance);
            }
        }

        decimal SumType(string type) => balances.Values
            .Where(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Balance);
        decimal Account(string code) => balances.TryGetValue(code, out var item) ? item.Balance : 0m;

        var assets = SumType("Asset");
        var liabilities = SumType("Liability");
        var explicitEquity = SumType("Equity");
        var equity = explicitEquity != 0m ? explicitEquity : assets - liabilities;
        var accountsReceivable = Account("1200");
        var accountsPayable = Account("2100");
        var inventoryValue = Account("1100") + Account("1110") + Account("1130");

        const string financialSql = @"
            SELECT TransactionType, COALESCE(SUM(Amount), 0)
            FROM dbo.FinancialTransactions
            GROUP BY TransactionType;";
        var financialTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using (var financialCommand = new SqlCommand(financialSql, connection))
        await using (var reader = await financialCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) financialTotals[reader.GetString(0)] = reader.GetDecimal(1);
        }

        decimal Financial(params string[] types) => types.Sum(type => financialTotals.GetValueOrDefault(type));
        var customerCollections = Financial("CustomerPayment");
        var customerAdvances = Financial("CustomerAdvance");
        var refunds = Financial("OrderCancellationRefund");
        var cashAccountsBalance = await ReadDecimalAsync(connection, "SELECT COALESCE(SUM(CASE CashDirection WHEN 1 THEN Amount WHEN 2 THEN -Amount ELSE 0 END), 0) FROM dbo.CashMovements", ct);
        var generalLedgerCashBalance = Account("1000");
        var cashDifference = cashAccountsBalance - generalLedgerCashBalance;

        var revenue = SumType("Revenue");
        var expenses = SumType("Expense");
        var costOfGoodsSold = Account("5200");

        return new FinancialStatementsDto(
            new FinancialBalanceSheetDto(assets, liabilities, accountsReceivable, accountsPayable, inventoryValue, equity),
            new FinancialCashFlowDto(customerCollections, customerAdvances, refunds, cashAccountsBalance, customerCollections + customerAdvances - refunds, generalLedgerCashBalance, cashDifference, Math.Abs(cashDifference) < 0.005m),
            new FinancialProfitLossDto(revenue, expenses, costOfGoodsSold, revenue - costOfGoodsSold, revenue - expenses));
    }

    private static async Task<decimal> ReadDecimalAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(ct);
        return value is decimal decimalValue ? decimalValue : Convert.ToDecimal(value ?? 0m);
    }

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
    private const string JournalSummarySql = """
        SELECT je.JournalEntryId, je.ReferenceNumber, je.Description, je.EntryDate, je.CreatedAt,
               COALESCE(SUM(jel.DebitAmount), 0) AS TotalDebit,
               COALESCE(SUM(jel.CreditAmount), 0) AS TotalCredit,
               COUNT(jel.JournalEntryLineId) AS LineCount
        FROM dbo.JournalEntries je
        LEFT JOIN dbo.JournalEntryLines jel ON jel.JournalEntryId = je.JournalEntryId
        GROUP BY je.JournalEntryId, je.ReferenceNumber, je.Description, je.EntryDate, je.CreatedAt
        """;
    private static JournalEntryDto MapJournal(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.NullableString("Description"), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetInt32(7));
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, int? id, Func<SqlDataReader, T> map, CancellationToken ct) { await using var connection = connections.Create(); await connection.OpenAsync(ct); await using var command = new SqlCommand(sql, connection); if (id.HasValue) command.Parameters.AddWithValue("@id", id.Value); await using var reader = await command.ExecuteReaderAsync(ct); var items = new List<T>(); while (await reader.ReadAsync(ct)) items.Add(map(reader)); return items; }
}