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
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("The foundation accounting event was not created.");
        return new FoundationPostingResult(reader.GetInt64(0), reader.GetBoolean(1));
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
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("The foundation reversal was not created.");
        return new FoundationPostingResult(reader.GetInt64(0), reader.GetBoolean(1));
    }
}