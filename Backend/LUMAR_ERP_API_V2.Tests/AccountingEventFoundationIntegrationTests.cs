using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class AccountingEventFoundationIntegrationTests
{
    [Fact]
    public async Task OfficialWriter_CreatesOneLinkedBalancedPosting_AndRetryReturnsTheSameIdentity()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var reference = $"AEF-RETRY-{Guid.NewGuid():N}";

        var paymentId = await InsertPaymentAsync(connection, transaction, reference, "Advance");
        var first = await AccountingEventPostingGateway.PostAsync(
            connection, transaction, AccountingEventType.CustomerAdvance, 25m, paymentId, null, null, null, reference, "Accounting event integration test", CancellationToken.None);
        var retry = await AccountingEventPostingGateway.PostAsync(
            connection, transaction, AccountingEventType.CustomerAdvance, 25m, paymentId, null, null, null, reference, "Accounting event integration test", CancellationToken.None);

        Assert.False(first.IsExisting);
        Assert.True(retry.IsExisting);
        Assert.Equal(first.AccountingEventId, retry.AccountingEventId);
        Assert.Equal(first.FinancialTransactionId, retry.FinancialTransactionId);
        Assert.Equal(first.JournalEntryId, retry.JournalEntryId);
        Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventId=@accountingEventId", first.AccountingEventId));
        Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE AccountingEventId=@accountingEventId AND Amount=25", first.AccountingEventId));
        Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.JournalEntries WHERE AccountingEventId=@accountingEventId", first.AccountingEventId));
        Assert.Equal(0m, await DifferenceAsync(connection, transaction, first.JournalEntryId));

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task OfficialWriter_RejectsUnsupportedEventTypeAndPaymentKind()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var reference = $"AEF-REJECT-{Guid.NewGuid():N}";
        var paymentId = await InsertPaymentAsync(connection, transaction, reference, "Refund");

        await Assert.ThrowsAsync<SqlException>(() => AccountingEventPostingGateway.PostAsync(
            connection, transaction, (AccountingEventType)99, 25m, null, null, null, null, reference, "Unsupported event", CancellationToken.None));
        await Assert.ThrowsAsync<SqlException>(() => AccountingEventPostingGateway.PostAsync(
            connection, transaction, AccountingEventType.CustomerPayment, 25m, paymentId, null, null, null, reference, "Unsupported payment kind", CancellationToken.None));

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task CallerRollback_RemovesOperationalSourceAndEveryAccountingArtifact()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        var reference = $"AEF-ROLLBACK-{Guid.NewGuid():N}";

        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            var paymentId = await InsertPaymentAsync(connection, transaction, reference, "Advance");
            await AccountingEventPostingGateway.PostAsync(
                connection, transaction, AccountingEventType.CustomerAdvance, 25m, paymentId, null, null, null, reference, "Rollback integration test", CancellationToken.None);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await CountAsync(connection, null, "SELECT COUNT(*) FROM dbo.Payments WHERE ReferenceNo=@reference", reference));
        Assert.Equal(0, await CountAsync(connection, null, "SELECT COUNT(*) FROM dbo.AccountingEvents ae INNER JOIN dbo.FinancialTransactions ft ON ft.AccountingEventId=ae.AccountingEventId WHERE ft.ReferenceNumber=@reference", reference));
        Assert.Equal(0, await CountAsync(connection, null, "SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber=@reference", reference));
    }

    [Fact]
    public async Task FutureOnlyChecks_RejectDirectFinancialAndJournalWriters()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        var reference = $"AEF-DIRECT-{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await using var command = new SqlCommand(@"
                INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt)
                VALUES (@reference, N'CustomerAdvance', 25, N'Direct write must be rejected', SYSUTCDATETIME());", connection);
            command.Parameters.AddWithValue("@reference", reference);
            await command.ExecuteNonQueryAsync();
        });

        await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await using var command = new SqlCommand(@"
                INSERT INTO dbo.JournalEntries (ReferenceNumber, Description, EntryDate, CreatedAt)
                VALUES (@reference, N'Direct write must be rejected', SYSUTCDATETIME(), SYSUTCDATETIME());", connection);
            command.Parameters.AddWithValue("@reference", reference);
            await command.ExecuteNonQueryAsync();
        });
    }

    private static async Task<int> InsertPaymentAsync(SqlConnection connection, SqlTransaction transaction, string reference, string paymentKind)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.Payments (PaymentDate, Amount, PaymentMethod, ReferenceNo, CreatedDate, PaymentKind)
            OUTPUT INSERTED.PaymentID
            VALUES (SYSUTCDATETIME(), 25, N'Cash', @reference, SYSUTCDATETIME(), @paymentKind);", connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@paymentKind", paymentKind);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAsync(SqlConnection connection, SqlTransaction? transaction, string sql, object value)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        if (sql.Contains("@accountingEventId", StringComparison.Ordinal))
            command.Parameters.AddWithValue("@accountingEventId", value);
        else
            command.Parameters.AddWithValue("@reference", value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> DifferenceAsync(SqlConnection connection, SqlTransaction transaction, int journalEntryId)
    {
        await using var command = new SqlCommand("SELECT COALESCE(SUM(DebitAmount), 0) - COALESCE(SUM(CreditAmount), 0) FROM dbo.JournalEntryLines WHERE JournalEntryId=@journalEntryId", connection, transaction);
        command.Parameters.AddWithValue("@journalEntryId", journalEntryId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
}