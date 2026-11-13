using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CashMovementFoundationIntegrationTests
{
    [Fact]
    public async Task OfficialWriter_CreatesOneCashMovement_RetryReturnsSameIdentity_AndDirectWriteIsRejected()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var reference = $"CMF-RETRY-{Guid.NewGuid():N}";
        var paymentId = await InsertPaymentAsync(connection, transaction, reference);
        var posting = await AccountingEventPostingGateway.PostAsync(connection, transaction, AccountingEventType.CustomerAdvance, 25m, paymentId, null, null, null, reference, "Cash movement integration test", CancellationToken.None);
        var cashAccountId = await EnsureActiveCashAccountAsync(connection, transaction);

        var first = await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, cashAccountId, 25m, CancellationToken.None);
        var retry = await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, cashAccountId, 25m, CancellationToken.None);

        Assert.False(first.IsExisting);
        Assert.True(retry.IsExisting);
        Assert.Equal(first.CashMovementId, retry.CashMovementId);
        Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", posting.AccountingEventId));

        await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await using var direct = new SqlCommand("INSERT INTO dbo.CashMovements (CashAccountId, AccountingEventId, CashDirection, Amount, OccurredAt) VALUES (@cashAccountId, @eventId, 1, 1, SYSUTCDATETIME())", connection, transaction);
            direct.Parameters.AddWithValue("@cashAccountId", cashAccountId);
            direct.Parameters.AddWithValue("@eventId", posting.AccountingEventId);
            await direct.ExecuteNonQueryAsync();
        });

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task CallerRollback_RemovesPaymentEventAccountingArtifactsAndCashMovement()
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        var reference = $"CMF-ROLLBACK-{Guid.NewGuid():N}";

        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync())
        {
            var paymentId = await InsertPaymentAsync(connection, transaction, reference);
            var posting = await AccountingEventPostingGateway.PostAsync(connection, transaction, AccountingEventType.CustomerAdvance, 25m, paymentId, null, null, null, reference, "Cash rollback integration test", CancellationToken.None);
            var cashAccountId = await EnsureActiveCashAccountAsync(connection, transaction);
            await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, cashAccountId, 25m, CancellationToken.None);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await CountAsync(connection, null, "SELECT COUNT(*) FROM dbo.Payments WHERE ReferenceNo=@reference", reference));
        Assert.Equal(0, await CountAsync(connection, null, "SELECT COUNT(*) FROM dbo.CashMovements cm INNER JOIN dbo.AccountingEvents ae ON ae.AccountingEventId=cm.AccountingEventId INNER JOIN dbo.FinancialTransactions ft ON ft.AccountingEventId=ae.AccountingEventId WHERE ft.ReferenceNumber=@reference", reference));
    }

    private static async Task<int> InsertPaymentAsync(SqlConnection connection, SqlTransaction transaction, string reference)
    {
        await using var command = new SqlCommand("INSERT INTO dbo.Payments (PaymentDate, Amount, PaymentMethod, ReferenceNo, CreatedDate, PaymentKind) OUTPUT INSERTED.PaymentID VALUES (SYSUTCDATETIME(), 25, N'Cash', @reference, SYSUTCDATETIME(), N'Advance')", connection, transaction);
        command.Parameters.AddWithValue("@reference", reference);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAsync(SqlConnection connection, SqlTransaction? transaction, string sql, object value)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(sql.Contains("@eventId", StringComparison.Ordinal) ? "@eventId" : "@reference", value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> EnsureActiveCashAccountAsync(SqlConnection connection, SqlTransaction transaction)
    {
        var ledgerId = await ReadIntAsync(connection, transaction, "SELECT TOP (1) LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode = N'1000' AND IsActive = 1 ORDER BY LedgerAccountId");
        if (ledgerId <= 0)
            throw new InvalidOperationException("Fabric/Payment tests require the live cash control ledger AccountCode 1000 to exist and be active.");

        var existingCashAccountId = await ReadIntAsync(connection, transaction, "SELECT TOP (1) CashAccountId FROM dbo.CashAccounts WHERE AccountName = N'Cash Movement Test Cash' AND IsActive = 1 AND CashAccountType = 1 AND CurrencyCode = N'YER' AND LedgerControlAccountId = @ledgerId AND AllowsReceipts = 1 ORDER BY CashAccountId",
            "@ledgerId", ledgerId);
        if (existingCashAccountId > 0)
            return existingCashAccountId;

        var insertedCashAccountId = await InsertCashAccountAsync(connection, transaction, ledgerId);
        if (insertedCashAccountId <= 0)
            throw new InvalidOperationException("Unable to provision a valid cash account for cash movement tests.");
        return insertedCashAccountId;
    }

    private static async Task<int> InsertCashAccountAsync(SqlConnection connection, SqlTransaction transaction, int ledgerId)
    {
        await using var command = new SqlCommand(@"
            INSERT INTO dbo.CashAccounts (AccountName, CurrentBalance, IsActive, CreatedAt, CashAccountType, CurrencyCode, LedgerControlAccountId, AllowsReceipts, AllowsDisbursements)
            OUTPUT INSERTED.CashAccountId
            VALUES (N'Cash Movement Test Cash', 0, 1, SYSUTCDATETIME(), 1, N'YER', @ledgerId, 1, 1);", connection, transaction);
        command.Parameters.AddWithValue("@ledgerId", ledgerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ReadIntAsync(SqlConnection connection, SqlTransaction transaction, string sql, string parameterName = "", object? parameterValue = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        if (!string.IsNullOrWhiteSpace(parameterName) && parameterValue is not null)
        {
            command.Parameters.AddWithValue(parameterName, parameterValue);
        }
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP_TEST_R3_ROLLS;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
}