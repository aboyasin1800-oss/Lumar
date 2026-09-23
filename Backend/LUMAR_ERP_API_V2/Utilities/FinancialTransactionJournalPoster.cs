using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public static class FinancialTransactionJournalPoster
{
    private static readonly HashSet<string> SupportedTransactionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CustomerAdvance",
        "CustomerPayment",
        "CustomerBalanceWaiver",
        "RevenueRecognized",
        "OrderCancellationRefund",
        "RevenueReversal",
        "WipToFinishedGoods",
        "DeliveryCost",
        "ReadyMadeCost",
        "PayrollPayment"
    };

    public static async Task<bool> TryCreateJournalEntryAsync(
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

        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new ArgumentException("A reference number is required.", nameof(referenceNumber));
        }

        if (string.IsNullOrWhiteSpace(transactionType))
        {
            throw new ArgumentException("A transaction type is required.", nameof(transactionType));
        }

        if (amount <= 0m)
        {
            return false;
        }

        if (!SupportedTransactionTypes.Contains(transactionType))
        {
            return false;
        }

        if (await JournalEntryExistsAsync(connection, transaction, referenceNumber, cancellationToken))
        {
            return false;
        }

        var entryDescription = string.IsNullOrWhiteSpace(description)
            ? $"{transactionType} journal entry for {referenceNumber}"
            : description.Trim();

        var entryDate = DateTime.UtcNow;
        var journalEntryId = await InsertJournalEntryAsync(connection, transaction, referenceNumber, entryDescription, entryDate, cancellationToken);

        var lines = await BuildJournalLinesAsync(connection, transaction, transactionType, amount, entryDescription, cancellationToken);
        foreach (var line in lines)
        {
            await InsertJournalLineAsync(connection, transaction, journalEntryId, line.LedgerAccountId, line.DebitAmount, line.CreditAmount, line.Description, cancellationToken);
        }

        return true;
    }

    private static async Task<bool> JournalEntryExistsAsync(SqlConnection connection, SqlTransaction transaction, string referenceNumber, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.JournalEntries WHERE ReferenceNumber = @referenceNumber) THEN 1 ELSE 0 END";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@referenceNumber", referenceNumber);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is int intValue ? intValue == 1 : value is bool boolValue && boolValue;
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
