using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public enum SupplierPaymentKind { Immediate, Later, Advance }

public sealed record SupplierFinancialInvoiceRequest(
    int SupplierId, string InvoiceNumber, DateOnly InvoiceDate, DateOnly DueDate, decimal Amount,
    string? Notes, Guid SourceOperationId, string CreatedBy, string CurrencyCode = "YER");

public sealed record SupplierFinancialPaymentRequest(
    int SupplierId, int? SupplierInvoiceId, decimal Amount, DateOnly PaymentDate, int CashAccountId,
    SupplierPaymentKind PaymentKind, string PaymentMethod, string ReferenceNumber, string? Notes,
    Guid SourceOperationId, string CreatedBy, string CurrencyCode = "YER");

public sealed record SupplierFinancialInvoiceResult(int SupplierInvoiceId, long AccountingEventId, decimal AmountPaid, decimal OutstandingAmount, bool IsExisting);
public sealed record SupplierFinancialPaymentResult(int SupplierPaymentId, long AccountingEventId, decimal AmountAllocated, decimal SupplierBalanceAfter, bool IsExisting);
public sealed record SupplierFinancialAllocationResult(int SupplierPaymentAllocationId, long AccountingEventId, decimal Amount, bool IsExisting);

public sealed class SupplierFinancialRuntime
{
    private readonly SupplierLedgerWriter _ledgerWriter = new();

    public async Task<SupplierFinancialInvoiceResult> CreateInvoiceAsync(SqlConnection connection, SqlTransaction transaction, SupplierFinancialInvoiceRequest request, CancellationToken cancellationToken)
    {
        ValidateInvoiceRequest(connection, transaction, request);
        var existing = await FindInvoiceAsync(connection, transaction, request.SourceOperationId, cancellationToken);
        if (existing is not null) return ValidateInvoiceRetry(existing.Value, request);
        await ValidateSupplierAsync(connection, transaction, request.SupplierId, cancellationToken);

        var invoiceId = await InsertInvoiceAsync(connection, transaction, request, cancellationToken);
        var ledger = await _ledgerWriter.PostAsync(connection, transaction, request.SupplierId,
            new LedgerEntryRequest("SupplierInvoice", request.Amount, request.Amount, DateTime.UtcNow, request.InvoiceDate,
                "SupplierInvoice", invoiceId, request.SourceOperationId, request.InvoiceNumber, request.CreatedBy), cancellationToken);
        var posting = await FoundationPostingGateway.PostAsync(connection, transaction,
            new FoundationPostingRequest(28, request.Amount, "SupplierInvoice", invoiceId, request.SourceOperationId,
                request.InvoiceNumber, "Supplier invoice", request.CreatedBy, null, "Supplier", request.SupplierId, request.CurrencyCode), cancellationToken);
        await InsertFinancialInvoiceAsync(connection, transaction, invoiceId, request.SourceOperationId, posting.AccountingEventId, request.CreatedBy, cancellationToken);

        var autoApplied = await ApplyAvailableAdvancesAsync(connection, transaction, request.SupplierId, invoiceId, request.InvoiceDate,
            request.SourceOperationId, request.InvoiceNumber, request.CreatedBy, cancellationToken);
        return new SupplierFinancialInvoiceResult(invoiceId, posting.AccountingEventId, autoApplied, request.Amount - autoApplied, false);
    }

