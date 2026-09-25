using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public enum FinancialPostingStatus
{
    PostingCreated,
    TransitionalReferenceMatchVerified,
    TransitionalReferenceConflict,
    InvalidAmount,
    UnsupportedTransactionType,
    MissingLedgerAccount,
    InactiveLedgerAccount,
    InvalidJournalLines,
    EmptyJournal,
    UnbalancedJournal,
    PostingVerificationFailed,
    PostingFailed,
    FlowBlockedForTrackB,
    FlowBlockedByMissingAccountingContract
}

public sealed record FinancialPostingResult(FinancialPostingStatus Status, int? JournalEntryId = null)
{
    public bool IsSuccess => Status is FinancialPostingStatus.PostingCreated or FinancialPostingStatus.TransitionalReferenceMatchVerified;

    public string ArabicMessage => Status switch
    {
        FinancialPostingStatus.InvalidAmount => "مبلغ العملية المالية غير صالح.",
        FinancialPostingStatus.UnsupportedTransactionType => "نوع العملية المالية غير مدعوم.",
        FinancialPostingStatus.MissingLedgerAccount => "الحساب المحاسبي المطلوب غير موجود.",
        FinancialPostingStatus.InactiveLedgerAccount => "الحساب المحاسبي المطلوب غير نشط.",
        FinancialPostingStatus.TransitionalReferenceConflict => "توجد سجلات مالية متعارضة للعملية الحالية.",
        FinancialPostingStatus.FlowBlockedForTrackB => "هذه العملية غير متاحة حتى اكتمال عقد الربط المحاسبي.",
        FinancialPostingStatus.FlowBlockedByMissingAccountingContract => "هذه العملية غير متاحة حتى اعتماد عقدها المحاسبي.",
        _ => "تعذر إكمال الترحيل المحاسبي."
    };

    public void ThrowIfFailure()
    {
        if (!IsSuccess)
            throw new InvalidOperationException(ArabicMessage);
    }
}

