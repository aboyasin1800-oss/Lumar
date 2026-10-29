using System.Globalization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.DTOs.Payroll;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class EmployeeRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IEmployeeRepository
{
    private static readonly string[] OfficialDepartmentNames =
    [
        "خياط",
        "قصاص",
        "محاسب",
        "كواي",
        "جودة",
        "تجميع",
        "استقبال",
        "مدير عام",
        "القسم العام"
    ];

    public Task<IReadOnlyList<EmployeeListDto>> GetAllAsync(CancellationToken ct) => QueryAsync("SELECT EmployeeID, EmployeeCode, EmployeeName, JobTitle, PhoneNumber, IsActive, Status, DepartmentId FROM dbo.Employees ORDER BY EmployeeName, EmployeeID", null, reader => new EmployeeListDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("JobTitle"), reader.NullableString("PhoneNumber"), reader.NullableBoolean("IsActive"), reader.GetString(6), reader.GetInt32(7)), ct);
    public async Task<EmployeeDetailsDto?> GetByIdAsync(int employeeId, CancellationToken ct) => (await QueryAsync("SELECT EmployeeID, EmployeeCode, EmployeeName, JobTitle, ScannerCode, PhoneNumber, BaseSalary, Notes, IsActive, SalaryType, FixedSalary, FullName, NationalId, Phone, Email, Address, HireDate, TerminationDate, Status, DepartmentId, BasicSalary, PieceWageRate, OvertimeHourlyRate, CreatedAt, UpdatedAt FROM dbo.Employees WHERE EmployeeID = @id", employeeId, reader => new EmployeeDetailsDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("JobTitle"), reader.NullableString("ScannerCode"), reader.NullableString("PhoneNumber"), reader.NullableDecimal("BaseSalary"), reader.NullableString("Notes"), reader.NullableBoolean("IsActive"), reader.NullableString("SalaryType"), reader.NullableDecimal("FixedSalary"), reader.GetString(11), reader.NullableString("NationalId"), reader.NullableString("Phone"), reader.NullableString("Email"), reader.NullableString("Address"), reader.GetDateTime(16), reader.NullableDateTime("TerminationDate"), reader.GetString(18), reader.GetInt32(19), reader.GetDecimal(20), reader.GetDecimal(21), reader.GetDecimal(22), reader.GetDateTime(23), reader.NullableDateTime("UpdatedAt"), null, null, null, null, null, null, null, null, null, null), ct)).FirstOrDefault();
    public async Task<EmployeeDetailsDto?> CreateAsync(CreateEmployeeDto request, CancellationToken ct)
    {
        var normalized = EmployeeWriteValidator.ValidateForCreate(request);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var employeeCode = string.IsNullOrWhiteSpace(normalized.EmployeeCode)
                ? await GenerateEmployeeCodeAsync(connection, (SqlTransaction)transaction, ct)
                : normalized.EmployeeCode.Trim();

            await EnsureDepartmentExistsAsync(connection, (SqlTransaction)transaction, normalized.DepartmentId, ct);

            await using (var duplicateCheck = new SqlCommand("SELECT TOP (1) EmployeeID FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeCode = @code", connection, (SqlTransaction)transaction))
            {
                duplicateCheck.Parameters.AddWithValue("@code", employeeCode);
                if (await duplicateCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("EmployeeCode already exists.");
            }

            const string sql = @"INSERT INTO dbo.Employees (EmployeeCode, EmployeeName, FullName, DepartmentId, PhoneNumber, BasicSalary, PieceWageRate, OvertimeHourlyRate, SalaryType, HireDate, Status, IsActive, CreatedAt, UpdatedAt)
OUTPUT INSERTED.EmployeeID
VALUES (@employeeCode, @employeeName, @fullName, @departmentId, @phoneNumber, @basicSalary, @pieceWageRate, @overtimeHourlyRate, @salaryType, @hireDate, @status, @isActive, SYSDATETIME(), SYSDATETIME());";

            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@employeeCode", employeeCode);
            command.Parameters.AddWithValue("@employeeName", normalized.FullName!.Trim());
            command.Parameters.AddWithValue("@fullName", normalized.FullName.Trim());
            command.Parameters.AddWithValue("@departmentId", normalized.DepartmentId);
            command.Parameters.AddWithValue("@phoneNumber", string.IsNullOrWhiteSpace(normalized.PhoneNumber) ? (object)DBNull.Value : normalized.PhoneNumber.Trim());
            command.Parameters.AddWithValue("@basicSalary", normalized.BasicSalary);
            command.Parameters.AddWithValue("@pieceWageRate", 0m);
            command.Parameters.AddWithValue("@overtimeHourlyRate", 0m);
            command.Parameters.AddWithValue("@salaryType", normalized.SalaryType!);
            command.Parameters.AddWithValue("@hireDate", normalized.HireDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", normalized.Status!);
            command.Parameters.AddWithValue("@isActive", normalized.Status!.Equals("Active", StringComparison.OrdinalIgnoreCase));

            var employeeId = (int)(await command.ExecuteScalarAsync(ct))!;
            await SaveEmployeeSupportingDataAsync(connection, (SqlTransaction)transaction, employeeId, normalized.ContractNumber, normalized.ContractType, normalized.ContractStatus, normalized.ContractStartDate, normalized.ContractEndDate, normalized.ContractSignedDate, normalized.ContractNotes, normalized.ContractFilePath, normalized.PieceRates, ct);
            await transaction.CommitAsync(ct);
            return await GetByIdAsync(employeeId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<string> GenerateEmployeeCodeAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        var prefix = await SystemCodeGenerator.ResolvePrefixAsync(connection, transaction, "EmployeeCodePrefix", "MO", ct);
        var number = await SystemCodeGenerator.GetNextNumberAsync(connection, transaction, "dbo.Employees", "EmployeeCode", "EmployeeCodePrefix", "MO", ct);
        return prefix + number.ToString(CultureInfo.InvariantCulture);
    }

    private static async Task EnsureDepartmentExistsAsync(SqlConnection connection, SqlTransaction transaction, int departmentId, CancellationToken ct)
    {
        if (departmentId <= 0)
            throw new ArgumentException("DepartmentId is required.");

        await using var command = new SqlCommand(
            "SELECT TOP (1) DepartmentId FROM dbo.Departments WITH (UPDLOCK, HOLDLOCK) WHERE DepartmentId = @id AND IsActive = 1",
            connection,
            transaction);

        command.Parameters.AddWithValue("@id", departmentId);
        if (await command.ExecuteScalarAsync(ct) is null)
            throw new ArgumentException("DepartmentId is invalid or inactive.");
    }

    public async Task<EmployeeDetailsDto?> UpdateAsync(int employeeId, UpdateEmployeeDto request, CancellationToken ct)
    {
        var normalized = EmployeeWriteValidator.ValidateForUpdate(request);
        var existingEmployee = await GetByIdAsync(employeeId, ct);
        if (existingEmployee is null) return null;

        var employeeCode = string.IsNullOrWhiteSpace(normalized.EmployeeCode)
            ? existingEmployee.EmployeeCode
            : normalized.EmployeeCode.Trim();

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await EnsureDepartmentExistsAsync(connection, (SqlTransaction)transaction, normalized.DepartmentId, ct);

            await using (var duplicateCheck = new SqlCommand("SELECT TOP (1) EmployeeID FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK) WHERE EmployeeCode = @code AND EmployeeID <> @id", connection, (SqlTransaction)transaction))
            {
                duplicateCheck.Parameters.AddWithValue("@code", employeeCode);
                duplicateCheck.Parameters.AddWithValue("@id", employeeId);
                if (await duplicateCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("EmployeeCode already exists.");
            }

            const string sql = @"UPDATE dbo.Employees
SET EmployeeCode = @employeeCode,
    EmployeeName = @employeeName,
    FullName = @fullName,
    DepartmentId = @departmentId,
    PhoneNumber = @phoneNumber,
    BasicSalary = @basicSalary,
    PieceWageRate = @pieceWageRate,
    OvertimeHourlyRate = @overtimeHourlyRate,
    SalaryType = @salaryType,
    HireDate = @hireDate,
    Status = @status,
    IsActive = @isActive,
    UpdatedAt = SYSDATETIME()
WHERE EmployeeID = @id;";

            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", employeeId);
            command.Parameters.AddWithValue("@employeeCode", employeeCode);
            command.Parameters.AddWithValue("@employeeName", normalized.FullName!.Trim());
            command.Parameters.AddWithValue("@fullName", normalized.FullName.Trim());
            command.Parameters.AddWithValue("@departmentId", normalized.DepartmentId);
            command.Parameters.AddWithValue("@phoneNumber", string.IsNullOrWhiteSpace(normalized.PhoneNumber) ? (object)DBNull.Value : normalized.PhoneNumber.Trim());
            command.Parameters.AddWithValue("@basicSalary", normalized.BasicSalary);
            command.Parameters.AddWithValue("@pieceWageRate", 0m);
            command.Parameters.AddWithValue("@overtimeHourlyRate", 0m);
            command.Parameters.AddWithValue("@salaryType", normalized.SalaryType!);
            command.Parameters.AddWithValue("@hireDate", normalized.HireDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@status", normalized.Status!);
            command.Parameters.AddWithValue("@isActive", normalized.IsActive);
            await command.ExecuteNonQueryAsync(ct);

            await SaveEmployeeSupportingDataAsync(connection, (SqlTransaction)transaction, employeeId, normalized.ContractNumber, normalized.ContractType, normalized.ContractStatus, normalized.ContractStartDate, normalized.ContractEndDate, normalized.ContractSignedDate, normalized.ContractNotes, normalized.ContractFilePath, normalized.PieceRates, ct);
            await transaction.CommitAsync(ct);
            return await GetByIdAsync(employeeId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<EmployeeDetailsDto?> ActivateAsync(int employeeId, CancellationToken ct)
    {
        if (await GetByIdAsync(employeeId, ct) is null) return null;
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "UPDATE dbo.Employees SET Status = @status, IsActive = 1, UpdatedAt = SYSDATETIME() WHERE EmployeeID = @id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", employeeId);
        command.Parameters.AddWithValue("@status", EmployeeWriteValidator.NormalizeStatus("Active"));
        await command.ExecuteNonQueryAsync(ct);
        return await GetByIdAsync(employeeId, ct);
    }

    public async Task<EmployeeDetailsDto?> DeactivateAsync(int employeeId, CancellationToken ct)
    {
        if (await GetByIdAsync(employeeId, ct) is null) return null;
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "UPDATE dbo.Employees SET Status = @status, IsActive = 0, UpdatedAt = SYSDATETIME() WHERE EmployeeID = @id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", employeeId);
        command.Parameters.AddWithValue("@status", EmployeeWriteValidator.NormalizeStatus("Inactive"));
        await command.ExecuteNonQueryAsync(ct);
        return await GetByIdAsync(employeeId, ct);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct)
    {
        var names = OfficialDepartmentNames
            .Select(static name => "N'" + name.Replace("'", "''") + "'")
            .ToArray();
        var clause = names.Length == 0 ? "1 = 0" : $"DepartmentName IN ({string.Join(", ", names)})";
        return await QueryAsync($"SELECT DepartmentId, DepartmentCode, DepartmentName, Description, IsActive, CreatedAt, UpdatedAt FROM dbo.Departments WHERE {clause} AND IsActive = 1 ORDER BY DepartmentName, DepartmentId", null, reader => new DepartmentDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Description"), reader.GetBoolean(4), reader.GetDateTime(5), reader.NullableDateTime("UpdatedAt")), ct);
    }
    public Task<IReadOnlyList<EmployeeAttendanceDto>> GetAttendanceAsync(int employeeId, CancellationToken ct) => QueryAsync("SELECT EmployeeAttendanceId, EmployeeId, AttendanceDate, CheckInTime, CheckOutTime, WorkedHours, OvertimeHours, IsAbsent, AbsenceReason, Notes, CreatedAt FROM dbo.EmployeeAttendances WHERE EmployeeId = @id ORDER BY AttendanceDate DESC, EmployeeAttendanceId DESC", employeeId, reader => new EmployeeAttendanceDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDateTime(2), reader.NullableDateTime("CheckInTime"), reader.NullableDateTime("CheckOutTime"), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetBoolean(7), reader.NullableString("AbsenceReason"), reader.NullableString("Notes"), reader.GetDateTime(10)), ct);
    public Task<IReadOnlyList<LeaveRequestDto>> GetLeaveRequestsAsync(int employeeId, CancellationToken ct) => QueryAsync("SELECT LeaveRequestId, EmployeeId, LeaveType, StartDate, EndDate, RequestedDays, Status, Reason, ApprovedBy, ApprovedAt, CreatedAt FROM dbo.LeaveRequests WHERE EmployeeId = @id ORDER BY CreatedAt DESC, LeaveRequestId DESC", employeeId, reader => new LeaveRequestDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetDecimal(5), reader.GetString(6), reader.NullableString("Reason"), reader.NullableString("ApprovedBy"), reader.NullableDateTime("ApprovedAt"), reader.GetDateTime(10)), ct);
    public Task<IReadOnlyList<EmployeeDrawDto>> GetDrawsAsync(string employeeCode, CancellationToken ct) => QueryAsync("SELECT DrawID, EmployeeCode, DrawDate, Amount, Notes FROM dbo.Employee_Draws WHERE EmployeeCode = @code ORDER BY DrawDate DESC, DrawID DESC", employeeCode, reader => new EmployeeDrawDto(reader.GetInt32(0), reader.NullableString("EmployeeCode"), reader.NullableDateTime("DrawDate"), reader.NullableDecimal("Amount"), reader.NullableString("Notes")), ct);
    public Task<IReadOnlyList<TrackingEventDto>> GetSelfProductionAsync(int employeeId, CancellationToken ct) => QueryAsync(
        "SELECT te.TrackingEventID, te.OrderItemID, te.OrderID, te.TrackingCode, te.Stage, te.Status, te.EventTime, te.EmployeeCode, te.Notes, te.IsReverted, te.RevertedAt, te.PieceID, te.ReadyMadeProductionOrderPieceInstanceId FROM dbo.TrackingEvents te INNER JOIN dbo.Employees e ON e.EmployeeCode = te.EmployeeCode WHERE e.EmployeeId = @id ORDER BY te.EventTime DESC, te.TrackingEventID DESC",
        employeeId,
        reader => new TrackingEventDto(reader.GetInt32(0), reader.NullableInt32("OrderItemID"), reader.NullableInt32("OrderID"), reader.NullableString("TrackingCode"), reader.GetString(4), reader.GetString(5), reader.GetDateTime(6), reader.NullableString("EmployeeCode"), reader.NullableString("Notes"), reader.GetBoolean(9), reader.NullableDateTime("RevertedAt"), reader.NullableInt32("PieceID"), reader.NullableInt32("ReadyMadeProductionOrderPieceInstanceId")),
        ct);
    public Task<IReadOnlyList<PieceWageRecordDto>> GetSelfPieceWagesAsync(int employeeId, CancellationToken ct) => QueryAsync(
        "SELECT PieceWageRecordID, OrderID, OrderItemID, PieceID, TrackingEventID, EmployeeId, EmployeeCode, PieceType, Stage, Quantity, WageRate, TotalWage, PayrollPeriodId, PayrollRecordId, Status, Notes, CreatedAt FROM dbo.PieceWageRecords WHERE EmployeeId = @id ORDER BY CreatedAt DESC, PieceWageRecordID DESC",
        employeeId,
        reader => new PieceWageRecordDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.NullableInt32("EmployeeId"), reader.NullableString("EmployeeCode"), reader.GetString(7), reader.GetString(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.NullableInt32("PayrollPeriodId"), reader.NullableInt32("PayrollRecordId"), reader.GetString(14), reader.NullableString("Notes"), reader.GetDateTime(16)),
        ct);
    public Task<IReadOnlyList<EmployeeDrawDto>> GetSelfDrawsAsync(int employeeId, CancellationToken ct) => QueryAsync(
        "SELECT d.DrawID, d.EmployeeCode, d.DrawDate, d.Amount, d.Notes FROM dbo.Employee_Draws d INNER JOIN dbo.Employees e ON e.EmployeeCode = d.EmployeeCode WHERE e.EmployeeId = @id ORDER BY d.DrawDate DESC, d.DrawID DESC",
        employeeId,
        reader => new EmployeeDrawDto(reader.GetInt32(0), reader.NullableString("EmployeeCode"), reader.NullableDateTime("DrawDate"), reader.NullableDecimal("Amount"), reader.NullableString("Notes")),
        ct);
    public Task<IReadOnlyList<PayrollSettlementDto>> GetSelfSettlementsAsync(int employeeId, CancellationToken ct) => QueryAsync(
        "SELECT s.SettlementId, s.DrawId, d.EmployeeCode, s.SettlementDate, s.Amount, s.Notes, s.JournalEntryId FROM dbo.EmployeeDrawSettlements s INNER JOIN dbo.Employee_Draws d ON d.DrawID = s.DrawId INNER JOIN dbo.Employees e ON e.EmployeeCode = d.EmployeeCode WHERE e.EmployeeId = @id ORDER BY s.SettlementDate DESC, s.SettlementId DESC",
        employeeId,
        reader => new PayrollSettlementDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableString("EmployeeCode"), reader.GetDateTime(3), reader.GetDecimal(4), reader.NullableString("Notes"), reader.NullableInt32("JournalEntryId")),
        ct);
    public async Task<EmployeeSelfFinancialSummaryDto?> GetSelfFinancialSummaryAsync(int employeeId, int? periodId, CancellationToken ct)
    {
        var sql = periodId.HasValue
            ? "SELECT TOP (1) EmployeeId, PayrollPeriodId, BasicSalaryAmount, PieceWageAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, CreatedAt FROM dbo.PayrollRecords WHERE EmployeeId = @id AND PayrollPeriodId = @periodId ORDER BY PayrollPeriodId DESC, CreatedAt DESC"
            : "SELECT TOP (1) EmployeeId, PayrollPeriodId, BasicSalaryAmount, PieceWageAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, CreatedAt FROM dbo.PayrollRecords WHERE EmployeeId = @id ORDER BY PayrollPeriodId DESC, CreatedAt DESC";

        var parameters = new Dictionary<string, object?>
        {
            ["@id"] = employeeId,
            ["@periodId"] = periodId.HasValue ? (object)periodId.Value : DBNull.Value
        };

        if (!periodId.HasValue)
        {
            parameters.Remove("@periodId");
        }

        var rows = await QueryAsync(sql, parameters, reader => new EmployeeSelfFinancialSummaryDto(reader.GetInt32(0), reader.NullableInt32("PayrollPeriodId"), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.NullableString("Status"), reader.NullableDateTime("CreatedAt")), ct);
        return rows.FirstOrDefault();
    }
    public async Task<EmployeeSelfMonthlyStatementDto?> GetSelfMonthlyStatementAsync(int employeeId, int periodId, CancellationToken ct)
    {
        var rows = await QueryAsync(
            "SELECT p.PayrollPeriodId, p.PeriodCode, p.StartDate, p.EndDate, p.Status, r.BasicSalaryAmount, r.PieceWageAmount, r.OvertimeAmount, r.GrossAmount, r.DeductionsAmount, ISNULL((SELECT SUM(d.Amount) FROM dbo.Employee_Draws d INNER JOIN dbo.Employees e ON e.EmployeeCode = d.EmployeeCode WHERE e.EmployeeId = @id AND d.DrawDate >= p.StartDate AND d.DrawDate < DATEADD(day, 1, p.EndDate)), 0) AS DrawAmount, ISNULL((SELECT SUM(s.Amount) FROM dbo.EmployeeDrawSettlements s INNER JOIN dbo.Employee_Draws d ON d.DrawID = s.DrawId INNER JOIN dbo.Employees e ON e.EmployeeCode = d.EmployeeCode WHERE e.EmployeeId = @id AND s.SettlementDate >= p.StartDate AND s.SettlementDate < DATEADD(day, 1, p.EndDate)), 0) AS SettlementAmount, r.NetAmount, p.GeneratedAt, p.ApprovedAt FROM dbo.PayrollPeriods p LEFT JOIN dbo.PayrollRecords r ON r.PayrollPeriodId = p.PayrollPeriodId AND r.EmployeeId = @id WHERE p.PayrollPeriodId = @periodId",
            new Dictionary<string, object?>
            {
                ["@id"] = employeeId,
                ["@periodId"] = periodId
            },
            reader => new EmployeeSelfMonthlyStatementDto(reader.GetInt32(0), reader.GetString(1), reader.GetDateTime(2), reader.GetDateTime(3), reader.GetString(4), reader.NullableDecimal("BasicSalaryAmount"), reader.NullableDecimal("PieceWageAmount"), reader.NullableDecimal("OvertimeAmount"), reader.NullableDecimal("GrossAmount"), reader.NullableDecimal("DeductionsAmount"), reader.NullableDecimal("DrawAmount"), reader.NullableDecimal("SettlementAmount"), reader.NullableDecimal("NetAmount"), reader.NullableDateTime("GeneratedAt"), reader.NullableDateTime("ApprovedAt")),
            ct);
        return rows.FirstOrDefault();
    }
    public Task<IReadOnlyList<EmployeeDocumentDto>> GetDocumentsAsync(int employeeId, CancellationToken ct) => QueryAsync("SELECT EmployeeDocumentId, EmployeeId, DocumentType, DocumentNumber, IssueDate, ExpiryDate, FilePath, Notes, CreatedAt FROM dbo.EmployeeDocuments WHERE EmployeeId = @id ORDER BY CreatedAt DESC, EmployeeDocumentId DESC", employeeId, reader => new EmployeeDocumentDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.NullableString("DocumentNumber"), reader.NullableDateTime("IssueDate"), reader.NullableDateTime("ExpiryDate"), reader.NullableString("FilePath"), reader.NullableString("Notes"), reader.GetDateTime(8)), ct);
    public async Task<IReadOnlyList<EmployeeWorkflowDto>> GetWorkflowAsync(string employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return Array.Empty<EmployeeWorkflowDto>();
        }

        var employeeRows = await QueryAsync(
            @"SELECT TOP (1) EmployeeId, EmployeeCode, ScannerCode
                FROM dbo.Employees WITH (NOLOCK)
                WHERE EmployeeCode = @code AND IsActive = 1;",
            employeeCode,
            reader => new
            {
                EmployeeId = reader.GetInt32(0),
                EmployeeCode = reader.GetString(1),
                ScannerCode = reader.IsDBNull(2) ? null : reader.GetString(2)
            },
            ct);

        var employee = employeeRows.FirstOrDefault();
        if (employee is null)
        {
            return Array.Empty<EmployeeWorkflowDto>();
        }

        var pieceRateSql = @"
            SELECT DISTINCT e.EmployeeCode, a.Stage AS WorkStage
            FROM dbo.Employees e
            INNER JOIN dbo.EmployeePieceRateAssignments a ON a.EmployeeId = e.EmployeeId
            WHERE e.EmployeeCode = @code
              AND a.IsActive = 1
              AND (a.EffectiveFrom IS NULL OR a.EffectiveFrom <= SYSUTCDATETIME())
              AND (a.EffectiveTo IS NULL OR a.EffectiveTo >= SYSUTCDATETIME())
            UNION ALL
            SELECT DISTINCT e.EmployeeCode, p.Stage AS WorkStage
            FROM dbo.Employees e
            INNER JOIN dbo.Scanners s ON s.ScannerCode = e.ScannerCode
            INNER JOIN dbo.ScannerStageProductPermissions p ON p.ScannerId = s.ScannerId
            WHERE e.EmployeeCode = @code
              AND e.ScannerCode IS NOT NULL
              AND e.ScannerCode <> ''
              AND p.IsActive = 1;";

        var stages = await QueryAsync(pieceRateSql, employeeCode, reader => new EmployeeWorkflowDto(reader.GetString(0), reader.NullableString("WorkStage")), ct);
        return stages
            .Where(item => !string.IsNullOrWhiteSpace(item.WorkStage))
            .GroupBy(item => item.WorkStage, StringComparer.OrdinalIgnoreCase)
            .Select(group => new EmployeeWorkflowDto(employee.EmployeeCode, group.Key))
            .OrderBy(item => item.WorkStage, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<IReadOnlyList<EmployeePieceRateAssignmentDto>> GetPieceRateAssignmentsAsync(int employeeId, CancellationToken ct) => QueryAsync("SELECT EmployeePieceRateAssignmentId, EmployeeId, PieceType, Stage, Rate, EffectiveFrom, EffectiveTo, IsActive, CreatedAt FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @id ORDER BY IsActive DESC, EffectiveFrom DESC, EmployeePieceRateAssignmentId DESC", employeeId, reader => new EmployeePieceRateAssignmentDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.NullableDateTime("EffectiveFrom"), reader.NullableDateTime("EffectiveTo"), reader.GetBoolean(7), reader.GetDateTime(8)), ct);
    public async Task<IReadOnlyList<EmployeeContractTemplateDto>> GetContractTemplatesAsync(CancellationToken ct)
    {
        return await QueryAsync(
            "SELECT ContractTemplateId, TemplateName, ContractType, IsActive, TemplateText, CreatedAt, UpdatedAt FROM dbo.EmployeeContractTemplates ORDER BY IsActive DESC, CreatedAt DESC",
            null,
            reader => new EmployeeContractTemplateDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.NullableDateTime("UpdatedAt")),
            ct);
    }

    public async Task<EmployeeContractTemplateDto?> GetContractTemplateByIdAsync(int templateId, CancellationToken ct)
    {
        var templates = await QueryAsync(
            "SELECT ContractTemplateId, TemplateName, ContractType, IsActive, TemplateText, CreatedAt, UpdatedAt FROM dbo.EmployeeContractTemplates WHERE ContractTemplateId = @id",
            templateId,
            reader => new EmployeeContractTemplateDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.NullableDateTime("UpdatedAt")),
            ct);
        return templates.FirstOrDefault();
    }

    public async Task<EmployeeContractTemplateDto> CreateContractTemplateAsync(EmployeeContractTemplateWriteDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateName)) throw new ArgumentException("Template name is required.");
        if (string.IsNullOrWhiteSpace(request.TemplateText)) throw new ArgumentException("Template text is required.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(@"INSERT INTO dbo.EmployeeContractTemplates (TemplateName, ContractType, IsActive, TemplateText, CreatedAt, UpdatedAt)
OUTPUT INSERTED.ContractTemplateId, INSERTED.TemplateName, INSERTED.ContractType, INSERTED.IsActive, INSERTED.TemplateText, INSERTED.CreatedAt, INSERTED.UpdatedAt
VALUES (@templateName, @contractType, @isActive, @templateText, SYSDATETIME(), SYSDATETIME());", connection);
        command.Parameters.AddWithValue("@templateName", request.TemplateName.Trim());
        command.Parameters.AddWithValue("@contractType", string.IsNullOrWhiteSpace(request.ContractType) ? "Standard" : request.ContractType.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@templateText", request.TemplateText.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Contract template could not be created.");

        return new EmployeeContractTemplateDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetString(4),
            reader.GetDateTime(5),
            reader.NullableDateTime("UpdatedAt"));
    }

    public async Task<EmployeeContractTemplateDto?> UpdateContractTemplateAsync(int templateId, EmployeeContractTemplateUpdateDto request, CancellationToken ct)
    {
        if (templateId <= 0) throw new ArgumentException("Template id must be positive.");
        if (string.IsNullOrWhiteSpace(request.TemplateName)) throw new ArgumentException("Template name is required.");
        if (string.IsNullOrWhiteSpace(request.TemplateText)) throw new ArgumentException("Template text is required.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(@"UPDATE dbo.EmployeeContractTemplates
SET TemplateName = @templateName,
    ContractType = @contractType,
    IsActive = @isActive,
    TemplateText = @templateText,
    UpdatedAt = SYSDATETIME()
WHERE ContractTemplateId = @templateId;", connection);
        command.Parameters.AddWithValue("@templateId", templateId);
        command.Parameters.AddWithValue("@templateName", request.TemplateName.Trim());
        command.Parameters.AddWithValue("@contractType", string.IsNullOrWhiteSpace(request.ContractType) ? "Standard" : request.ContractType.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@templateText", request.TemplateText.Trim());

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 0) return null;

        return await GetContractTemplateByIdAsync(templateId, ct);
    }

    public async Task<EmployeeContractDto?> GetContractAsync(int employeeId, CancellationToken ct)
    {
        var contracts = await QueryAsync("SELECT EmployeeContractId, EmployeeId, ContractTemplateId, ContractNumber, ContractType, ContractStatus, ContractStartDate, ContractEndDate, ContractSignedDate, ContractNotes, ContractFilePath, ContractText, CreatedAt, UpdatedAt FROM dbo.EmployeeContracts WHERE EmployeeId = @id ORDER BY EmployeeContractId DESC", employeeId, reader => new EmployeeContractDto(reader.GetInt32(0), reader.GetInt32(1), reader.NullableInt32("ContractTemplateId"), reader.NullableString("ContractNumber"), reader.NullableString("ContractType"), reader.NullableString("ContractStatus"), reader.NullableDateTime("ContractStartDate"), reader.NullableDateTime("ContractEndDate"), reader.NullableDateTime("ContractSignedDate"), reader.NullableString("ContractNotes"), reader.NullableString("ContractFilePath"), reader.NullableString("ContractText"), reader.GetDateTime(12), reader.NullableDateTime("UpdatedAt")), ct);
        return contracts.FirstOrDefault();
    }

    public async Task<EmployeeContractDto?> UpsertContractAsync(int employeeId, EmployeeContractDto contract, CancellationToken ct)
    {
        if (contract is null) throw new ArgumentException("Contract payload is required.");
        if (employeeId <= 0) throw new ArgumentException("Employee id must be positive.");

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using var insertCommand = new SqlCommand(@"INSERT INTO dbo.EmployeeContracts (EmployeeId, ContractTemplateId, ContractNumber, ContractType, ContractStatus, ContractStartDate, ContractEndDate, ContractSignedDate, ContractNotes, ContractFilePath, ContractText, CreatedAt, UpdatedAt)
VALUES (@employeeId, @contractTemplateId, @contractNumber, @contractType, @contractStatus, @contractStartDate, @contractEndDate, @contractSignedDate, @contractNotes, @contractFilePath, @contractText, SYSDATETIME(), SYSDATETIME());", connection, (SqlTransaction)transaction);
            insertCommand.Parameters.AddWithValue("@employeeId", employeeId);
            insertCommand.Parameters.AddWithValue("@contractTemplateId", contract.ContractTemplateId ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@contractNumber", string.IsNullOrWhiteSpace(contract.ContractNumber) ? (object)DBNull.Value : contract.ContractNumber.Trim());
            insertCommand.Parameters.AddWithValue("@contractType", string.IsNullOrWhiteSpace(contract.ContractType) ? (object)DBNull.Value : contract.ContractType.Trim());
            insertCommand.Parameters.AddWithValue("@contractStatus", string.IsNullOrWhiteSpace(contract.ContractStatus) ? "Active" : contract.ContractStatus.Trim());
            insertCommand.Parameters.AddWithValue("@contractStartDate", contract.ContractStartDate ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@contractEndDate", contract.ContractEndDate ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@contractSignedDate", contract.ContractSignedDate ?? (object)DBNull.Value);
            insertCommand.Parameters.AddWithValue("@contractNotes", string.IsNullOrWhiteSpace(contract.ContractNotes) ? (object)DBNull.Value : contract.ContractNotes.Trim());
            insertCommand.Parameters.AddWithValue("@contractFilePath", string.IsNullOrWhiteSpace(contract.ContractFilePath) ? (object)DBNull.Value : contract.ContractFilePath.Trim());
            insertCommand.Parameters.AddWithValue("@contractText", string.IsNullOrWhiteSpace(contract.ContractText) ? (object)DBNull.Value : contract.ContractText.Trim());
            await insertCommand.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);
            return await GetContractAsync(employeeId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<EmployeeContractDto?> GenerateContractAsync(int employeeId, CancellationToken ct)
    {
        var employee = await GetByIdAsync(employeeId, ct);
        if (employee is null) return null;

        var template = await GetContractTemplatesAsync(ct);
        var activeTemplate = template.FirstOrDefault(t => t.IsActive) ?? template.FirstOrDefault();
        if (activeTemplate is null) return null;

        var department = await QueryAsync("SELECT DepartmentName FROM dbo.Departments WHERE DepartmentId = @id", employee.DepartmentId, reader => reader.GetString(0), ct);
        var departmentName = department.FirstOrDefault() ?? "غير محدد";
        var employeeName = string.IsNullOrWhiteSpace(employee.FullName) ? employee.EmployeeName : employee.FullName;
        var salaryTypeLabel = string.Equals(employee.SalaryType, "PieceWage", StringComparison.OrdinalIgnoreCase) ? "أجر بالقطعة" : "راتب أساسي";
        var pieceRateText = employee.PieceWageRate > 0 ? $"أجر القطعة: {employee.PieceWageRate:N2} ريال" : "غير محدد";

        var contractText = activeTemplate.TemplateText
            .Replace("{OrganizationName}", "LUMAR ERP")
            .Replace("{EmployeeName}", employeeName)
            .Replace("{EmployeeCode}", employee.EmployeeCode)
            .Replace("{NationalId}", string.IsNullOrWhiteSpace(employee.NationalId) ? "غير محدد" : employee.NationalId)
            .Replace("{JobTitle}", string.IsNullOrWhiteSpace(employee.JobTitle) ? "غير محدد" : employee.JobTitle)
            .Replace("{Department}", departmentName)
            .Replace("{SalaryType}", salaryTypeLabel)
            .Replace("{BasicSalary}", employee.BasicSalary > 0 ? employee.BasicSalary.ToString("N2") : "0.00")
            .Replace("{PieceRateDescription}", pieceRateText)
            .Replace("{ContractStartDate}", employee.HireDate.ToString("dd/MM/yyyy"))
            .Replace("{ContractEndDate}", employee.TerminationDate?.ToString("dd/MM/yyyy") ?? "غير محدد");

        var contract = new EmployeeContractDto(
            EmployeeContractId: 0,
            EmployeeId: employeeId,
            ContractTemplateId: activeTemplate.ContractTemplateId,
            ContractNumber: $"CNT-{employee.EmployeeCode}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ContractType: activeTemplate.ContractType,
            ContractStatus: "Active",
            ContractStartDate: employee.HireDate,
            ContractEndDate: employee.TerminationDate,
            ContractSignedDate: DateTime.UtcNow,
            ContractNotes: null,
            ContractFilePath: null,
            ContractText: contractText,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

        return await UpsertContractAsync(employeeId, contract, ct);
    }

    private async Task SaveEmployeeSupportingDataAsync(SqlConnection connection, SqlTransaction transaction, int employeeId, string? contractNumber, string? contractType, string? contractStatus, DateTime? contractStartDate, DateTime? contractEndDate, DateTime? contractSignedDate, string? contractNotes, string? contractFilePath, IReadOnlyList<EmployeePieceRateAssignmentInputDto>? pieceRates, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(contractNumber) || !string.IsNullOrWhiteSpace(contractType) || !string.IsNullOrWhiteSpace(contractStatus) || contractStartDate.HasValue || contractEndDate.HasValue || contractSignedDate.HasValue || !string.IsNullOrWhiteSpace(contractNotes) || !string.IsNullOrWhiteSpace(contractFilePath))
        {
            await using var upsert = new SqlCommand(@"MERGE dbo.EmployeeContracts AS target
USING (SELECT @employeeId AS EmployeeId) AS source
ON target.EmployeeId = source.EmployeeId
WHEN MATCHED THEN
    UPDATE SET ContractNumber = @contractNumber,
               ContractType = @contractType,
               ContractStatus = @contractStatus,
               ContractStartDate = @contractStartDate,
               ContractEndDate = @contractEndDate,
               ContractSignedDate = @contractSignedDate,
               ContractNotes = @contractNotes,
               ContractFilePath = @contractFilePath,
               UpdatedAt = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (EmployeeId, ContractNumber, ContractType, ContractStatus, ContractStartDate, ContractEndDate, ContractSignedDate, ContractNotes, ContractFilePath, CreatedAt, UpdatedAt)
    VALUES (@employeeId, @contractNumber, @contractType, @contractStatus, @contractStartDate, @contractEndDate, @contractSignedDate, @contractNotes, @contractFilePath, SYSDATETIME(), SYSDATETIME());", connection, transaction);
            upsert.Parameters.AddWithValue("@employeeId", employeeId);
            upsert.Parameters.AddWithValue("@contractNumber", string.IsNullOrWhiteSpace(contractNumber) ? (object)DBNull.Value : contractNumber.Trim());
            upsert.Parameters.AddWithValue("@contractType", string.IsNullOrWhiteSpace(contractType) ? (object)DBNull.Value : contractType.Trim());
            upsert.Parameters.AddWithValue("@contractStatus", string.IsNullOrWhiteSpace(contractStatus) ? "Active" : contractStatus.Trim());
            upsert.Parameters.AddWithValue("@contractStartDate", contractStartDate ?? (object)DBNull.Value);
            upsert.Parameters.AddWithValue("@contractEndDate", contractEndDate ?? (object)DBNull.Value);
            upsert.Parameters.AddWithValue("@contractSignedDate", contractSignedDate ?? (object)DBNull.Value);
            upsert.Parameters.AddWithValue("@contractNotes", string.IsNullOrWhiteSpace(contractNotes) ? (object)DBNull.Value : contractNotes.Trim());
            upsert.Parameters.AddWithValue("@contractFilePath", string.IsNullOrWhiteSpace(contractFilePath) ? (object)DBNull.Value : contractFilePath.Trim());
            await upsert.ExecuteNonQueryAsync(ct);
        }

        var effectivePieceRates = pieceRates is null || pieceRates.Count == 0
            ? Array.Empty<EmployeePieceRateAssignmentInputDto>()
            : pieceRates;

        await using (var deleteExisting = new SqlCommand("DELETE FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId;", connection, transaction))
        {
            deleteExisting.Parameters.AddWithValue("@employeeId", employeeId);
            await deleteExisting.ExecuteNonQueryAsync(ct);
        }

        foreach (var item in effectivePieceRates)
        {
            if (item is null) continue;
            if (string.IsNullOrWhiteSpace(item.PieceType) || string.IsNullOrWhiteSpace(item.Stage)) continue;
            await using var insertAssignment = new SqlCommand(@"INSERT INTO dbo.EmployeePieceRateAssignments (EmployeeId, PieceType, Stage, Rate, EffectiveFrom, EffectiveTo, IsActive, CreatedAt)
VALUES (@employeeId, @pieceType, @stage, @rate, @effectiveFrom, @effectiveTo, @isActive, SYSDATETIME());", connection, transaction);
            insertAssignment.Parameters.AddWithValue("@employeeId", employeeId);
            insertAssignment.Parameters.AddWithValue("@pieceType", item.PieceType.Trim());
            insertAssignment.Parameters.AddWithValue("@stage", item.Stage.Trim());
            insertAssignment.Parameters.AddWithValue("@rate", item.Rate);
            insertAssignment.Parameters.AddWithValue("@effectiveFrom", item.EffectiveFrom ?? (object)DBNull.Value);
            insertAssignment.Parameters.AddWithValue("@effectiveTo", item.EffectiveTo ?? (object)DBNull.Value);
            insertAssignment.Parameters.AddWithValue("@isActive", item.IsActive);
            await insertAssignment.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameter, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);

        if (parameter is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var item in dictionary)
            {
                command.Parameters.AddWithValue(item.Key, item.Value ?? DBNull.Value);
            }
        }
        else if (parameter is not null)
        {
            var type = parameter.GetType();
            var isScalarValue = type == typeof(string)
                || type.IsPrimitive
                || type.IsEnum
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || Nullable.GetUnderlyingType(type) is not null;

            if (isScalarValue)
            {
                var parameterName = sql.Contains("@code", StringComparison.OrdinalIgnoreCase)
                    ? "@code"
                    : sql.Contains("@periodId", StringComparison.OrdinalIgnoreCase)
                        ? "@periodId"
                        : sql.Contains("@id", StringComparison.OrdinalIgnoreCase)
                            ? "@id"
                            : "@value";

                command.Parameters.AddWithValue(parameterName, parameter ?? DBNull.Value);
            }
            else
            {
                foreach (var property in type.GetProperties())
                {
                    var parameterName = property.Name.StartsWith('@') ? property.Name : $"@{property.Name}";
                    var value = property.GetValue(parameter) ?? DBNull.Value;
                    command.Parameters.AddWithValue(parameterName, value);
                }
            }
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(map(reader));
        }

        return items;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                var value = parameter.Value ?? DBNull.Value;
                command.Parameters.AddWithValue(parameter.Key, value);
            }
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(map(reader));
        }

        return items;
    }
}