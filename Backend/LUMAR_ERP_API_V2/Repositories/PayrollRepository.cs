using System.Data;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Payroll;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PayrollRepository(ReadOnlySqlConnectionFactory readOnlyConnections, OperationalSqlConnectionFactory operationalConnections) : IPayrollRepository
{
    public Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(CancellationToken ct) => QueryAsync("SELECT PayrollPeriodId, PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, ApprovedAt, CreatedAt FROM dbo.PayrollPeriods ORDER BY StartDate DESC, PayrollPeriodId DESC", null, MapPeriod, ct);
    public async Task<PayrollPeriodDto?> GetPeriodAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT PayrollPeriodId, PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, ApprovedAt, CreatedAt FROM dbo.PayrollPeriods WHERE PayrollPeriodId = @id", id, MapPeriod, ct)).FirstOrDefault();
    public Task<IReadOnlyList<PayrollRecordDto>> GetRecordsAsync(CancellationToken ct) => QueryAsync("SELECT PayrollRecordId, PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt FROM dbo.PayrollRecords ORDER BY CreatedAt DESC, PayrollRecordId DESC", null, MapRecord, ct);
    public async Task<PayrollRecordDto?> GetRecordAsync(int id, CancellationToken ct) => (await QueryAsync("SELECT PayrollRecordId, PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt FROM dbo.PayrollRecords WHERE PayrollRecordId = @id", id, MapRecord, ct)).FirstOrDefault();
    public Task<IReadOnlyList<PayrollItemDto>> GetItemsAsync(int recordId, CancellationToken ct) => QueryAsync("SELECT PayrollItemId, PayrollRecordId, ItemType, ItemName, Quantity, Rate, Amount, Notes FROM dbo.PayrollItems WHERE PayrollRecordId = @id ORDER BY PayrollItemId", recordId, reader => new PayrollItemDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.NullableString("Notes")), ct);
    public Task<IReadOnlyList<EmployeePayrollSummaryDto>> GetEmployeeSummariesAsync(int periodId, CancellationToken ct) => QueryAsync("SELECT EmployeeId, PayrollPeriodId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount FROM dbo.PayrollRecords WHERE PayrollPeriodId = @id ORDER BY EmployeeId", periodId, reader => new EmployeePayrollSummaryDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8)), ct);
    public Task<IReadOnlyList<PieceWageRecordDto>> GetPieceWagesAsync(CancellationToken ct) => QueryAsync("SELECT PieceWageRecordID, OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt FROM dbo.PieceWageRecords ORDER BY CreatedAt DESC, PieceWageRecordID DESC", null, reader => new PieceWageRecordDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.NullableInt32("EmployeeId"), reader.NullableString("EmployeeCode"), reader.GetString(7), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.NullableInt32("PayrollPeriodId"), reader.NullableInt32("PayrollRecordId"), reader.GetString(14), reader.NullableString("Notes"), reader.GetDateTime(16)), ct);
    public Task<IReadOnlyList<PieceWageRateDto>> GetPieceWageRatesAsync(CancellationToken ct) => QueryAsync("SELECT PieceWageRateID, PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt FROM dbo.PieceWageRates ORDER BY PieceType, Stage, PieceWageRateID", null, reader => new PieceWageRateDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.NullableString("Notes"), reader.GetDateTime(6), reader.NullableDateTime("UpdatedAt")), ct);

    public async Task<PieceWageRateDto> CreatePieceWageRateAsync(CreatePieceWageRateDto request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.PieceType)) throw new ArgumentException("Piece type is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Stage)) throw new ArgumentException("Stage is required.", nameof(request));
        if (request.WageRate < 0m) throw new ArgumentOutOfRangeException(nameof(request), "Wage rate cannot be negative.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "INSERT INTO dbo.PieceWageRates (PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt) OUTPUT INSERTED.PieceWageRateID, INSERTED.PieceType, INSERTED.Stage, INSERTED.WageRate, INSERTED.IsActive, INSERTED.Notes, INSERTED.CreatedAt, INSERTED.UpdatedAt VALUES (@pieceType, @stage, @wageRate, @isActive, @notes, @createdAt, @updatedAt)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceType", request.PieceType.Trim());
        command.Parameters.AddWithValue("@stage", request.Stage.Trim());
        command.Parameters.AddWithValue("@wageRate", request.WageRate);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(request.Notes) ? (object)DBNull.Value : request.Notes.Trim());
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("@createdAt", now);
        command.Parameters.AddWithValue("@updatedAt", (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Piece wage rate was not created.");
        return new PieceWageRateDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.NullableString("Notes"), reader.GetDateTime(6), reader.NullableDateTime("UpdatedAt"));
    }

    public async Task<PieceWageRateDto?> UpdatePieceWageRateAsync(int id, UpdatePieceWageRateDto request, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.PieceType)) throw new ArgumentException("Piece type is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Stage)) throw new ArgumentException("Stage is required.", nameof(request));
        if (request.WageRate < 0m) throw new ArgumentOutOfRangeException(nameof(request), "Wage rate cannot be negative.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "UPDATE dbo.PieceWageRates SET PieceType=@pieceType, Stage=@stage, WageRate=@wageRate, IsActive=@isActive, Notes=@notes, UpdatedAt=@updatedAt WHERE PieceWageRateID=@id; SELECT PieceWageRateID, PieceType, Stage, WageRate, IsActive, Notes, CreatedAt, UpdatedAt FROM dbo.PieceWageRates WHERE PieceWageRateID=@id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceType", request.PieceType.Trim());
        command.Parameters.AddWithValue("@stage", request.Stage.Trim());
        command.Parameters.AddWithValue("@wageRate", request.WageRate);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(request.Notes) ? (object)DBNull.Value : request.Notes.Trim());
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new PieceWageRateDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.NullableString("Notes"), reader.GetDateTime(6), reader.NullableDateTime("UpdatedAt"));
    }

    public async Task<bool> DeletePieceWageRateAsync(int id, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "DELETE FROM dbo.PieceWageRates WHERE PieceWageRateID = @id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<GeneratePayrollResultDto> GenerateAsync(GeneratePayrollRequestDto request, CancellationToken ct)
    {
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Payroll start date must be on or before the end date.", nameof(request));
        }

        var normalizedCode = string.IsNullOrWhiteSpace(request.PeriodCode) ? $"PR-{request.StartDate:yyyyMM}-{request.EndDate:yyyyMM}" : request.PeriodCode.Trim();
        var note = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var generationTime = DateTime.UtcNow;

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var existingPeriod = await ReadPeriodForRangeAsync(connection, transaction, request.StartDate.Date, request.EndDate.Date, ct);
            if (existingPeriod is not null)
            {
                var existingRecordIds = await GetRecordIdsAsync(connection, transaction, existingPeriod.PayrollPeriodId, ct);
                var summaries = await GetPeriodSummariesAsync(connection, transaction, existingPeriod.PayrollPeriodId, ct);
                await transaction.RollbackAsync(CancellationToken.None);
                return new GeneratePayrollResultDto(
                    existingPeriod.PayrollPeriodId,
                    existingPeriod.PeriodCode,
                    existingRecordIds.Count,
                    summaries.Sum(s => s.GrossAmount),
                    summaries.Sum(s => s.NetAmount),
                    existingPeriod.GeneratedAt ?? generationTime,
                    existingRecordIds);
            }

            var periodId = await InsertPayrollPeriodAsync(connection, transaction, normalizedCode, request.StartDate.Date, request.EndDate.Date, "Generated", note, generationTime, ct);
            var employees = await GetPayrollSeedEmployeesAsync(connection, transaction, ct);
            var recordIds = new List<int>();
            decimal totalGross = 0m;
            decimal totalNet = 0m;

            foreach (var employee in employees)
            {
                var pieceWageAmount = await GetPieceWageTotalForEmployeeAsync(connection, transaction, employee.EmployeeId, request.StartDate.Date, request.EndDate.Date, ct);
                var overtimeAmount = await GetOvertimeAmountForEmployeeAsync(connection, transaction, employee.EmployeeId, request.StartDate.Date, request.EndDate.Date, ct);
                var drawDeductionAmount = await GetDrawDeductionForEmployeeAsync(connection, transaction, employee.EmployeeCode, request.StartDate.Date, request.EndDate.Date, ct);
                var grossAmount = PayrollEngine.CalculateGrossAmount(employee.BasicSalary, pieceWageAmount, 0m, overtimeAmount);
                var netAmount = PayrollEngine.CalculateNetAmount(grossAmount, drawDeductionAmount);

                var recordId = await InsertPayrollRecordAsync(
                    connection,
                    transaction,
                    periodId,
                    employee.EmployeeId,
                    employee.BasicSalary,
                    pieceWageAmount,
                    0m,
                    overtimeAmount,
                    grossAmount,
                    drawDeductionAmount,
                    netAmount,
                    "Generated",
                    note,
                    ct);

                recordIds.Add(recordId);
                totalGross += grossAmount;
                totalNet += netAmount;

                await InsertPayrollItemAsync(connection, transaction, recordId, "BasicSalary", "راتب أساسي", 1m, employee.BasicSalary, employee.BasicSalary, null, ct);
                if (pieceWageAmount > 0m)
                {
                    await InsertPayrollItemAsync(connection, transaction, recordId, "PieceWage", "أجر القطعة", 1m, pieceWageAmount, pieceWageAmount, null, ct);
                }
                if (overtimeAmount > 0m)
                {
                    await InsertPayrollItemAsync(connection, transaction, recordId, "Overtime", "أجر إضافي", 1m, overtimeAmount, overtimeAmount, null, ct);
                }
                if (drawDeductionAmount > 0m)
                {
                    await InsertPayrollItemAsync(connection, transaction, recordId, "Deduction", "خصم سلف", 1m, drawDeductionAmount, drawDeductionAmount, "Employee draw deduction for payroll period", ct);
                }

                await LinkPieceWageRecordsAsync(connection, transaction, employee.EmployeeId, request.StartDate.Date, request.EndDate.Date, periodId, recordId, ct);
            }

            await transaction.CommitAsync(ct);
            return new GeneratePayrollResultDto(periodId, normalizedCode, recordIds.Count, totalGross, totalNet, generationTime, recordIds);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore rollback failures after the primary exception is already active.
            }

            throw;
        }
    }

    public async Task<PayrollPeriodDto?> ApproveAsync(int periodId, ApprovePayrollRequestDto request, CancellationToken ct)
    {
        if (periodId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodId), "Payroll period id must be positive.");
        }

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) ? "System" : request.ApprovedBy.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var existingPeriod = await GetPeriodByIdForWriteAsync(connection, transaction, periodId, ct);
            if (existingPeriod is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var effectiveNotes = existingPeriod.Notes is null
                ? (notes is null ? $"Approved by {approvedBy}" : $"{notes} | Approved by {approvedBy}")
                : string.IsNullOrWhiteSpace(notes)
                    ? existingPeriod.Notes.Contains("Approved by", StringComparison.OrdinalIgnoreCase) ? existingPeriod.Notes : $"{existingPeriod.Notes}; Approved by {approvedBy}"
                    : $"{existingPeriod.Notes}; {notes} | Approved by {approvedBy}";

            const string updatePeriodSql = "UPDATE dbo.PayrollPeriods SET Status = @status, ApprovedAt = @approvedAt, Notes = @notes WHERE PayrollPeriodId = @periodId";
            await using (var cmd = new SqlCommand(updatePeriodSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@status", "Approved");
                cmd.Parameters.AddWithValue("@approvedAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(effectiveNotes) ? (object)DBNull.Value : effectiveNotes);
                cmd.Parameters.AddWithValue("@periodId", periodId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            const string updateRecordsSql = "UPDATE dbo.PayrollRecords SET Status = @status WHERE PayrollPeriodId = @periodId";
            await using (var cmd = new SqlCommand(updateRecordsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@status", "Approved");
                cmd.Parameters.AddWithValue("@periodId", periodId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return await GetPeriodAsync(periodId, ct);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore rollback failures after the primary exception is already active.
            }

            throw;
        }
    }

    public async Task<PayrollRecordDto?> PayAsync(int recordId, PayrollPaymentRequestDto request, CancellationToken ct)
    {
        if (recordId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordId), "Payroll record id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            throw new ArgumentException("Payment method is required.", nameof(request));
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            var record = await GetRecordForWriteAsync(connection, transaction, recordId, ct);
            if (record is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var referenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? $"PAY-{recordId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : request.ReferenceNumber.Trim();
            var paymentAmount = request.OverrideAmount ?? record.NetAmount;
            var paymentNote = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Payroll payment via {request.PaymentMethod.Trim()}"
                : request.Notes.Trim();

            if (!string.IsNullOrWhiteSpace(referenceNumber))
            {
                paymentNote = $"{paymentNote} | Ref: {referenceNumber}";
            }

            const string updateRecordSql = "UPDATE dbo.PayrollRecords SET Status = @status, Notes = @notes WHERE PayrollRecordId = @recordId";
            await using (var cmd = new SqlCommand(updateRecordSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@status", "Paid");
                cmd.Parameters.AddWithValue("@notes", paymentNote);
                cmd.Parameters.AddWithValue("@recordId", recordId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            const string insertFinancialSql = @"INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt)
VALUES (@referenceNumber, @transactionType, @amount, @description, @createdAt)";
            await using (var cmd = new SqlCommand(insertFinancialSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber);
                cmd.Parameters.AddWithValue("@transactionType", "PayrollPayment");
                cmd.Parameters.AddWithValue("@amount", paymentAmount);
                cmd.Parameters.AddWithValue("@description", $"Payroll payment for record {recordId}");
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await FinancialTransactionJournalPoster.TryCreateJournalEntryAsync(
                connection,
                transaction,
                referenceNumber,
                "PayrollPayment",
                paymentAmount,
                $"Payroll payment for record {recordId}",
                ct);

            await transaction.CommitAsync(ct);
            return await GetRecordAsync(recordId, ct);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore rollback failures after the primary exception is already active.
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<PayrollSettlementDto>> GetSettlementsAsync(int employeeId, CancellationToken ct)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = @"SELECT s.SettlementId, s.DrawId, e.EmployeeCode, s.SettlementDate, s.Amount, s.Notes, s.JournalEntryId
FROM dbo.EmployeeDrawSettlements s
INNER JOIN dbo.Employee_Draws d ON d.DrawID = s.DrawId
INNER JOIN dbo.Employees e ON e.EmployeeCode = d.EmployeeCode
WHERE e.EmployeeId = @employeeId
ORDER BY s.SettlementDate DESC, s.SettlementId DESC";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<PayrollSettlementDto>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new PayrollSettlementDto(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.NullableString("EmployeeCode"),
                reader.GetDateTime(3),
                reader.GetDecimal(4),
                reader.NullableString("Notes"),
                reader.NullableInt32("JournalEntryId")));
        }

        return items;
    }

    private static PayrollPeriodDto MapPeriod(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetString(1), reader.GetDateTime(2), reader.GetDateTime(3), reader.GetString(4), reader.NullableString("Notes"), reader.NullableDateTime("GeneratedAt"), reader.NullableDateTime("ApprovedAt"), reader.GetDateTime(8));
    private static PayrollRecordDto MapRecord(SqlDataReader reader) => new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10), reader.NullableString("Notes"), reader.GetDateTime(12));

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, int? id, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(map(reader));
        }

        return items;
    }

    private static async Task<PayrollPeriodDto?> ReadPeriodForRangeAsync(SqlConnection connection, SqlTransaction transaction, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        const string sql = "SELECT TOP (1) PayrollPeriodId, PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, ApprovedAt, CreatedAt FROM dbo.PayrollPeriods WITH (UPDLOCK,HOLDLOCK) WHERE StartDate = @startDate AND EndDate = @endDate ORDER BY PayrollPeriodId DESC";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@startDate", startDate.Date);
        command.Parameters.AddWithValue("@endDate", endDate.Date);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapPeriod(reader);
    }

    private static async Task<int> InsertPayrollPeriodAsync(SqlConnection connection, SqlTransaction transaction, string periodCode, DateTime startDate, DateTime endDate, string status, string? notes, DateTime generatedAt, CancellationToken ct)
    {
        const string sql = "INSERT INTO dbo.PayrollPeriods (PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, CreatedAt) OUTPUT INSERTED.PayrollPeriodId VALUES (@periodCode, @startDate, @endDate, @status, @notes, @generatedAt, @createdAt)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodCode", periodCode);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes);
        command.Parameters.AddWithValue("@generatedAt", generatedAt);
        command.Parameters.AddWithValue("@createdAt", generatedAt);
        var result = await command.ExecuteScalarAsync(ct);
        return result is int value ? value : throw new InvalidOperationException("Payroll period was not created.");
    }

    private static async Task<List<PayrollSeedEmployee>> GetPayrollSeedEmployeesAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        const string sql = "SELECT EmployeeID, EmployeeCode, ISNULL(BasicSalary, 0) FROM dbo.Employees WHERE IsActive = 1 OR Status = 'Active' ORDER BY EmployeeID";
        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var employees = new List<PayrollSeedEmployee>();
        while (await reader.ReadAsync(ct))
        {
            employees.Add(new PayrollSeedEmployee(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2)));
        }

        return employees;
    }

    private static async Task<int> InsertPayrollRecordAsync(SqlConnection connection, SqlTransaction transaction, int periodId, int employeeId, decimal basicSalaryAmount, decimal pieceWageAmount, decimal attendanceAdjustmentAmount, decimal overtimeAmount, decimal grossAmount, decimal deductionsAmount, decimal netAmount, string status, string? notes, CancellationToken ct)
    {
        const string sql = "INSERT INTO dbo.PayrollRecords (PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt) OUTPUT INSERTED.PayrollRecordId VALUES (@periodId, @employeeId, @basicSalaryAmount, @pieceWageAmount, @attendanceAdjustmentAmount, @overtimeAmount, @grossAmount, @deductionsAmount, @netAmount, @status, @notes, @createdAt)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodId", periodId);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@basicSalaryAmount", basicSalaryAmount);
        command.Parameters.AddWithValue("@pieceWageAmount", pieceWageAmount);
        command.Parameters.AddWithValue("@attendanceAdjustmentAmount", attendanceAdjustmentAmount);
        command.Parameters.AddWithValue("@overtimeAmount", overtimeAmount);
        command.Parameters.AddWithValue("@grossAmount", grossAmount);
        command.Parameters.AddWithValue("@deductionsAmount", deductionsAmount);
        command.Parameters.AddWithValue("@netAmount", netAmount);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        var result = await command.ExecuteScalarAsync(ct);
        return result is int value ? value : throw new InvalidOperationException("Payroll record was not created.");
    }

    private static async Task InsertPayrollItemAsync(SqlConnection connection, SqlTransaction transaction, int recordId, string itemType, string itemName, decimal quantity, decimal rate, decimal amount, string? notes, CancellationToken ct)
    {
        const string sql = "INSERT INTO dbo.PayrollItems (PayrollRecordId, ItemType, ItemName, Quantity, Rate, Amount, Notes) VALUES (@recordId, @itemType, @itemName, @quantity, @rate, @amount, @notes)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@recordId", recordId);
        command.Parameters.AddWithValue("@itemType", itemType);
        command.Parameters.AddWithValue("@itemName", itemName);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@rate", rate);
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<decimal> GetPieceWageTotalForEmployeeAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        const string sql = "SELECT ISNULL(SUM(CAST(TotalWage AS decimal(18,2))), 0) FROM dbo.PieceWageRecords WHERE EmployeeId = @employeeId AND CreatedAt >= @startDate AND CreatedAt < DATEADD(day, 1, @endDate) AND (PayrollRecordId IS NULL OR PayrollRecordId = 0) AND Status IN ('PendingPayroll', 'Generated', 'Approved', 'Paid')";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);
        var result = await command.ExecuteScalarAsync(ct);
        return ToDecimalOrZero(result);
    }

    private static async Task<decimal> GetOvertimeAmountForEmployeeAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        const string hoursSql = "SELECT ISNULL(SUM(CAST(OvertimeHours AS decimal(18,2))), 0) FROM dbo.EmployeeAttendances WHERE EmployeeId = @employeeId AND AttendanceDate >= @startDate AND AttendanceDate < DATEADD(day, 1, @endDate)";
        await using var hoursCommand = new SqlCommand(hoursSql, connection, transaction);
        hoursCommand.Parameters.AddWithValue("@employeeId", employeeId);
        hoursCommand.Parameters.AddWithValue("@startDate", startDate);
        hoursCommand.Parameters.AddWithValue("@endDate", endDate);
        var totalHours = await hoursCommand.ExecuteScalarAsync(ct);
        var overtimeHours = ToDecimalOrZero(totalHours);

        const string rateSql = "SELECT ISNULL(OvertimeHourlyRate, 0) FROM dbo.Employees WHERE EmployeeID = @employeeId";
        await using var rateCommand = new SqlCommand(rateSql, connection, transaction);
        rateCommand.Parameters.AddWithValue("@employeeId", employeeId);
        var rateResult = await rateCommand.ExecuteScalarAsync(ct);
        var hourlyRate = ToDecimalOrZero(rateResult);
        return overtimeHours * hourlyRate;
    }

    private static async Task<decimal> GetDrawDeductionForEmployeeAsync(SqlConnection connection, SqlTransaction transaction, string employeeCode, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        const string sql = "SELECT ISNULL(SUM(CAST(Amount AS decimal(18,2))), 0) FROM dbo.Employee_Draws WHERE EmployeeCode = @employeeCode AND DrawDate >= @startDate AND DrawDate < DATEADD(day, 1, @endDate)";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeCode", employeeCode);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);
        var result = await command.ExecuteScalarAsync(ct);
        return ToDecimalOrZero(result);
    }

    private static async Task LinkPieceWageRecordsAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, DateTime startDate, DateTime endDate, int periodId, int recordId, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.PieceWageRecords SET PayrollPeriodId = @periodId, PayrollRecordId = @recordId, Status = @status WHERE EmployeeId = @employeeId AND CreatedAt >= @startDate AND CreatedAt < DATEADD(day, 1, @endDate) AND (PayrollRecordId IS NULL OR PayrollRecordId = 0) AND Status IN ('PendingPayroll', 'Generated')";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodId", periodId);
        command.Parameters.AddWithValue("@recordId", recordId);
        command.Parameters.AddWithValue("@status", "Generated");
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<int>> GetRecordIdsAsync(SqlConnection connection, SqlTransaction transaction, int periodId, CancellationToken ct)
    {
        const string sql = "SELECT PayrollRecordId FROM dbo.PayrollRecords WHERE PayrollPeriodId = @periodId ORDER BY PayrollRecordId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodId", periodId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var ids = new List<int>();
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    private static async Task<IReadOnlyList<EmployeePayrollSummaryDto>> GetPeriodSummariesAsync(SqlConnection connection, SqlTransaction transaction, int periodId, CancellationToken ct)
    {
        const string sql = "SELECT EmployeeId, PayrollPeriodId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount FROM dbo.PayrollRecords WHERE PayrollPeriodId = @periodId ORDER BY EmployeeId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodId", periodId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<EmployeePayrollSummaryDto>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(new EmployeePayrollSummaryDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8)));
        }

        return items;
    }

    private static async Task<PayrollPeriodDto?> GetPeriodByIdForWriteAsync(SqlConnection connection, SqlTransaction transaction, int periodId, CancellationToken ct)
    {
        const string sql = "SELECT PayrollPeriodId, PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, ApprovedAt, CreatedAt FROM dbo.PayrollPeriods WITH (UPDLOCK,HOLDLOCK) WHERE PayrollPeriodId = @periodId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@periodId", periodId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapPeriod(reader);
    }

    private static async Task<PayrollRecordDto?> GetRecordForWriteAsync(SqlConnection connection, SqlTransaction transaction, int recordId, CancellationToken ct)
    {
        const string sql = "SELECT PayrollRecordId, PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt FROM dbo.PayrollRecords WITH (UPDLOCK,HOLDLOCK) WHERE PayrollRecordId = @recordId";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@recordId", recordId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapRecord(reader);
    }

    private static decimal ToDecimalOrZero(object? value)
    {
        if (value is null)
        {
            return 0m;
        }

        return value switch
        {
            decimal amount => amount,
            double dbl => Convert.ToDecimal(dbl),
            float flt => Convert.ToDecimal(flt),
            int integer => integer,
            long longValue => longValue,
            short shortValue => shortValue,
            _ => Convert.ToDecimal(value)
        };
    }

    private sealed record PayrollSeedEmployee(int EmployeeId, string EmployeeCode, decimal BasicSalary);
}