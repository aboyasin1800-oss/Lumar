using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public sealed record FoundationPostingRequest(
    byte AccountingEventType,
    decimal Amount,
    string SourceType,
    long SourceId,
    Guid SourceOperationId,
    string ReferenceNumber,
    string Description,
    string CreatedBy,
    int? CashAccountId = null,
    string? RecipientType = null,
    long? RecipientId = null,
    string CurrencyCode = "YER");

public sealed record FoundationPostingResult(long AccountingEventId, bool IsExisting);

public static class FoundationPostingGateway
{
        internal static async Task<FoundationPostingResult> ReverseFabricPartialAsync(
                SqlConnection connection, SqlTransaction transaction, long reversalId, long originalEventId,
                Guid operationId, string reference, string reason, string actor, CancellationToken cancellationToken)
        {
                const string sql = """
                        SET XACT_ABORT ON;
                        DECLARE @amount decimal(18,2), @originalAmount decimal(18,2);
                        SELECT @amount=reversal.PostingAmount,@originalAmount=original.PostingAmount
                        FROM dbo.FabricConsumptionPartialReversals reversal WITH(UPDLOCK,HOLDLOCK)
                        JOIN dbo.FabricConsumptionSources source ON source.FabricConsumptionSourceId=reversal.FabricConsumptionSourceId
                        JOIN dbo.AccountingEvents original WITH(UPDLOCK,HOLDLOCK) ON original.AccountingEventId=source.AccountingEventId
                        WHERE reversal.FabricConsumptionPartialReversalId=@reversalId AND reversal.SourceOperationId=@operationId
                            AND original.AccountingEventId=@originalId AND original.AccountingEventType=8 AND original.Status=N'Posted';
                        IF @amount IS NULL OR @amount<=0 OR @amount>@originalAmount
                            THROW 51960,N'Invalid original fabric consumption reversal source.',1;
                        IF NOT EXISTS(SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=33 AND IsEnabled=1 AND IsBusinessRuntimeEnabled=0)
                            THROW 51961,N'The official reversal definition is disabled.',1;
                        IF EXISTS(SELECT 1 FROM dbo.AccountingEvents WHERE OriginalAccountingEventId=@originalId AND SourceType<>N'FabricConsumptionPartialReversal')
                            THROW 51962,N'The original fabric event was already reversed.',1;
                        DECLARE @originalJournal int,@debitAccount int,@creditAccount int;
                        SELECT @originalJournal=JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@originalId;
                        SELECT @debitAccount=MAX(CASE WHEN DebitAmount=@originalAmount AND CreditAmount=0 THEN LedgerAccountId END),
                                     @creditAccount=MAX(CASE WHEN CreditAmount=@originalAmount AND DebitAmount=0 THEN LedgerAccountId END)
                        FROM dbo.JournalEntryLines WHERE JournalEntryId=@originalJournal;
                        IF @debitAccount IS NULL OR @creditAccount IS NULL OR
                             (SELECT COUNT(*) FROM dbo.JournalEntryLines WHERE JournalEntryId=@originalJournal)<>2
                            THROW 51963,N'The original fabric journal does not match its official two-sided posting.',1;
                        INSERT dbo.AccountingEvents(AccountingEventType,PostingAmount,SourceType,SourceId,SourceOperationId,
                                OriginalAccountingEventId,ReversalReason,ReversedBy,ReversedAt,Status)
                        VALUES(33,@amount,N'FabricConsumptionPartialReversal',@reversalId,@operationId,@originalId,@reason,@actor,SYSUTCDATETIME(),N'Posted');
                        DECLARE @eventId bigint=SCOPE_IDENTITY();
                        INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt,AccountingEventId)
                            VALUES(@reference,N'Reversal',@amount,@reason,SYSUTCDATETIME(),@eventId);
                        INSERT dbo.JournalEntries(ReferenceNumber,Description,EntryDate,CreatedAt,AccountingEventId)
                            VALUES(@reference,@reason,SYSUTCDATETIME(),SYSUTCDATETIME(),@eventId);
                        DECLARE @journalId int=SCOPE_IDENTITY();
                        INSERT dbo.JournalEntryLines(JournalEntryId,LedgerAccountId,DebitAmount,CreditAmount,Description)
                            VALUES(@journalId,@creditAccount,@amount,0,@reason),(@journalId,@debitAccount,0,@amount,@reason);
                        SELECT @eventId;
                        """;
                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@reversalId", reversalId);
                command.Parameters.AddWithValue("@originalId", originalEventId);
                command.Parameters.AddWithValue("@operationId", operationId);
                command.Parameters.AddWithValue("@reference", reference);
                command.Parameters.AddWithValue("@reason", reason);
                command.Parameters.AddWithValue("@actor", actor);
                return new FoundationPostingResult(Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)), false);
        }

    public static async Task<FoundationPostingResult> PostAsync(SqlConnection connection, SqlTransaction transaction, FoundationPostingRequest request, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.usp_PostFoundationAccountingEvent", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@AccountingEventType", SqlDbType.TinyInt).Value = request.AccountingEventType;
        var amount = command.Parameters.Add("@PostingAmount", SqlDbType.Decimal); amount.Precision = 18; amount.Scale = 2; amount.Value = request.Amount;
        command.Parameters.AddWithValue("@SourceType", request.SourceType);
        command.Parameters.AddWithValue("@SourceId", request.SourceId);
        command.Parameters.Add("@SourceOperationId", SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        command.Parameters.AddWithValue("@ReferenceNumber", request.ReferenceNumber);
        command.Parameters.AddWithValue("@Description", request.Description);
        command.Parameters.AddWithValue("@CreatedBy", request.CreatedBy);
        command.Parameters.Add("@CurrencyCode", SqlDbType.Char, 3).Value = request.CurrencyCode;
        command.Parameters.Add("@CashAccountId", SqlDbType.Int).Value = request.CashAccountId ?? (object)DBNull.Value;
        command.Parameters.Add("@RecipientType", SqlDbType.NVarChar, 50).Value = request.RecipientType ?? (object)DBNull.Value;
        command.Parameters.Add("@RecipientId", SqlDbType.BigInt).Value = request.RecipientId ?? (object)DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        FoundationPostingResult? result = null;
        do
        {
            if (await reader.ReadAsync(cancellationToken))
                result = new FoundationPostingResult(reader.GetInt64(0), reader.GetBoolean(1));
        }
        while (await reader.NextResultAsync(cancellationToken));

        return result ?? throw new InvalidOperationException("The foundation accounting event was not created.");
    }

    public static async Task<FoundationPostingResult> ReverseAsync(SqlConnection connection, SqlTransaction transaction, long originalEventId, Guid sourceOperationId, string referenceNumber, string reason, string reversedBy, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.usp_ReverseFoundationAccountingEvent", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@OriginalAccountingEventId", originalEventId);
        command.Parameters.Add("@SourceOperationId", SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        command.Parameters.AddWithValue("@ReferenceNumber", referenceNumber);
        command.Parameters.AddWithValue("@Reason", reason);
        command.Parameters.AddWithValue("@ReversedBy", reversedBy);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        FoundationPostingResult? result = null;
        do
        {
            if (await reader.ReadAsync(cancellationToken))
                result = new FoundationPostingResult(reader.GetInt64(0), reader.GetBoolean(1));
        }
        while (await reader.NextResultAsync(cancellationToken));

        return result ?? throw new InvalidOperationException("The foundation reversal was not created.");
    }
}