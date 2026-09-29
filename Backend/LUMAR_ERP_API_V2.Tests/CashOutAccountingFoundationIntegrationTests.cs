using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CashOutAccountingFoundationIntegrationTests
{
    [Fact]
    public async Task Foundation_CreatesCashOutBalancedPostingIdempotentlyAndReversesOnce()
    {
        var connectionString = GetConnectionString();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var disabledRequest = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, Guid.NewGuid(), $"ES2-D-{suffix}", "Disabled definition test", "foundation-test", 1, "Employee", 1);
        await AssertDisabledDefinitionRejectedAsync(connectionString, disabledRequest);
        await AssertUnmappedDefinitionRejectedAsync(connectionString, suffix);
        await AssertIdempotencyConflictRejectedAsync(connectionString, suffix);
        await AssertInvalidCashRejectedAsync(connectionString, suffix);
        await AssertDuplicateReversalRejectedAsync(connectionString, suffix);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var cashAccountId = await ConfigureTemporaryFoundationAsync(connection, transaction, suffix);
            var operation = Guid.NewGuid();
            var request = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, operation, $"ES2-{suffix}", "Foundation cash-out test", "foundation-test", cashAccountId, "Employee", 1);

            var created = await FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None);
            var retry = await FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None);
            Assert.False(created.IsExisting);
            Assert.True(retry.IsExisting);
            Assert.Equal(created.AccountingEventId, retry.AccountingEventId);
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE AccountingEventId=@id", created.AccountingEventId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.JournalEntries WHERE AccountingEventId=@id", created.AccountingEventId));
            Assert.Equal(0m, await ScalarDecimalAsync(connection, transaction, "SELECT SUM(DebitAmount-CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@id)", created.AccountingEventId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@id AND CashDirection=2", created.AccountingEventId));

            var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, created.AccountingEventId, Guid.NewGuid(), $"ES2-R-{suffix}", "test reversal", "foundation-test", CancellationToken.None);
            Assert.False(reversal.IsExisting);
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@id AND CashDirection=1", reversal.AccountingEventId));
            await transaction.RollbackAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task AssertDisabledDefinitionRejectedAsync(string connectionString, FoundationPostingRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try { await Assert.ThrowsAsync<SqlException>(() => FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None)); }
        finally { if (transaction.Connection is not null) await transaction.RollbackAsync(); }
    }

    private static async Task AssertUnmappedDefinitionRejectedAsync(string connectionString, string suffix)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var cashAccountId = await ConfigureTemporaryFoundationAsync(connection, transaction, suffix);
            await using var disableMapping = new SqlCommand("UPDATE dbo.AccountRoleMappings SET IsEnabled=0 WHERE AccountRole=N'EmployeeAdvance'", connection, transaction);
            await disableMapping.ExecuteNonQueryAsync();
            var request = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, Guid.NewGuid(), $"ES2-M-{suffix}", "Unmapped definition test", "foundation-test", cashAccountId, "Employee", 1);
            await Assert.ThrowsAsync<SqlException>(() => FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None));
        }
        finally { if (transaction.Connection is not null) await transaction.RollbackAsync(); }
    }

    private static async Task AssertIdempotencyConflictRejectedAsync(string connectionString, string suffix)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var cashAccountId = await ConfigureTemporaryFoundationAsync(connection, transaction, suffix);
            var request = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, Guid.NewGuid(), $"ES2-I-{suffix}", "Idempotency test", "foundation-test", cashAccountId, "Employee", 1);
            await FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None);
            await Assert.ThrowsAsync<SqlException>(() => FoundationPostingGateway.PostAsync(connection, transaction, request with { Amount = 76m }, CancellationToken.None));
        }
        finally { if (transaction.Connection is not null) await transaction.RollbackAsync(); }
    }

    private static async Task AssertInvalidCashRejectedAsync(string connectionString, string suffix)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await ConfigureTemporaryFoundationAsync(connection, transaction, suffix);
            var request = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, Guid.NewGuid(), $"ES2-C-{suffix}", "Invalid cash test", "foundation-test", -1, "Employee", 1);
            await Assert.ThrowsAsync<SqlException>(() => FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None));
        }
        finally { if (transaction.Connection is not null) await transaction.RollbackAsync(); }
    }

    private static async Task AssertDuplicateReversalRejectedAsync(string connectionString, string suffix)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var cashAccountId = await ConfigureTemporaryFoundationAsync(connection, transaction, suffix);
            var request = new FoundationPostingRequest(23, 75m, "EmployeeLedgerEntry", 9001, Guid.NewGuid(), $"ES2-R-{suffix}", "Reversal test", "foundation-test", cashAccountId, "Employee", 1);
            var created = await FoundationPostingGateway.PostAsync(connection, transaction, request, CancellationToken.None);
            var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, created.AccountingEventId, Guid.NewGuid(), $"ES2-R1-{suffix}", "test reversal", "foundation-test", CancellationToken.None);
            Assert.False(reversal.IsExisting);
            await Assert.ThrowsAsync<SqlException>(() => FoundationPostingGateway.ReverseAsync(connection, transaction, created.AccountingEventId, Guid.NewGuid(), $"ES2-R2-{suffix}", "duplicate", "foundation-test", CancellationToken.None));
        }
        finally { if (transaction.Connection is not null) await transaction.RollbackAsync(); }
    }

    private static async Task<int> ConfigureTemporaryFoundationAsync(SqlConnection connection, SqlTransaction transaction, string suffix)
    {
        const string sql = """
            INSERT INTO dbo.LedgerAccounts (AccountCode,AccountName,AccountType,IsActive,CreatedAt) VALUES (@cashCode,N'Cash test',N'Asset',1,SYSUTCDATETIME()),(@advanceCode,N'Advance test',N'Asset',1,SYSUTCDATETIME());
            DECLARE @cashLedgerId int=(SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode=@cashCode);
            DECLARE @advanceLedgerId int=(SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode=@advanceCode);
            UPDATE dbo.AccountRoleMappings SET LedgerAccountId=@cashLedgerId,IsEnabled=1 WHERE AccountRole=N'Cash';
            UPDATE dbo.AccountRoleMappings SET LedgerAccountId=@advanceLedgerId,IsEnabled=1 WHERE AccountRole=N'EmployeeAdvance';
            UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN (23,33);
            INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements)
            OUTPUT INSERTED.CashAccountId VALUES(N'ES2 test cash',0,1,SYSUTCDATETIME(),1,N'YER',@cashLedgerId,1,1);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@cashCode", $"ES2C-{suffix}");
        command.Parameters.AddWithValue("@advanceCode", $"ES2A-{suffix}");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAsync(SqlConnection connection, SqlTransaction transaction, string sql, long id)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@id", id); return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(SqlConnection connection, SqlTransaction transaction, string sql, long id)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@id", id); return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString") ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase) && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Phase ES-2 tests require an approved ES validation database.");
        return builder.ConnectionString;
    }
}