    public async Task<SupplierFinancialPaymentResult> PayAsync(SqlConnection connection, SqlTransaction transaction, SupplierFinancialPaymentRequest request, CancellationToken cancellationToken)
    {
        ValidatePaymentRequest(connection, transaction, request);
        var existing = await FindPaymentAsync(connection, transaction, request.SourceOperationId, cancellationToken);
        if (existing is not null) return ValidatePaymentRetry(existing.Value, request);
        await ValidateSupplierAsync(connection, transaction, request.SupplierId, cancellationToken);
        if (request.PaymentKind != SupplierPaymentKind.Advance)
            await ValidateInvoiceForPaymentAsync(connection, transaction, request.SupplierId, request.SupplierInvoiceId!.Value, request.Amount, cancellationToken);

        var paymentId = await InsertPaymentAsync(connection, transaction, request, cancellationToken);
        var entryType = request.PaymentKind == SupplierPaymentKind.Advance ? "SupplierAdvancePayment" : "SupplierPayment";
        var ledger = await _ledgerWriter.PostAsync(connection, transaction, request.SupplierId,
            new LedgerEntryRequest(entryType, request.Amount, -request.Amount, DateTime.UtcNow, request.PaymentDate,
                "SupplierPayment", paymentId, request.SourceOperationId, request.ReferenceNumber, request.CreatedBy), cancellationToken);
        var eventType = request.PaymentKind switch { SupplierPaymentKind.Immediate => (byte)29, SupplierPaymentKind.Later => (byte)31, _ => (byte)30 };
        var posting = await FoundationPostingGateway.PostAsync(connection, transaction,
            new FoundationPostingRequest(eventType, request.Amount, "SupplierPayment", paymentId, request.SourceOperationId,
                request.ReferenceNumber, $"Supplier {request.PaymentKind.ToString().ToLowerInvariant()} payment", request.CreatedBy,
                request.CashAccountId, "Supplier", request.SupplierId, request.CurrencyCode), cancellationToken);
        await InsertFinancialPaymentAsync(connection, transaction, paymentId, request, posting.AccountingEventId, cancellationToken);

        decimal allocated = 0m;
        if (request.SupplierInvoiceId.HasValue)
        {
            allocated = request.PaymentKind == SupplierPaymentKind.Immediate
                ? await RecordImmediatePaymentAllocationAsync(connection, transaction, paymentId, request.SupplierInvoiceId.Value, request.Amount, request.PaymentDate, cancellationToken)
                : (await AllocateAsync(connection, transaction, paymentId, request.SupplierInvoiceId.Value, request.Amount,
                    request.PaymentDate, request.SourceOperationId, request.ReferenceNumber, request.CreatedBy, cancellationToken)).Amount;
        }
        var balance = await GetSupplierBalanceAsync(connection, transaction, request.SupplierId, cancellationToken);
        return new SupplierFinancialPaymentResult(paymentId, posting.AccountingEventId, allocated, balance, false);
    }

