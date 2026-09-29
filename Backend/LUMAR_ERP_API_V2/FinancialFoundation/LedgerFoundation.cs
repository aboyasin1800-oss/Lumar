using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public sealed record LedgerEntryRequest(
    string EntryType,
    decimal Amount,
    decimal BalanceEffect,
    DateTime OccurredAt,
    DateOnly EffectiveDate,
    string SourceType,
    long SourceId,
    Guid SourceOperationId,
    string ReferenceNumber,
    string CreatedBy,
    long? OriginalEntryId = null,
    string? ReversalReason = null,
    string? ReversedBy = null,
    DateTime? ReversedAt = null);

public sealed record LedgerEntryResult(long EntryId, bool IsExisting);
public sealed record EmployeeBalance(decimal NetBalance, decimal Accruals, decimal Advances, decimal DeductibleExpenses, decimal Payments);
public sealed record SupplierBalance(decimal CurrentBalance, decimal TotalInvoices, decimal TotalPayments, decimal TotalAdvances, decimal TotalAllocations);

public sealed class EmployeeLedgerWriter
{
    private static readonly HashSet<string> EntryTypes = new(StringComparer.Ordinal)
    {
        "SalaryAccrual", "SeasonalBonus", "PieceWageAccrual", "Advance",
        "SalariedEmployeeDailyExpense", "PieceWorkerDailyExpense", "EmployeePayment", "Reversal"
    };

    public async Task<LedgerEntryResult> PostAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        Validate(connection, transaction, employeeId, request, EntryTypes);
        var existing = await FindExistingAsync(connection, transaction, employeeId, request, cancellationToken);
        if (existing is not null) return existing;

        if (request.EntryType == "Reversal")
            await ValidateAndMarkOriginalAsync(connection, transaction, employeeId, request, cancellationToken);

