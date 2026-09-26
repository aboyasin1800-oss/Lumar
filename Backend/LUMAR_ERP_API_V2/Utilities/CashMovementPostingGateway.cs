using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public enum CashDirection : byte
{
    CashIn = 1,
    CashOut = 2
}

public sealed record CashMovementPostingResult(long CashMovementId, bool IsExisting);

public static class CashMovementPostingGateway
{
    public static async Task<CashMovementPostingResult> PostCashInAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long accountingEventId,
        int cashAccountId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.usp_PostCashMovement", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@AccountingEventId", SqlDbType.BigInt).Value = accountingEventId;
        command.Parameters.Add("@CashAccountId", SqlDbType.Int).Value = cashAccountId;
        command.Parameters.Add("@CashDirection", SqlDbType.TinyInt).Value = (byte)CashDirection.CashIn;
        var amountParameter = command.Parameters.Add("@Amount", SqlDbType.Decimal);
        amountParameter.Precision = 18;
        amountParameter.Scale = 2;
        amountParameter.Value = amount;
        command.Parameters.Add("@CurrencyCode", SqlDbType.Char, 3).Value = "YER";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("تعذر إنشاء الحركة النقدية الرسمية.");

        return new CashMovementPostingResult(reader.GetInt64(0), reader.GetBoolean(1));
    }
}