    public async Task<SupplierFinancialAllocationResult> AllocateAsync(SqlConnection connection, SqlTransaction transaction, int supplierPaymentId, int supplierInvoiceId, decimal amount, DateOnly allocationDate, Guid sourceOperationId, string referenceNumber, string createdBy, CancellationToken cancellationToken)
    {
        if (connection is null || transaction is null || supplierPaymentId <= 0 || supplierInvoiceId <= 0 || amount <= 0 || sourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(referenceNumber) || string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("Supplier payment allocation is incomplete.");
        var existing = await FindAllocationAsync(connection, transaction, sourceOperationId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Value.Amount != amount || existing.Value.PaymentId != supplierPaymentId || existing.Value.InvoiceId != supplierInvoiceId)
                throw new InvalidOperationException("IDEMPOTENCY CONFLICT");
            return new SupplierFinancialAllocationResult(existing.Value.Id, existing.Value.EventId, existing.Value.Amount, true);
        }

        var supplierId = await ReadPaymentSupplierAsync(connection, transaction, supplierPaymentId, cancellationToken);
        await ValidateInvoiceForAllocationAsync(connection, transaction, supplierId, supplierInvoiceId, amount, cancellationToken);
        var available = await GetAvailablePaymentAmountAsync(connection, transaction, supplierPaymentId, cancellationToken);
        if (amount > available) throw new InvalidOperationException("Supplier payment allocation exceeds the available payment amount.");
        var allocationId = await InsertAllocationAsync(connection, transaction, supplierPaymentId, supplierInvoiceId, amount, allocationDate, cancellationToken);
        var posting = await FoundationPostingGateway.PostAsync(connection, transaction,
            new FoundationPostingRequest(32, amount, "SupplierPaymentAllocation", allocationId, sourceOperationId, referenceNumber,
                "Supplier payment allocation", createdBy, null, "Supplier", supplierId), cancellationToken);
        await InsertFinancialAllocationAsync(connection, transaction, allocationId, sourceOperationId, posting.AccountingEventId, createdBy, cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.SupplierInvoices SET AmountPaid=AmountPaid+@amount WHERE SupplierInvoiceId=@invoiceId;", connection, transaction);
        update.Parameters.AddWithValue("@amount", amount); update.Parameters.AddWithValue("@invoiceId", supplierInvoiceId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        return new SupplierFinancialAllocationResult(allocationId, posting.AccountingEventId, amount, false);
    }

    private static async Task<decimal> RecordImmediatePaymentAllocationAsync(SqlConnection connection, SqlTransaction transaction, int paymentId, int invoiceId, decimal amount, DateOnly allocationDate, CancellationToken cancellationToken)
    {
        var supplierId = await ReadPaymentSupplierAsync(connection, transaction, paymentId, cancellationToken);
        await ValidateInvoiceForAllocationAsync(connection, transaction, supplierId, invoiceId, amount, cancellationToken);
        var available = await GetAvailablePaymentAmountAsync(connection, transaction, paymentId, cancellationToken);
        if (amount > available) throw new InvalidOperationException("Supplier payment allocation exceeds the available payment amount.");
        await InsertAllocationAsync(connection, transaction, paymentId, invoiceId, amount, allocationDate, cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.SupplierInvoices SET AmountPaid=AmountPaid+@amount WHERE SupplierInvoiceId=@invoiceId;", connection, transaction);
        update.Parameters.AddWithValue("@amount", amount);
        update.Parameters.AddWithValue("@invoiceId", invoiceId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        return amount;
    }

    public async Task<long> ReverseInvoiceAsync(SqlConnection connection, SqlTransaction transaction, int supplierInvoiceId, Guid sourceOperationId, string referenceNumber, string reason, string reversedBy, CancellationToken cancellationToken)
    {
        if (connection is null || transaction is null || supplierInvoiceId <= 0 || sourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reversedBy)) throw new ArgumentException("Supplier invoice reversal is incomplete.");
        var existing = await FindReversalEventAsync(connection, transaction, sourceOperationId, cancellationToken);
        if (existing.HasValue) return existing.Value;
        await EnsureNoPostedAllocationsAsync(connection, transaction, "a.SupplierInvoiceId", supplierInvoiceId, cancellationToken);
        var original = await ReadInvoiceForReversalAsync(connection, transaction, supplierInvoiceId, cancellationToken);
        var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, original.EventId, sourceOperationId, referenceNumber, reason, reversedBy, cancellationToken);
        await _ledgerWriter.PostAsync(connection, transaction, original.SupplierId, new LedgerEntryRequest("Reversal", original.Amount, -original.Amount, DateTime.UtcNow, original.InvoiceDate, "SupplierInvoice", supplierInvoiceId, sourceOperationId, referenceNumber, reversedBy, original.LedgerEntryId, reason, reversedBy, DateTime.UtcNow), cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.SupplierFinancialInvoices SET Status=N'Reversed' WHERE SupplierInvoiceId=@invoiceId; UPDATE dbo.SupplierInvoiceLines SET Status=N'Reversed' WHERE SupplierInvoiceId=@invoiceId AND Status=N'Posted';", connection, transaction);
        update.Parameters.AddWithValue("@invoiceId", supplierInvoiceId); await update.ExecuteNonQueryAsync(cancellationToken);
        return reversal.AccountingEventId;
    }

    public async Task<long> ReversePaymentAsync(SqlConnection connection, SqlTransaction transaction, int supplierPaymentId, Guid sourceOperationId, string referenceNumber, string reason, string reversedBy, CancellationToken cancellationToken)
    {
        if (connection is null || transaction is null || supplierPaymentId <= 0 || sourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reversedBy)) throw new ArgumentException("Supplier payment reversal is incomplete.");
        var existing = await FindReversalEventAsync(connection, transaction, sourceOperationId, cancellationToken);
        if (existing.HasValue) return existing.Value;
        await EnsureNoPostedAllocationsAsync(connection, transaction, "a.SupplierPaymentId", supplierPaymentId, cancellationToken);
        var original = await ReadPaymentForReversalAsync(connection, transaction, supplierPaymentId, cancellationToken);
        var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, original.EventId, sourceOperationId, referenceNumber, reason, reversedBy, cancellationToken);
        await _ledgerWriter.PostAsync(connection, transaction, original.SupplierId, new LedgerEntryRequest("Reversal", original.Amount, original.Amount, DateTime.UtcNow, original.PaymentDate, "SupplierPayment", supplierPaymentId, sourceOperationId, referenceNumber, reversedBy, original.LedgerEntryId, reason, reversedBy, DateTime.UtcNow), cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.SupplierFinancialPayments SET Status=N'Reversed' WHERE SupplierPaymentId=@paymentId;", connection, transaction);
        update.Parameters.AddWithValue("@paymentId", supplierPaymentId); await update.ExecuteNonQueryAsync(cancellationToken);
        return reversal.AccountingEventId;
    }

    public async Task<long> ReverseAllocationAsync(SqlConnection connection, SqlTransaction transaction, int supplierPaymentAllocationId, Guid sourceOperationId, string referenceNumber, string reason, string reversedBy, CancellationToken cancellationToken)
    {
        if (connection is null || transaction is null || supplierPaymentAllocationId <= 0 || sourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reversedBy)) throw new ArgumentException("Supplier payment allocation reversal is incomplete.");
        var existing = await FindReversalEventAsync(connection, transaction, sourceOperationId, cancellationToken);
        if (existing.HasValue) return existing.Value;
        var original = await ReadAllocationForReversalAsync(connection, transaction, supplierPaymentAllocationId, cancellationToken);
        var reversal = await FoundationPostingGateway.ReverseAsync(connection, transaction, original.EventId, sourceOperationId, referenceNumber, reason, reversedBy, cancellationToken);
        await using var update = new SqlCommand("UPDATE dbo.SupplierFinancialPaymentAllocations SET Status=N'Reversed' WHERE SupplierPaymentAllocationId=@allocationId; UPDATE dbo.SupplierInvoices SET AmountPaid=AmountPaid-@amount WHERE SupplierInvoiceId=@invoiceId;", connection, transaction);
        update.Parameters.AddWithValue("@allocationId", supplierPaymentAllocationId); update.Parameters.AddWithValue("@amount", original.Amount); update.Parameters.AddWithValue("@invoiceId", original.InvoiceId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        return reversal.AccountingEventId;
    }

    private static void ValidateInvoiceRequest(SqlConnection c, SqlTransaction t, SupplierFinancialInvoiceRequest r)
    {
        if (c is null || t is null || r.SupplierId <= 0 || r.Amount <= 0 || r.SourceOperationId == Guid.Empty || r.InvoiceDate > r.DueDate || string.IsNullOrWhiteSpace(r.InvoiceNumber) || string.IsNullOrWhiteSpace(r.CreatedBy)) throw new ArgumentException("Supplier invoice is incomplete.");
    }
    private static void ValidatePaymentRequest(SqlConnection c, SqlTransaction t, SupplierFinancialPaymentRequest r)
    {
        if (c is null || t is null || r.SupplierId <= 0 || r.Amount <= 0 || r.CashAccountId <= 0 || r.SourceOperationId == Guid.Empty || string.IsNullOrWhiteSpace(r.ReferenceNumber) || string.IsNullOrWhiteSpace(r.PaymentMethod) || string.IsNullOrWhiteSpace(r.CreatedBy) || (r.PaymentKind == SupplierPaymentKind.Advance ? r.SupplierInvoiceId.HasValue : !r.SupplierInvoiceId.HasValue)) throw new ArgumentException("Supplier payment is incomplete.");
    }
    private static async Task ValidateSupplierAsync(SqlConnection c, SqlTransaction t, int supplierId, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT IsActive FROM dbo.Suppliers WITH(UPDLOCK,HOLDLOCK) WHERE SupplierId=@supplierId", c, t); cmd.Parameters.AddWithValue("@supplierId", supplierId); var value = await cmd.ExecuteScalarAsync(ct); if (value is null || value is DBNull || !Convert.ToBoolean(value)) throw new InvalidOperationException("Supplier is unavailable for financial posting."); }
    private static async Task<int> InsertInvoiceAsync(SqlConnection c, SqlTransaction t, SupplierFinancialInvoiceRequest r, CancellationToken ct)
    { const string sql = "INSERT dbo.SupplierInvoices(SupplierId,PurchaseOrderId,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,AmountPaid,Status,Notes,CreatedAt) OUTPUT inserted.SupplierInvoiceId VALUES(@supplierId,NULL,@number,@invoiceDate,@dueDate,@amount,0,N'Open',@notes,SYSUTCDATETIME());"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@supplierId", r.SupplierId); cmd.Parameters.AddWithValue("@number", r.InvoiceNumber); cmd.Parameters.AddWithValue("@invoiceDate", r.InvoiceDate.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@dueDate", r.DueDate.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@amount", r.Amount); cmd.Parameters.AddWithValue("@notes", r.Notes ?? (object)DBNull.Value); return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task InsertFinancialInvoiceAsync(SqlConnection c, SqlTransaction t, int invoiceId, Guid operation, long eventId, string by, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.SupplierFinancialInvoices(SupplierInvoiceId,SourceOperationId,AccountingEventId,Status,CreatedBy) VALUES(@invoiceId,@operation,@eventId,N'Posted',@by);", c, t); cmd.Parameters.AddWithValue("@invoiceId", invoiceId); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; cmd.Parameters.AddWithValue("@eventId", eventId); cmd.Parameters.AddWithValue("@by", by); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<int> InsertPaymentAsync(SqlConnection c, SqlTransaction t, SupplierFinancialPaymentRequest r, CancellationToken ct)
    { const string sql = "INSERT dbo.SupplierPayments(SupplierId,PaymentNumber,PaymentDate,Amount,PaymentMethod,ReferenceNumber,Notes,CreatedAt,JournalEntryId) OUTPUT inserted.SupplierPaymentId VALUES(@supplierId,@number,@date,@amount,@method,@reference,@notes,SYSUTCDATETIME(),NULL);"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@supplierId", r.SupplierId); cmd.Parameters.AddWithValue("@number", r.ReferenceNumber); cmd.Parameters.AddWithValue("@date", r.PaymentDate.ToDateTime(TimeOnly.MinValue)); cmd.Parameters.AddWithValue("@amount", r.Amount); cmd.Parameters.AddWithValue("@method", r.PaymentMethod); cmd.Parameters.AddWithValue("@reference", r.ReferenceNumber); cmd.Parameters.AddWithValue("@notes", r.Notes ?? (object)DBNull.Value); return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task InsertFinancialPaymentAsync(SqlConnection c, SqlTransaction t, int paymentId, SupplierFinancialPaymentRequest r, long eventId, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.SupplierFinancialPayments(SupplierPaymentId,CashAccountId,PaymentKind,CurrencyCode,SourceOperationId,AccountingEventId,Status,CreatedBy) VALUES(@paymentId,@cash,@kind,@currency,@operation,@eventId,N'Posted',@by);", c, t); cmd.Parameters.AddWithValue("@paymentId", paymentId); cmd.Parameters.AddWithValue("@cash", r.CashAccountId); cmd.Parameters.AddWithValue("@kind", r.PaymentKind.ToString()); cmd.Parameters.AddWithValue("@currency", r.CurrencyCode); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = r.SourceOperationId; cmd.Parameters.AddWithValue("@eventId", eventId); cmd.Parameters.AddWithValue("@by", r.CreatedBy); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<int> InsertAllocationAsync(SqlConnection c, SqlTransaction t, int paymentId, int invoiceId, decimal amount, DateOnly date, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.SupplierPaymentAllocations(SupplierPaymentId,SupplierInvoiceId,AllocatedAmount,AllocationDate,Notes,CreatedAt) OUTPUT inserted.SupplierPaymentAllocationId VALUES(@paymentId,@invoiceId,@amount,@date,NULL,SYSUTCDATETIME());", c, t); cmd.Parameters.AddWithValue("@paymentId", paymentId); cmd.Parameters.AddWithValue("@invoiceId", invoiceId); cmd.Parameters.AddWithValue("@amount", amount); cmd.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue)); return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task InsertFinancialAllocationAsync(SqlConnection c, SqlTransaction t, int allocationId, Guid operation, long eventId, string by, CancellationToken ct)
    { await using var cmd = new SqlCommand("INSERT dbo.SupplierFinancialPaymentAllocations(SupplierPaymentAllocationId,SourceOperationId,AccountingEventId,Status,CreatedBy) VALUES(@allocationId,@operation,@eventId,N'Posted',@by);", c, t); cmd.Parameters.AddWithValue("@allocationId", allocationId); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; cmd.Parameters.AddWithValue("@eventId", eventId); cmd.Parameters.AddWithValue("@by", by); await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<(int Id, long EventId, decimal Amount, int SupplierId, string Number, decimal Paid, bool Posted)?> FindInvoiceAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    { const string sql = "SELECT i.SupplierInvoiceId,f.AccountingEventId,i.TotalAmount,i.SupplierId,i.InvoiceNumber,i.AmountPaid,CAST(CASE WHEN f.Status=N'Posted' THEN 1 ELSE 0 END AS bit) FROM dbo.SupplierFinancialInvoices f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierInvoices i ON i.SupplierInvoiceId=f.SupplierInvoiceId WHERE f.SourceOperationId=@operation;"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? (r.GetInt32(0), r.GetInt64(1), r.GetDecimal(2), r.GetInt32(3), r.GetString(4), r.GetDecimal(5), r.GetBoolean(6)) : null; }
    private static SupplierFinancialInvoiceResult ValidateInvoiceRetry((int Id, long EventId, decimal Amount, int SupplierId, string Number, decimal Paid, bool Posted) e, SupplierFinancialInvoiceRequest r)
    { if (!e.Posted || e.SupplierId != r.SupplierId || e.Amount != r.Amount || e.Number != r.InvoiceNumber) throw new InvalidOperationException("IDEMPOTENCY CONFLICT"); return new(e.Id, e.EventId, e.Paid, e.Amount - e.Paid, true); }
    private static async Task<(int Id, long EventId, int SupplierId, decimal Amount, int? InvoiceId, SupplierPaymentKind Kind, bool Posted)?> FindPaymentAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    { const string sql = "SELECT p.SupplierPaymentId,f.AccountingEventId,p.SupplierId,p.Amount,(SELECT TOP(1) SupplierInvoiceId FROM dbo.SupplierPaymentAllocations WHERE SupplierPaymentId=p.SupplierPaymentId),f.PaymentKind,CAST(CASE WHEN f.Status=N'Posted' THEN 1 ELSE 0 END AS bit) FROM dbo.SupplierFinancialPayments f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=f.SupplierPaymentId WHERE f.SourceOperationId=@operation;"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; return (r.GetInt32(0), r.GetInt64(1), r.GetInt32(2), r.GetDecimal(3), r.IsDBNull(4) ? null : r.GetInt32(4), Enum.Parse<SupplierPaymentKind>(r.GetString(5)), r.GetBoolean(6)); }
    private static SupplierFinancialPaymentResult ValidatePaymentRetry((int Id, long EventId, int SupplierId, decimal Amount, int? InvoiceId, SupplierPaymentKind Kind, bool Posted) e, SupplierFinancialPaymentRequest r)
    { if (!e.Posted || e.SupplierId != r.SupplierId || e.Amount != r.Amount || e.InvoiceId != r.SupplierInvoiceId || e.Kind != r.PaymentKind) throw new InvalidOperationException("IDEMPOTENCY CONFLICT"); return new(e.Id, e.EventId, r.SupplierInvoiceId.HasValue ? r.Amount : 0m, 0m, true); }
    private static async Task<(int Id, int PaymentId, int InvoiceId, decimal Amount, long EventId)?> FindAllocationAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    { const string sql = "SELECT a.SupplierPaymentAllocationId,a.SupplierPaymentId,a.SupplierInvoiceId,a.AllocatedAmount,f.AccountingEventId FROM dbo.SupplierFinancialPaymentAllocations f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierPaymentAllocations a ON a.SupplierPaymentAllocationId=f.SupplierPaymentAllocationId WHERE f.SourceOperationId=@operation;"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetDecimal(3), r.GetInt64(4)) : null; }
    private static async Task<int> ReadPaymentSupplierAsync(SqlConnection c, SqlTransaction t, int paymentId, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT SupplierId FROM dbo.SupplierPayments WITH(UPDLOCK,HOLDLOCK) WHERE SupplierPaymentId=@paymentId", c, t); cmd.Parameters.AddWithValue("@paymentId", paymentId); var value = await cmd.ExecuteScalarAsync(ct); return value is null ? throw new InvalidOperationException("Supplier payment is unavailable.") : Convert.ToInt32(value); }
    private static async Task ValidateInvoiceForPaymentAsync(SqlConnection c, SqlTransaction t, int supplierId, int invoiceId, decimal amount, CancellationToken ct) => await ValidateInvoiceForAllocationAsync(c, t, supplierId, invoiceId, amount, ct);
    private static async Task ValidateInvoiceForAllocationAsync(SqlConnection c, SqlTransaction t, int supplierId, int invoiceId, decimal amount, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT SupplierId,TotalAmount,AmountPaid FROM dbo.SupplierInvoices WITH(UPDLOCK,HOLDLOCK) WHERE SupplierInvoiceId=@invoiceId", c, t); cmd.Parameters.AddWithValue("@invoiceId", invoiceId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct) || r.GetInt32(0) != supplierId || amount > r.GetDecimal(1) - r.GetDecimal(2)) throw new InvalidOperationException("Supplier invoice is unavailable or overpaid."); }
    private static async Task<decimal> GetAvailablePaymentAmountAsync(SqlConnection c, SqlTransaction t, int paymentId, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT p.Amount-COALESCE(SUM(CASE WHEN f.Status=N'Posted' THEN a.AllocatedAmount ELSE 0 END),0) FROM dbo.SupplierPayments p WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.SupplierPaymentAllocations a WITH(UPDLOCK,HOLDLOCK) ON a.SupplierPaymentId=p.SupplierPaymentId LEFT JOIN dbo.SupplierFinancialPaymentAllocations f WITH(UPDLOCK,HOLDLOCK) ON f.SupplierPaymentAllocationId=a.SupplierPaymentAllocationId WHERE p.SupplierPaymentId=@paymentId GROUP BY p.Amount;", c, t); cmd.Parameters.AddWithValue("@paymentId", paymentId); var value = await cmd.ExecuteScalarAsync(ct); return value is null ? throw new InvalidOperationException("Supplier payment is unavailable.") : Convert.ToDecimal(value); }
    private async Task<decimal> ApplyAvailableAdvancesAsync(SqlConnection c, SqlTransaction t, int supplierId, int invoiceId, DateOnly date, Guid invoiceOperation, string reference, string by, CancellationToken ct)
    { decimal applied = 0m; const string sql = "SELECT p.SupplierPaymentId,p.Amount-COALESCE(SUM(CASE WHEN af.Status=N'Posted' THEN a.AllocatedAmount ELSE 0 END),0) FROM dbo.SupplierFinancialPayments f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierPayments p WITH(UPDLOCK,HOLDLOCK) ON p.SupplierPaymentId=f.SupplierPaymentId LEFT JOIN dbo.SupplierPaymentAllocations a WITH(UPDLOCK,HOLDLOCK) ON a.SupplierPaymentId=p.SupplierPaymentId LEFT JOIN dbo.SupplierFinancialPaymentAllocations af WITH(UPDLOCK,HOLDLOCK) ON af.SupplierPaymentAllocationId=a.SupplierPaymentAllocationId WHERE p.SupplierId=@supplierId AND f.PaymentKind=N'Advance' AND f.Status=N'Posted' GROUP BY p.SupplierPaymentId,p.Amount HAVING p.Amount-COALESCE(SUM(CASE WHEN af.Status=N'Posted' THEN a.AllocatedAmount ELSE 0 END),0)>0 ORDER BY p.SupplierPaymentId;"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@supplierId", supplierId); var advances = new List<(int Id, decimal Available)>(); await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) advances.Add((r.GetInt32(0), r.GetDecimal(1))); foreach (var advance in advances) { var remaining = await GetInvoiceOutstandingAsync(c, t, invoiceId, ct); if (remaining <= 0) break; var amount = Math.Min(advance.Available, remaining); var operation = DeterministicOperation(invoiceOperation, advance.Id); await AllocateAsync(c, t, advance.Id, invoiceId, amount, date, operation, reference, by, ct); applied += amount; } return applied; }
    private static async Task<decimal> GetInvoiceOutstandingAsync(SqlConnection c, SqlTransaction t, int invoiceId, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT TotalAmount-AmountPaid FROM dbo.SupplierInvoices WITH(UPDLOCK,HOLDLOCK) WHERE SupplierInvoiceId=@invoiceId", c, t); cmd.Parameters.AddWithValue("@invoiceId", invoiceId); return Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct)); }
    private static Guid DeterministicOperation(Guid invoiceOperation, int paymentId)
    { var bytes = invoiceOperation.ToByteArray(); BitConverter.GetBytes(paymentId).CopyTo(bytes, 0); return new Guid(bytes); }
    private static async Task<decimal> GetSupplierBalanceAsync(SqlConnection c, SqlTransaction t, int supplierId, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT COALESCE(SUM(BalanceEffect),0) FROM dbo.SupplierLedgerEntries WITH(UPDLOCK,HOLDLOCK) WHERE SupplierId=@supplierId AND Status=N'Posted';", c, t); cmd.Parameters.AddWithValue("@supplierId", supplierId); return Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct)); }
    private static async Task<(int SupplierId, decimal Amount, DateOnly InvoiceDate, long EventId, int LedgerEntryId)> ReadInvoiceForReversalAsync(SqlConnection c, SqlTransaction t, int invoiceId, CancellationToken ct)
    { const string sql = "SELECT i.SupplierId,i.TotalAmount,i.InvoiceDate,f.AccountingEventId,l.SupplierLedgerEntryId FROM dbo.SupplierFinancialInvoices f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierInvoices i ON i.SupplierInvoiceId=f.SupplierInvoiceId JOIN dbo.SupplierLedgerEntries l ON l.SourceType=N'SupplierInvoice' AND l.SourceId=i.SupplierInvoiceId AND l.EntryType=N'SupplierInvoice' WHERE f.SupplierInvoiceId=@invoiceId AND f.Status=N'Posted';"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@invoiceId", invoiceId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Supplier invoice cannot be reversed."); return (r.GetInt32(0), r.GetDecimal(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetInt64(3), r.GetInt32(4)); }
    private static async Task<(int SupplierId, decimal Amount, DateOnly PaymentDate, long EventId, int LedgerEntryId)> ReadPaymentForReversalAsync(SqlConnection c, SqlTransaction t, int paymentId, CancellationToken ct)
    { const string sql = "SELECT p.SupplierId,p.Amount,p.PaymentDate,f.AccountingEventId,l.SupplierLedgerEntryId FROM dbo.SupplierFinancialPayments f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=f.SupplierPaymentId JOIN dbo.SupplierLedgerEntries l ON l.SourceType=N'SupplierPayment' AND l.SourceId=p.SupplierPaymentId AND l.EntryType IN(N'SupplierPayment',N'SupplierAdvancePayment') WHERE f.SupplierPaymentId=@paymentId AND f.Status=N'Posted';"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@paymentId", paymentId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Supplier payment cannot be reversed."); return (r.GetInt32(0), r.GetDecimal(1), DateOnly.FromDateTime(r.GetDateTime(2)), r.GetInt64(3), r.GetInt32(4)); }
    private static async Task<(int InvoiceId, decimal Amount, long EventId)> ReadAllocationForReversalAsync(SqlConnection c, SqlTransaction t, int allocationId, CancellationToken ct)
    { const string sql = "SELECT a.SupplierInvoiceId,a.AllocatedAmount,f.AccountingEventId FROM dbo.SupplierFinancialPaymentAllocations f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierPaymentAllocations a ON a.SupplierPaymentAllocationId=f.SupplierPaymentAllocationId WHERE f.SupplierPaymentAllocationId=@allocationId AND f.Status=N'Posted';"; await using var cmd = new SqlCommand(sql, c, t); cmd.Parameters.AddWithValue("@allocationId", allocationId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Supplier payment allocation cannot be reversed."); return (r.GetInt32(0), r.GetDecimal(1), r.GetInt64(2)); }
    private static async Task<long?> FindReversalEventAsync(SqlConnection c, SqlTransaction t, Guid operation, CancellationToken ct)
    { await using var cmd = new SqlCommand("SELECT AccountingEventId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@operation AND AccountingEventType=33;", c, t); cmd.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operation; var value = await cmd.ExecuteScalarAsync(ct); return value is null ? null : Convert.ToInt64(value); }
    private static async Task EnsureNoPostedAllocationsAsync(SqlConnection c, SqlTransaction t, string keyColumn, int keyId, CancellationToken ct)
    { await using var cmd = new SqlCommand($"SELECT COUNT(*) FROM dbo.SupplierPaymentAllocations a WITH(UPDLOCK,HOLDLOCK) JOIN dbo.SupplierFinancialPaymentAllocations f WITH(UPDLOCK,HOLDLOCK) ON f.SupplierPaymentAllocationId=a.SupplierPaymentAllocationId WHERE {keyColumn}=@id AND f.Status=N'Posted';", c, t); cmd.Parameters.AddWithValue("@id", keyId); if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) != 0) throw new InvalidOperationException("Supplier allocations must be reversed before the financial document can be reversed."); }
}