public static class FinancialTransactionJournalPoster
{
    private static readonly HashSet<string> SupportedTransactionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CustomerAdvance",
        "CustomerPayment",
        "RevenueRecognized",
        "WipToFinishedGoods",
        "ReadyMadeCost"
    };

    public static async Task<FinancialPostingResult> TryCreateJournalEntryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string referenceNumber,
        string transactionType,
        decimal amount,
        string? description,
        CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (transaction is null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        if (string.IsNullOrWhiteSpace(referenceNumber) || string.IsNullOrWhiteSpace(transactionType))
            return new FinancialPostingResult(FinancialPostingStatus.PostingFailed);

        if (amount <= 0m)
        {
            return new FinancialPostingResult(FinancialPostingStatus.InvalidAmount);
        }

        var blockedStatus = transactionType switch
        {
            "OrderCancellationRefund" or "RevenueReversal" or "CostReversal" => FinancialPostingStatus.FlowBlockedForTrackB,
            "CustomerBalanceWaiver" or "DeliveryCost" => FinancialPostingStatus.FlowBlockedByMissingAccountingContract,
            _ => (FinancialPostingStatus?)null
        };
        if (blockedStatus is not null)
            return new FinancialPostingResult(blockedStatus.Value);

        if (!SupportedTransactionTypes.Contains(transactionType))
        {
            return new FinancialPostingResult(FinancialPostingStatus.UnsupportedTransactionType);
        }

        if (!await HasSingleMatchingFinancialTransactionAsync(connection, transaction, referenceNumber, transactionType, amount, cancellationToken))
            return new FinancialPostingResult(FinancialPostingStatus.TransitionalReferenceConflict);

        var existingJournalIds = await GetJournalEntryIdsAsync(connection, transaction, referenceNumber, cancellationToken);
        if (existingJournalIds.Count > 1)
            return new FinancialPostingResult(FinancialPostingStatus.TransitionalReferenceConflict);
        if (existingJournalIds.Count == 1)
            return await VerifyJournalAsync(connection, transaction, existingJournalIds[0], amount, cancellationToken)
                ? new FinancialPostingResult(FinancialPostingStatus.TransitionalReferenceMatchVerified, existingJournalIds[0])
                : new FinancialPostingResult(FinancialPostingStatus.TransitionalReferenceConflict, existingJournalIds[0]);

        var accountValidation = await ValidateRequiredLedgerAccountsAsync(connection, transaction, transactionType, cancellationToken);
        if (accountValidation is not null)
            return new FinancialPostingResult(accountValidation.Value);

        var lines = await BuildJournalLinesAsync(connection, transaction, transactionType, amount, description ?? string.Empty, cancellationToken);
        var lineValidation = ValidateJournalLines(lines, amount);
        if (lineValidation is not null)
            return new FinancialPostingResult(lineValidation.Value);

        var entryDescription = string.IsNullOrWhiteSpace(description)
            ? $"{transactionType} journal entry for {referenceNumber}"
            : description.Trim();

        var entryDate = DateTime.UtcNow;
        var journalEntryId = await InsertJournalEntryAsync(connection, transaction, referenceNumber, entryDescription, entryDate, cancellationToken);

        foreach (var line in lines)
        {
            await InsertJournalLineAsync(connection, transaction, journalEntryId, line.LedgerAccountId, line.DebitAmount, line.CreditAmount, line.Description, cancellationToken);
        }

        return await VerifyJournalAsync(connection, transaction, journalEntryId, amount, cancellationToken)
            ? new FinancialPostingResult(FinancialPostingStatus.PostingCreated, journalEntryId)
            : new FinancialPostingResult(FinancialPostingStatus.PostingVerificationFailed, journalEntryId);
    }

    private static async Task<bool> HasSingleMatchingFinancialTransactionAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber, string transactionType, decimal amount, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TransactionType, Amount FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @referenceNumber";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            count++;
            if (!string.Equals(reader.GetString(0), transactionType, StringComparison.OrdinalIgnoreCase) || reader.GetDecimal(1) != amount)
                return false;
        }

        return count == 1;
    }

    private static async Task<IReadOnlyList<int>> GetJournalEntryIdsAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber, CancellationToken cancellationToken)
    {
        const string sql = "SELECT JournalEntryId FROM dbo.JournalEntries WITH (UPDLOCK,HOLDLOCK) WHERE ReferenceNumber = @referenceNumber ORDER BY JournalEntryId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var journalIds = new List<int>();
        while (await reader.ReadAsync(cancellationToken))
            journalIds.Add(reader.GetInt32(0));
        return journalIds;
    }

    private static async Task<FinancialPostingStatus?> ValidateRequiredLedgerAccountsAsync(SqlConnection connection, SqlTransaction transaction, string transactionType, CancellationToken cancellationToken)
    {
        var accountCodes = transactionType switch
        {
            "CustomerAdvance" => new[] { "1000", "1160" },
            "CustomerPayment" => new[] { "1000", "1200" },
            "RevenueRecognized" => new[] { "1200", "4200" },
            "WipToFinishedGoods" => new[] { "1110", "1130" },
            "ReadyMadeCost" => new[] { "5200", "1110" },
            _ => []
        };

        foreach (var accountCode in accountCodes)
        {
            const string sql = "SELECT TOP(1) IsActive FROM dbo.LedgerAccounts WHERE AccountCode = @accountCode ORDER BY LedgerAccountId";
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@accountCode", accountCode);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is null) return FinancialPostingStatus.MissingLedgerAccount;
            if (value is not bool isActive || !isActive) return FinancialPostingStatus.InactiveLedgerAccount;
        }

        return null;
    }

    private static FinancialPostingStatus? ValidateJournalLines(IReadOnlyList<JournalLine> lines, decimal amount)
    {
        if (lines.Count == 0) return FinancialPostingStatus.EmptyJournal;
        if (lines.Count < 2) return FinancialPostingStatus.InvalidJournalLines;

        foreach (var line in lines)
        {
            var hasDebit = line.DebitAmount > 0m;
            var hasCredit = line.CreditAmount > 0m;
            if (line.DebitAmount < 0m || line.CreditAmount < 0m || hasDebit == hasCredit)
                return FinancialPostingStatus.InvalidJournalLines;
        }

        var totalDebit = lines.Sum(line => line.DebitAmount);
        var totalCredit = lines.Sum(line => line.CreditAmount);
        if (totalDebit <= 0m || totalCredit <= 0m) return FinancialPostingStatus.InvalidJournalLines;
        return totalDebit == totalCredit && totalDebit == amount ? null : FinancialPostingStatus.UnbalancedJournal;
    }

    private static async Task<bool> VerifyJournalAsync(SqlConnection connection, SqlTransaction transaction, int journalEntryId, decimal amount, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT jel.LedgerAccountId, jel.DebitAmount, jel.CreditAmount, la.IsActive
            FROM dbo.JournalEntryLines jel
            LEFT JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
            WHERE jel.JournalEntryId = @journalEntryId
            ORDER BY jel.JournalEntryLineId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<JournalLine>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(3) || !reader.GetBoolean(3)) return false;
            lines.Add(new JournalLine(reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2), string.Empty));
        }

        return ValidateJournalLines(lines, amount) is null;
    }

    private static async Task<int> InsertJournalEntryAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber, string description, DateTime entryDate, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.JournalEntries (ReferenceNumber, Description, EntryDate, CreatedAt) OUTPUT INSERTED.JournalEntryId VALUES (@referenceNumber, @description, @entryDate, @createdAt)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        command.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@entryDate", entryDate);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : throw new InvalidOperationException("Journal entry was not created.");
    }

    private static async Task<IReadOnlyList<JournalLine>> BuildJournalLinesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string transactionType,
        decimal amount,
        string description,
        CancellationToken cancellationToken)
    {
        return transactionType switch
        {
            "CustomerAdvance" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1000", cancellationToken), amount, 0m, "Advance payment received"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1160", cancellationToken), 0m, amount, "Customer advance recorded")
            },
            "CustomerPayment" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1000", cancellationToken), amount, 0m, "Cash received from customer"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1200", cancellationToken), 0m, amount, "Accounts receivable reduced")
            },
            "CustomerBalanceWaiver" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "4200", cancellationToken), amount, 0m, "Customer balance donated"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1200", cancellationToken), 0m, amount, "Accounts receivable waived")
            },
            "RevenueRecognized" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1200", cancellationToken), amount, 0m, "Revenue recognized"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "4200", cancellationToken), 0m, amount, "Sales revenue recognized")
            },
            "OrderCancellationRefund" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1160", cancellationToken), amount, 0m, "Customer advance refunded"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1000", cancellationToken), 0m, amount, "Cash refund issued")
            },
            "RevenueReversal" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "4200", cancellationToken), amount, 0m, "Revenue reversed"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1200", cancellationToken), 0m, amount, "Accounts receivable reversed")
            },
            "WipToFinishedGoods" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1110", cancellationToken), amount, 0m, "Transfer to finished goods inventory"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1130", cancellationToken), 0m, amount, "Transfer from work in progress")
            },
            "DeliveryCost" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "5200", cancellationToken), amount, 0m, "Delivery cost expense"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1000", cancellationToken), 0m, amount, "Cash payment for delivery cost")
            },
            "ReadyMadeCost" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "5200", cancellationToken), amount, 0m, "Cost of sales for ready-made item"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1110", cancellationToken), 0m, amount, "Reduce finished goods inventory")
            },
            "PayrollPayment" => new[]
            {
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "5200", cancellationToken), amount, 0m, "Payroll expense"),
                new JournalLine(await GetLedgerAccountIdAsync(connection, transaction, "1000", cancellationToken), 0m, amount, "Cash paid to employees")
            },
            _ => throw new InvalidOperationException($"Unsupported transaction type: {transactionType}")
        };
    }

    private static async Task<int> GetLedgerAccountIdAsync(SqlConnection connection, SqlTransaction transaction, string accountCode, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP(1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = @accountCode AND IsActive = 1 ORDER BY LedgerAccountId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@accountCode", accountCode);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : throw new InvalidOperationException($"Ledger account {accountCode} is not active or does not exist.");
    }

    private static async Task InsertJournalLineAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int journalEntryId,
        int ledgerAccountId,
        decimal debitAmount,
        decimal creditAmount,
        string description,
        CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO dbo.JournalEntryLines (JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description) VALUES (@journalEntryId, @ledgerAccountId, @debitAmount, @creditAmount, @description)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        command.Parameters.AddWithValue("@ledgerAccountId", ledgerAccountId);
        command.Parameters.AddWithValue("@debitAmount", debitAmount);
        command.Parameters.AddWithValue("@creditAmount", creditAmount);
        command.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record JournalLine(int LedgerAccountId, decimal DebitAmount, decimal CreditAmount, string Description);
}
