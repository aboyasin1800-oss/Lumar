using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FinancialPostingRuntimeReliabilityTests
{
    [Fact]
    public async Task Poster_ReturnsExplicitPreInsertFailures_WithoutCreatingJournalEntry()
    {
        var reference = $"POSTING-RUNTIME-{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            var zeroAmount = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection, transaction, reference, "CustomerPayment", 0m, null, CancellationToken.None);
            var unsupported = await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection, transaction, reference, "Unsupported", 100m, null, CancellationToken.None);

            Assert.Equal(FinancialPostingStatus.InvalidAmount, zeroAmount.Status);
            Assert.Equal(FinancialPostingStatus.UnsupportedTransactionType, unsupported.Status);
            Assert.Equal(0, await CountJournalEntriesAsync(connection, transaction, reference));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Fact]
    public async Task AccountingEventFoundation_RejectsDirectCustomerPaymentWrites()
    {
        var reference = $"POSTING-CONFLICT-{Guid.NewGuid():N}";
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            await Assert.ThrowsAsync<SqlException>(() => InsertFinancialTransactionAsync(connection, transaction, reference, "CustomerPayment", 100m));
            Assert.Equal(0, await CountJournalEntriesAsync(connection, transaction, reference));
        }
        finally
        {
            transaction.Rollback();
        }
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private static async Task InsertFinancialTransactionAsync(SqlConnection connection, SqlTransaction transaction, string reference, string transactionType, decimal amount)
    {
        const string sql = "INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt) VALUES (@reference, @transactionType, @amount, N'اختبار موثوقية الترحيل', SYSUTCDATETIME())";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        command.Parameters.AddWithValue("@transactionType", transactionType);
        command.Parameters.AddWithValue("@amount", amount);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountJournalEntriesAsync(SqlConnection connection, SqlTransaction transaction, string reference)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.JournalEntries WHERE ReferenceNumber = @reference", connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}