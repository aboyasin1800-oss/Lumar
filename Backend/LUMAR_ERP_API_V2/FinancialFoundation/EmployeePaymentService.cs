using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public sealed record EmployeePaymentRequest(int EmployeeId, decimal Amount, DateOnly PaymentDate, DateOnly AsOfDate, int CashAccountId, string PaymentMethod, string ReferenceNumber, Guid SourceOperationId, string CreatedBy, string CurrencyCode = "YER");
public sealed record EmployeePaymentResult(long EmployeePaymentId, long AccountingEventId, decimal BalanceBefore, decimal BalanceAfter, bool IsExisting);

public sealed class EmployeePayableBalanceReader
{
    public async Task<decimal> GetAsOfAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COALESCE(SUM(BalanceEffect),0) FROM dbo.EmployeeLedgerEntries WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeId=@employeeId AND EffectiveDate<=@asOfDate AND Status=N'Posted';";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@asOfDate", asOfDate.ToDateTime(TimeOnly.MinValue));
        return Convert.ToDecimal(await command.ExecuteScalarAsync(cancellationToken));
    }
}

public sealed class EmployeePaymentService
{
    private readonly EmployeePayableBalanceReader _balanceReader = new();
    private readonly EmployeeLedgerWriter _ledgerWriter = new();

