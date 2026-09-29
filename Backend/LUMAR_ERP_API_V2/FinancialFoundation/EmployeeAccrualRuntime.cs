using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public sealed record EligibilityRecord(long Id, DailyEligibilityResult Result, bool IsExisting);

public sealed class EmployeeDailyEligibilityWriter
{
    private readonly EmployeeDailyEligibilityResolver _resolver = new();

    public async Task<EligibilityRecord> ResolveAndWriteAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateOnly date, Guid sourceOperationId, string createdBy, CancellationToken cancellationToken)
    {
        var resolved = await _resolver.ResolveAsync(connection, transaction, employeeId, date, cancellationToken);
        const string find = "SELECT EmployeeDailyEligibilityId,EligibilityStatus FROM dbo.EmployeeDailyEligibility WITH(UPDLOCK,HOLDLOCK) WHERE EmployeeId=@employeeId AND EligibilityDate=@date;";
        await using var existingCommand = new SqlCommand(find, connection, transaction);
        existingCommand.Parameters.AddWithValue("@employeeId", employeeId);
        existingCommand.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue));
        await using var existingReader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        if (await existingReader.ReadAsync(cancellationToken))
        {
            var stored = existingReader.GetString(1);
            if (stored != resolved.Status) throw new InvalidOperationException("The official eligibility record conflicts with the current authoritative sources.");
            return new EligibilityRecord(existingReader.GetInt64(0), resolved, true);
        }
        await existingReader.CloseAsync();
        const string insert = "INSERT dbo.EmployeeDailyEligibility(EmployeeId,EligibilityDate,EligibilityStatus,SourceOperationId,CreatedBy) OUTPUT INSERTED.EmployeeDailyEligibilityId VALUES(@employeeId,@date,@status,@operation,@createdBy);";
        await using var insertCommand = new SqlCommand(insert, connection, transaction);
        insertCommand.Parameters.AddWithValue("@employeeId", employeeId);
        insertCommand.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue));
        insertCommand.Parameters.AddWithValue("@status", resolved.Status);
        insertCommand.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = sourceOperationId;
        insertCommand.Parameters.AddWithValue("@createdBy", createdBy);
        return new EligibilityRecord(Convert.ToInt64(await insertCommand.ExecuteScalarAsync(cancellationToken)), resolved, false);
    }
}

public sealed class EmployeeAccrualRuntime
{
    private readonly EmployeeDailyEligibilityWriter _eligibilityWriter = new();
    private readonly EmployeeLedgerWriter _ledgerWriter = new();

    public async Task<long> PostDailySalaryAccrualAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateOnly date, decimal amount, Guid operationId, string reference, string createdBy, CancellationToken cancellationToken)
    {
        var eligibility = await _eligibilityWriter.ResolveAndWriteAsync(connection, transaction, employeeId, date, Guid.NewGuid(), createdBy, cancellationToken);
        if (!eligibility.Result.IsEligible) throw new InvalidOperationException("Daily salary accrual requires an eligible day.");
        var sourceId = await InsertAsync(connection, transaction, "EmployeeDailyAccruals", "EmployeeDailyAccrualId", "INSERT dbo.EmployeeDailyAccruals(EmployeeId,AccrualDate,EligibilityId,Amount,SourceOperationId,CreatedBy) OUTPUT INSERTED.EmployeeDailyAccrualId VALUES(@employeeId,@date,@eligibilityId,@amount,@operation,@createdBy);", employeeId, date, amount, operationId, createdBy, cancellationToken, eligibility.Id);
        return await PostAsync(connection, transaction, employeeId, "SalaryAccrual", 20, amount, amount, date, sourceId, operationId, reference, createdBy, null, cancellationToken);
    }

    public async Task<long> PostSeasonalBonusAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string season, int seasonYear, decimal amount, Guid operationId, string reference, string createdBy, CancellationToken cancellationToken)
    {
        var sourceId = await InsertAsync(connection, transaction, "EmployeeSeasonalBonuses", "EmployeeSeasonalBonusId", "INSERT dbo.EmployeeSeasonalBonuses(EmployeeId,Season,SeasonYear,Amount,SourceOperationId,CreatedBy) OUTPUT INSERTED.EmployeeSeasonalBonusId VALUES(@employeeId,@season,@seasonYear,@amount,@operation,@createdBy);", employeeId, DateOnly.FromDateTime(DateTime.UtcNow), amount, operationId, createdBy, cancellationToken, null, season, seasonYear);
        return await PostAsync(connection, transaction, employeeId, "SeasonalBonus", 21, amount, amount, DateOnly.FromDateTime(DateTime.UtcNow), sourceId, operationId, reference, createdBy, null, cancellationToken);
    }

    public async Task<long> PostAdvanceAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateOnly date, decimal amount, int cashAccountId, Guid operationId, string reference, string createdBy, CancellationToken cancellationToken)
    {
        var sourceId = await InsertAsync(connection, transaction, "EmployeeAdvances", "EmployeeAdvanceId", "INSERT dbo.EmployeeAdvances(EmployeeId,AdvanceDate,Amount,CashAccountId,SourceOperationId,CreatedBy) OUTPUT INSERTED.EmployeeAdvanceId VALUES(@employeeId,@date,@amount,@cashAccountId,@operation,@createdBy);", employeeId, date, amount, operationId, createdBy, cancellationToken, null, null, null, cashAccountId);
        return await PostAsync(connection, transaction, employeeId, "Advance", 23, amount, -amount, date, sourceId, operationId, reference, createdBy, cashAccountId, cancellationToken);
    }

    public async Task<long> PostDailyExpenseAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateOnly date, decimal amount, int cashAccountId, Guid operationId, string reference, string createdBy, CancellationToken cancellationToken)
    {
        var salaryType = await GetSalaryTypeAsync(connection, transaction, employeeId, cancellationToken);
        var sourceId = await InsertAsync(connection, transaction, "EmployeeDailyExpenses", "EmployeeDailyExpenseId", "INSERT dbo.EmployeeDailyExpenses(EmployeeId,ExpenseDate,Amount,CashAccountId,SourceOperationId,CreatedBy) OUTPUT INSERTED.EmployeeDailyExpenseId VALUES(@employeeId,@date,@amount,@cashAccountId,@operation,@createdBy);", employeeId, date, amount, operationId, createdBy, cancellationToken, null, null, null, cashAccountId);
        var salaried = salaryType == "BasicSalary";
        return await PostAsync(connection, transaction, employeeId, salaried ? "SalariedEmployeeDailyExpense" : "PieceWorkerDailyExpense", salaried ? (byte)24 : (byte)25, amount, salaried ? 0m : -amount, date, sourceId, operationId, reference, createdBy, cashAccountId, cancellationToken);
    }

    private async Task<long> PostAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string entryType, byte eventType, decimal amount, decimal balanceEffect, DateOnly date, long sourceId, Guid operationId, string reference, string createdBy, int? cashAccountId, CancellationToken cancellationToken)
    {
        var entry = await _ledgerWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(entryType, amount, balanceEffect, DateTime.UtcNow, date, "EmployeeAccrualSource", sourceId, operationId, reference, createdBy), cancellationToken);
        var eventResult = await FoundationPostingGateway.PostAsync(connection, transaction, new FoundationPostingRequest(eventType, amount, "EmployeeLedgerEntry", entry.EntryId, operationId, reference, entryType, createdBy, cashAccountId, cashAccountId is null ? null : "Employee", cashAccountId is null ? null : employeeId), cancellationToken);
        return eventResult.AccountingEventId;
    }

    private static async Task<string> GetSalaryTypeAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT SalaryType FROM dbo.Employees WHERE EmployeeID=@employeeId", connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? throw new InvalidOperationException("Employee was not found.");
    }

    private static async Task<long> InsertAsync(SqlConnection connection, SqlTransaction transaction, string table, string idColumn, string sql, int employeeId, DateOnly date, decimal amount, Guid operationId, string createdBy, CancellationToken cancellationToken, long? eligibilityId, string? season = null, int? seasonYear = null, int? cashAccountId = null)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId); command.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue)); command.Parameters.AddWithValue("@amount", amount); command.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operationId; command.Parameters.AddWithValue("@createdBy", createdBy);
        if (eligibilityId.HasValue) command.Parameters.AddWithValue("@eligibilityId", eligibilityId.Value);
        if (season is not null) command.Parameters.AddWithValue("@season", season);
        if (seasonYear.HasValue) command.Parameters.AddWithValue("@seasonYear", seasonYear.Value);
        if (cashAccountId.HasValue) command.Parameters.AddWithValue("@cashAccountId", cashAccountId.Value);
        try { return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)); }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await using var existing = new SqlCommand($"SELECT {idColumn} FROM dbo.{table} WHERE SourceOperationId=@operation", connection, transaction);
            existing.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = operationId;
            var sourceId = await existing.ExecuteScalarAsync(cancellationToken);
            if (sourceId is not null) return Convert.ToInt64(sourceId);
            throw;
        }
    }
}