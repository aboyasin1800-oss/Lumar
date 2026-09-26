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

        var first = await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, 1, 25m, CancellationToken.None);
        var retry = await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, 1, 25m, CancellationToken.None);

        Assert.False(first.IsExisting);
        Assert.True(retry.IsExisting);
        Assert.Equal(first.CashMovementId, retry.CashMovementId);
        Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", posting.AccountingEventId));

        await Assert.ThrowsAsync<SqlException>(async () =>
        {
            await using var direct = new SqlCommand("INSERT INTO dbo.CashMovements (CashAccountId, AccountingEventId, CashDirection, Amount, OccurredAt) VALUES (1, @eventId, 1, 1, SYSUTCDATETIME())", connection, transaction);
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
            await CashMovementPostingGateway.PostCashInAsync(connection, transaction, posting.AccountingEventId, 1, 25m, CancellationToken.None);
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

    private static string GetConnectionString() => Environment.GetEnvironmentVariable("Lumar__ConnectionString")
        ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
}