        const string sql = """
            INSERT INTO dbo.EmployeeLedgerEntries
                (EmployeeId, EntryType, Amount, BalanceEffect, OccurredAt, EffectiveDate, SourceType, SourceId, SourceOperationId, ReferenceNumber, OriginalEntryId, ReversalReason, ReversedBy, ReversedAt, CreatedBy, Status)
            OUTPUT INSERTED.EmployeeLedgerEntryId
            VALUES
                (@employeeId, @entryType, @amount, @balanceEffect, @occurredAt, @effectiveDate, @sourceType, @sourceId, @sourceOperationId, @referenceNumber, @originalEntryId, @reversalReason, @reversedBy, @reversedAt, @createdBy, N'Posted');
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        AddEntryParameters(command, employeeId, request);
        var entryId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new LedgerEntryResult(entryId, false);
    }

    private static async Task<LedgerEntryResult?> FindExistingAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EmployeeLedgerEntryId, EmployeeId, Amount, BalanceEffect, SourceType, SourceId, ReferenceNumber
            FROM dbo.EmployeeLedgerEntries WITH (UPDLOCK, HOLDLOCK)
            WHERE SourceOperationId=@sourceOperationId AND EntryType=@entryType;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        command.Parameters.AddWithValue("@entryType", request.EntryType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (reader.GetInt32(1) != employeeId || reader.GetDecimal(2) != request.Amount || reader.GetDecimal(3) != request.BalanceEffect
            || reader.GetString(4) != request.SourceType || reader.GetInt64(5) != request.SourceId || reader.GetString(6) != request.ReferenceNumber)
            throw new InvalidOperationException("Employee ledger retry conflicts with the official entry.");
        return new LedgerEntryResult(reader.GetInt64(0), true);
    }

    private static async Task ValidateAndMarkOriginalAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EmployeeId, BalanceEffect, Status
            FROM dbo.EmployeeLedgerEntries WITH (UPDLOCK, HOLDLOCK)
            WHERE EmployeeLedgerEntryId=@originalEntryId;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@originalEntryId", request.OriginalEntryId!.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(0) != employeeId || reader.GetString(2) != "Posted" || reader.GetDecimal(1) != -request.BalanceEffect)
            throw new InvalidOperationException("The employee ledger entry cannot be reversed.");
        await reader.CloseAsync();
        await using var update = new SqlCommand("UPDATE dbo.EmployeeLedgerEntries SET Status=N'Reversed' WHERE EmployeeLedgerEntryId=@originalEntryId", connection, transaction);
        update.Parameters.AddWithValue("@originalEntryId", request.OriginalEntryId.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddEntryParameters(SqlCommand command, int employeeId, LedgerEntryRequest request)
    {
        command.Parameters.AddWithValue("@employeeId", employeeId);
        AddCommonParameters(command, request);
    }

    internal static void AddCommonParameters(SqlCommand command, LedgerEntryRequest request)
    {
        command.Parameters.AddWithValue("@entryType", request.EntryType);
        command.Parameters.AddWithValue("@amount", request.Amount);
        command.Parameters.AddWithValue("@balanceEffect", request.BalanceEffect);
        command.Parameters.AddWithValue("@occurredAt", request.OccurredAt);
        command.Parameters.AddWithValue("@effectiveDate", request.EffectiveDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@sourceType", request.SourceType);
        command.Parameters.AddWithValue("@sourceId", request.SourceId);
        command.Parameters.Add("@sourceOperationId", SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        command.Parameters.AddWithValue("@referenceNumber", request.ReferenceNumber);
        command.Parameters.AddWithValue("@originalEntryId", request.OriginalEntryId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@reversalReason", request.ReversalReason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@reversedBy", request.ReversedBy ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@reversedAt", request.ReversedAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdBy", request.CreatedBy);
    }

    internal static void Validate(SqlConnection connection, SqlTransaction transaction, int partyId, LedgerEntryRequest request, IReadOnlySet<string> allowedEntryTypes)
    {
        if (connection is null || transaction is null) throw new ArgumentException("A caller-owned SQL transaction is required.");
        if (partyId <= 0 || request is null || !allowedEntryTypes.Contains(request.EntryType) || request.Amount <= 0 || (request.BalanceEffect == 0 && request.EntryType != "SalariedEmployeeDailyExpense")
            || request.SourceOperationId == Guid.Empty || request.SourceId <= 0 || string.IsNullOrWhiteSpace(request.SourceType)
            || string.IsNullOrWhiteSpace(request.ReferenceNumber) || string.IsNullOrWhiteSpace(request.CreatedBy))
            throw new ArgumentException("The ledger entry is incomplete.");
        var reversal = request.EntryType == "Reversal";
        if (reversal != request.OriginalEntryId.HasValue || reversal != !string.IsNullOrWhiteSpace(request.ReversalReason)
            || reversal != !string.IsNullOrWhiteSpace(request.ReversedBy) || reversal != request.ReversedAt.HasValue)
            throw new ArgumentException("The reversal reference is incomplete.");
    }
}

public sealed class EmployeeLedgerReader
{
    public async Task<EmployeeBalance> GetBalanceAsync(SqlConnection connection, int employeeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(BalanceEffect), 0),
                COALESCE(SUM(CASE WHEN EntryType IN (N'SalaryAccrual', N'SeasonalBonus', N'PieceWageAccrual') THEN BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'Advance' THEN -BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'PieceWorkerDailyExpense' THEN -BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'EmployeePayment' THEN -BalanceEffect ELSE 0 END), 0)
            FROM dbo.EmployeeLedgerEntries
            WHERE EmployeeId=@employeeId;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new EmployeeBalance(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
    }
}

public sealed class SupplierLedgerWriter
{
    private static readonly HashSet<string> EntryTypes = new(StringComparer.Ordinal)
    {
        "SupplierInvoice", "SupplierPayment", "SupplierAdvancePayment", "SupplierPaymentAllocation", "Reversal"
    };

    public async Task<LedgerEntryResult> PostAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        EmployeeLedgerWriter.Validate(connection, transaction, supplierId, request, EntryTypes);
        var existing = await FindExistingAsync(connection, transaction, supplierId, request, cancellationToken);
        if (existing is not null) return existing;
        if (request.EntryType == "Reversal") await ValidateAndMarkOriginalAsync(connection, transaction, supplierId, request, cancellationToken);

        const string balanceSql = "SELECT COALESCE(SUM(BalanceEffect), 0) FROM dbo.SupplierLedgerEntries WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId=@supplierId AND Status <> N'Legacy';";
        await using var balanceCommand = new SqlCommand(balanceSql, connection, transaction);
        balanceCommand.Parameters.AddWithValue("@supplierId", supplierId);
        var balanceAfter = Convert.ToDecimal(await balanceCommand.ExecuteScalarAsync(cancellationToken)) + request.BalanceEffect;
        const string sql = """
            INSERT INTO dbo.SupplierLedgerEntries
                (SupplierId, ReferenceNumber, DebitAmount, CreditAmount, BalanceAfterTransaction, CreatedAt, EntryType, Amount, BalanceEffect, OccurredAt, SourceType, SourceId, SourceOperationId, OriginalEntryId, ReversalReason, ReversedBy, ReversedAt, CreatedBy, Status)
            OUTPUT INSERTED.SupplierLedgerEntryId
            VALUES
                (@supplierId, @referenceNumber, @debitAmount, @creditAmount, @balanceAfter, SYSUTCDATETIME(), @entryType, @amount, @balanceEffect, @occurredAt, @sourceType, @sourceId, @sourceOperationId, @originalEntryId, @reversalReason, @reversedBy, @reversedAt, @createdBy, N'Posted');
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        command.Parameters.AddWithValue("@debitAmount", request.BalanceEffect < 0 ? -request.BalanceEffect : 0m);
        command.Parameters.AddWithValue("@creditAmount", request.BalanceEffect > 0 ? request.BalanceEffect : 0m);
        command.Parameters.AddWithValue("@balanceAfter", balanceAfter);
        EmployeeLedgerWriter.AddCommonParameters(command, request);
        var entryId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new LedgerEntryResult(entryId, false);
    }

    private static async Task<LedgerEntryResult?> FindExistingAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        const string sql = "SELECT SupplierLedgerEntryId, SupplierId, Amount, BalanceEffect, SourceType, SourceId, ReferenceNumber FROM dbo.SupplierLedgerEntries WITH (UPDLOCK, HOLDLOCK) WHERE SourceOperationId=@sourceOperationId AND EntryType=@entryType;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@sourceOperationId", SqlDbType.UniqueIdentifier).Value = request.SourceOperationId;
        command.Parameters.AddWithValue("@entryType", request.EntryType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (reader.GetInt32(1) != supplierId || reader.GetDecimal(2) != request.Amount || reader.GetDecimal(3) != request.BalanceEffect
            || reader.GetString(4) != request.SourceType || reader.GetInt64(5) != request.SourceId || reader.GetString(6) != request.ReferenceNumber)
            throw new InvalidOperationException("Supplier ledger retry conflicts with the official entry.");
        return new LedgerEntryResult(reader.GetInt64(0), true);
    }

    private static async Task ValidateAndMarkOriginalAsync(SqlConnection connection, SqlTransaction transaction, int supplierId, LedgerEntryRequest request, CancellationToken cancellationToken)
    {
        const string sql = "SELECT SupplierId, BalanceEffect, Status FROM dbo.SupplierLedgerEntries WITH (UPDLOCK, HOLDLOCK) WHERE SupplierLedgerEntryId=@originalEntryId;";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@originalEntryId", request.OriginalEntryId!.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(0) != supplierId || reader.GetString(2) != "Posted" || reader.GetDecimal(1) != -request.BalanceEffect)
            throw new InvalidOperationException("The supplier ledger entry cannot be reversed.");
        await reader.CloseAsync();
        await using var update = new SqlCommand("UPDATE dbo.SupplierLedgerEntries SET Status=N'Reversed' WHERE SupplierLedgerEntryId=@originalEntryId", connection, transaction);
        update.Parameters.AddWithValue("@originalEntryId", request.OriginalEntryId.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class SupplierLedgerReader
{
    public async Task<SupplierBalance> GetBalanceAsync(SqlConnection connection, int supplierId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(SUM(BalanceEffect), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'SupplierInvoice' THEN BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'SupplierPayment' THEN -BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'SupplierAdvancePayment' THEN -BalanceEffect ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EntryType=N'SupplierPaymentAllocation' THEN BalanceEffect ELSE 0 END), 0)
            FROM dbo.SupplierLedgerEntries
            WHERE SupplierId=@supplierId AND Status <> N'Legacy';
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new SupplierBalance(reader.GetDecimal(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4));
    }
}