    public async Task<EmployeePaymentResult> PayAsync(SqlConnection connection, SqlTransaction transaction, EmployeePaymentRequest request, CancellationToken cancellationToken)
    {
        ValidateTransaction(connection, transaction, request);
        var existing = await FindPaymentAsync(connection, transaction, request.SourceOperationId, cancellationToken);
        if (existing is not null) return ValidateRetry(existing.Value, request);
        await ValidateEmployeeAsync(connection, transaction, request.EmployeeId, cancellationToken);
        var balanceBefore = await _balanceReader.GetAsOfAsync(connection, transaction, request.EmployeeId, request.AsOfDate, cancellationToken);
        if (balanceBefore <= 0 || request.Amount > balanceBefore) throw new InvalidOperationException("Employee payment exceeds the official payable balance.");
        var paymentId = await InsertPaymentAsync(connection, transaction, request, balanceBefore, balanceBefore - request.Amount, cancellationToken);
        var ledger = await _ledgerWriter.PostAsync(connection, transaction, request.EmployeeId, new LedgerEntryRequest("EmployeePayment", request.Amount, -request.Amount, DateTime.UtcNow, request.PaymentDate, "EmployeePayment", paymentId, request.SourceOperationId, request.ReferenceNumber, request.CreatedBy), cancellationToken);
        var posting = await FoundationPostingGateway.PostAsync(connection, transaction, new FoundationPostingRequest(26, request.Amount, "EmployeeLedgerEntry", ledger.EntryId, request.SourceOperationId, request.ReferenceNumber, "Employee payment", request.CreatedBy, request.CashAccountId, "Employee", request.EmployeeId, request.CurrencyCode), cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.EmployeePayments SET AccountingEventId=@eventId WHERE EmployeePaymentId=@paymentId", connection, transaction);
        update.Parameters.AddWithValue("@eventId", posting.AccountingEventId); update.Parameters.AddWithValue("@paymentId", paymentId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        return new EmployeePaymentResult(paymentId, posting.AccountingEventId, balanceBefore, balanceBefore - request.Amount, false);
    }

    public async Task<EmployeePaymentResult> ReverseAsync(SqlConnection connection, SqlTransaction transaction, long originalPaymentId, Guid sourceOperationId, string referenceNumber, string reason, string reversedBy, CancellationToken cancellationToken)
    {
        if (connection is null || transaction is null || originalPaymentId <= 0 || sourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reversedBy)) throw new ArgumentException("A complete caller-owned reversal transaction is required.");
        var existing = await FindPaymentAsync(connection, transaction, sourceOperationId, cancellationToken);
        if (existing is not null) return new EmployeePaymentResult(existing.Value.Id, existing.Value.EventId, existing.Value.Before, existing.Value.After, true);
        const string originalSql = "SELECT EmployeeId,PaymentAmount,CashAccountId,PaymentDate,AsOfDate,AccountingEventId,Status,BalanceBefore FROM dbo.EmployeePayments WITH(UPDLOCK,HOLDLOCK) WHERE EmployeePaymentId=@id AND PaymentType=N'Individual';";
        await using var originalCommand = new SqlCommand(originalSql, connection, transaction); originalCommand.Parameters.AddWithValue("@id", originalPaymentId);
        await using var reader = await originalCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetString(6) != "Posted" || reader.IsDBNull(5)) throw new InvalidOperationException("The original employee payment cannot be reversed.");
        var employeeId = reader.GetInt32(0); var amount = reader.GetDecimal(1); var cashAccountId = reader.GetInt32(2); var date = DateOnly.FromDateTime(reader.GetDateTime(3)); var asOf = DateOnly.FromDateTime(reader.GetDateTime(4)); var eventId = reader.GetInt64(5); var before = reader.GetDecimal(7);
        await reader.CloseAsync();
        var balanceBefore = await _balanceReader.GetAsOfAsync(connection, transaction, employeeId, asOf, cancellationToken);
        var reversalId = await InsertReversalAsync(connection, transaction, employeeId, amount, date, asOf, cashAccountId, sourceOperationId, referenceNumber, originalPaymentId, reason, reversedBy, balanceBefore, balanceBefore + amount, cancellationToken);
        var reversalEvent = await FoundationPostingGateway.ReverseAsync(connection, transaction, eventId, sourceOperationId, referenceNumber, reason, reversedBy, cancellationToken);
        var originalEntryId = await FindOriginalLedgerEntryAsync(connection, transaction, originalPaymentId, cancellationToken);
        await _ledgerWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest("Reversal", amount, amount, DateTime.UtcNow, date, "EmployeePayment", reversalId, sourceOperationId, referenceNumber, reversedBy, originalEntryId, reason, reversedBy, DateTime.UtcNow), cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.EmployeePayments SET Status=N'Reversed' WHERE EmployeePaymentId=@id; UPDATE dbo.EmployeePayments SET AccountingEventId=@eventId WHERE EmployeePaymentId=@reversalId;", connection, transaction);
        update.Parameters.AddWithValue("@id", originalPaymentId); update.Parameters.AddWithValue("@reversalId", reversalId); update.Parameters.AddWithValue("@eventId", reversalEvent.AccountingEventId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        return new EmployeePaymentResult(reversalId, reversalEvent.AccountingEventId, balanceBefore, balanceBefore + amount, false);
    }

    private static void ValidateTransaction(SqlConnection connection, SqlTransaction transaction, EmployeePaymentRequest request)
    {
        if (connection is null || transaction is null || request.EmployeeId <= 0 || request.Amount <= 0 || request.CashAccountId <= 0 || request.SourceOperationId == Guid.Empty || request.PaymentDate > request.AsOfDate || string.IsNullOrWhiteSpace(request.ReferenceNumber) || string.IsNullOrWhiteSpace(request.PaymentMethod) || string.IsNullOrWhiteSpace(request.CreatedBy)) throw new ArgumentException("Employee payment is incomplete.");
    }
    private static async Task ValidateEmployeeAsync(SqlConnection c, SqlTransaction t, int id, CancellationToken ct)
    { await using var cmd=new SqlCommand("SELECT IsActive FROM dbo.Employees WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeID=@id",c,t);cmd.Parameters.AddWithValue("@id",id);var value=await cmd.ExecuteScalarAsync(ct);if(value is null||value is DBNull||!Convert.ToBoolean(value))throw new InvalidOperationException("Employee is unavailable for payment."); }
    private static async Task<long> InsertPaymentAsync(SqlConnection c,SqlTransaction t,EmployeePaymentRequest r,decimal before,decimal after,CancellationToken ct)
    { const string sql="INSERT dbo.EmployeePayments(EmployeeId,PaymentAmount,PaymentType,PaymentDate,AsOfDate,CashAccountId,PaymentMethod,CurrencyCode,ReferenceNumber,SourceOperationId,BalanceBefore,BalanceAfter,Status,CreatedBy) OUTPUT inserted.EmployeePaymentId VALUES(@employeeId,@amount,N'Individual',@date,@asOf,@cash,@method,@currency,@reference,@operation,@before,@after,N'Posted',@createdBy);";return await InsertAsync(c,t,sql,r.EmployeeId,r.Amount,r.PaymentDate,r.AsOfDate,r.CashAccountId,r.PaymentMethod,r.CurrencyCode,r.ReferenceNumber,r.SourceOperationId,r.CreatedBy,before,after,ct); }
    private static async Task<long> InsertReversalAsync(SqlConnection c,SqlTransaction t,int employeeId,decimal amount,DateOnly date,DateOnly asOf,int cash,Guid operation,string reference,long original,string reason,string by,decimal before,decimal after,CancellationToken ct)
    { const string sql="INSERT dbo.EmployeePayments(EmployeeId,PaymentAmount,PaymentType,PaymentDate,AsOfDate,CashAccountId,PaymentMethod,CurrencyCode,ReferenceNumber,SourceOperationId,BalanceBefore,BalanceAfter,Status,OriginalPaymentId,ReversalReason,ReversedBy,ReversedAt,CreatedBy) OUTPUT inserted.EmployeePaymentId VALUES(@employeeId,@amount,N'Reversal',@date,@asOf,@cash,N'Reversal',N'YER',@reference,@operation,@before,@after,N'Posted',@original,@reason,@by,SYSUTCDATETIME(),@by);";await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@employeeId",employeeId);cmd.Parameters.AddWithValue("@amount",amount);cmd.Parameters.AddWithValue("@date",date.ToDateTime(TimeOnly.MinValue));cmd.Parameters.AddWithValue("@asOf",asOf.ToDateTime(TimeOnly.MinValue));cmd.Parameters.AddWithValue("@cash",cash);cmd.Parameters.AddWithValue("@reference",reference);cmd.Parameters.Add("@operation",SqlDbType.UniqueIdentifier).Value=operation;cmd.Parameters.AddWithValue("@before",before);cmd.Parameters.AddWithValue("@after",after);cmd.Parameters.AddWithValue("@original",original);cmd.Parameters.AddWithValue("@reason",reason);cmd.Parameters.AddWithValue("@by",by);return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task<long> InsertAsync(SqlConnection c,SqlTransaction t,string sql,int employeeId,decimal amount,DateOnly date,DateOnly asOf,int cash,string method,string currency,string reference,Guid operation,string by,decimal before,decimal after,CancellationToken ct)
    { await using var cmd=new SqlCommand(sql,c,t);cmd.Parameters.AddWithValue("@employeeId",employeeId);cmd.Parameters.AddWithValue("@amount",amount);cmd.Parameters.AddWithValue("@date",date.ToDateTime(TimeOnly.MinValue));cmd.Parameters.AddWithValue("@asOf",asOf.ToDateTime(TimeOnly.MinValue));cmd.Parameters.AddWithValue("@cash",cash);cmd.Parameters.AddWithValue("@method",method);cmd.Parameters.AddWithValue("@currency",currency);cmd.Parameters.AddWithValue("@reference",reference);cmd.Parameters.Add("@operation",SqlDbType.UniqueIdentifier).Value=operation;cmd.Parameters.AddWithValue("@before",before);cmd.Parameters.AddWithValue("@after",after);cmd.Parameters.AddWithValue("@createdBy",by);return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task<(long Id,long EventId,decimal Before,decimal After,int EmployeeId,decimal Amount,int Cash,DateOnly AsOf)?> FindPaymentAsync(SqlConnection c,SqlTransaction t,Guid operation,CancellationToken ct)
    { await using var cmd=new SqlCommand("SELECT EmployeePaymentId,AccountingEventId,BalanceBefore,BalanceAfter,EmployeeId,PaymentAmount,CashAccountId,AsOfDate FROM dbo.EmployeePayments WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operation",c,t);cmd.Parameters.Add("@operation",SqlDbType.UniqueIdentifier).Value=operation;await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;if(r.IsDBNull(1))throw new InvalidOperationException("Existing payment is incomplete.");return(r.GetInt64(0),r.GetInt64(1),r.GetDecimal(2),r.GetDecimal(3),r.GetInt32(4),r.GetDecimal(5),r.GetInt32(6),DateOnly.FromDateTime(r.GetDateTime(7))); }
    private static EmployeePaymentResult ValidateRetry((long Id,long EventId,decimal Before,decimal After,int EmployeeId,decimal Amount,int Cash,DateOnly AsOf) e,EmployeePaymentRequest r)
    { if(e.EmployeeId!=r.EmployeeId||e.Amount!=r.Amount||e.Cash!=r.CashAccountId||e.AsOf!=r.AsOfDate)throw new InvalidOperationException("IDEMPOTENCY CONFLICT");return new(e.Id,e.EventId,e.Before,e.After,true); }
    private static async Task<long> FindOriginalLedgerEntryAsync(SqlConnection c,SqlTransaction t,long paymentId,CancellationToken ct){await using var cmd=new SqlCommand("SELECT EmployeeLedgerEntryId FROM dbo.EmployeeLedgerEntries WITH(UPDLOCK,HOLDLOCK) WHERE SourceType=N'EmployeePayment' AND SourceId=@id AND EntryType=N'EmployeePayment'",c,t);cmd.Parameters.AddWithValue("@id",paymentId);var value=await cmd.ExecuteScalarAsync(ct);return value is null?throw new InvalidOperationException("Original payment ledger entry is unavailable."):Convert.ToInt64(value);}
}