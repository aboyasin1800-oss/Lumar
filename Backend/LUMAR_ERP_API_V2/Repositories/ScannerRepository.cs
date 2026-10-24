using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class ScannerRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IScannerRepository
{
    public Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct) => QueryAsync(@"SELECT s.ScannerId, s.ScannerCode, s.ScannerName, s.Description, s.IsActive, s.CreatedAt, s.UpdatedAt,
       e.EmployeeID, e.EmployeeCode, e.EmployeeName
FROM dbo.Scanners AS s
LEFT JOIN dbo.Employees AS e ON e.ScannerCode = s.ScannerCode
ORDER BY s.ScannerName, s.ScannerId;", reader => new ScannerDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Description"), reader.GetBoolean(4), reader.GetDateTime(5), reader.NullableDateTime("UpdatedAt"), reader.NullableInt32("EmployeeID"), reader.NullableString("EmployeeCode"), reader.NullableString("EmployeeName")), ct);

    public async Task<ScannerDto?> GetByIdAsync(int scannerId, CancellationToken ct)
    {
        var results = await QueryAsync(@"SELECT s.ScannerId, s.ScannerCode, s.ScannerName, s.Description, s.IsActive, s.CreatedAt, s.UpdatedAt,
       e.EmployeeID, e.EmployeeCode, e.EmployeeName
FROM dbo.Scanners AS s
LEFT JOIN dbo.Employees AS e ON e.ScannerCode = s.ScannerCode
WHERE s.ScannerId = @id;", scannerId, "@id", reader => new ScannerDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Description"), reader.GetBoolean(4), reader.GetDateTime(5), reader.NullableDateTime("UpdatedAt"), reader.NullableInt32("EmployeeID"), reader.NullableString("EmployeeCode"), reader.NullableString("EmployeeName")), ct);
        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<ScannerPermissionDto>> GetPermissionsAsync(int scannerId, CancellationToken ct)
    {
        if (scannerId <= 0)
        {
            return Array.Empty<ScannerPermissionDto>();
        }

        var sql = @"SELECT ScannerStageProductPermissionId, ScannerId, ProductTypeId, Stage, IsActive, CreatedAt, UpdatedAt
                    FROM dbo.ScannerStageProductPermissions WITH (NOLOCK)
                    WHERE ScannerId = @scannerId
                    ORDER BY ProductTypeId, Stage;";

        return await QueryAsync(sql, scannerId, "@scannerId", reader => new ScannerPermissionDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetDateTime(5),
            reader.NullableDateTime("UpdatedAt")), ct);
    }

    public async Task<ScannerDto?> CreateAsync(CreateScannerDto request, CancellationToken ct)
    {
        var normalized = ScannerWriteValidator.ValidateForCreate(request);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var duplicateCheck = new SqlCommand("SELECT TOP (1) ScannerId FROM dbo.Scanners WITH (UPDLOCK, HOLDLOCK) WHERE ScannerCode = @code", connection, (SqlTransaction)transaction))
            {
                duplicateCheck.Parameters.AddWithValue("@code", normalized.ScannerCode!);
                if (await duplicateCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("ScannerCode already exists.");
            }

            const string sql = @"INSERT INTO dbo.Scanners (ScannerCode, ScannerName, Description, IsActive, CreatedAt, UpdatedAt)
OUTPUT INSERTED.ScannerId
VALUES (@scannerCode, @scannerName, @description, @isActive, SYSDATETIME(), SYSDATETIME());";

            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@scannerCode", normalized.ScannerCode!.Trim());
            command.Parameters.AddWithValue("@scannerName", normalized.ScannerName!.Trim());
            command.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(normalized.Description) ? (object)DBNull.Value : normalized.Description.Trim());
            command.Parameters.AddWithValue("@isActive", normalized.IsActive);

            var scannerId = (int)(await command.ExecuteScalarAsync(ct))!;
            await UpsertPermissionsInTransactionAsync(connection, (SqlTransaction)transaction, scannerId, normalized.Permissions ?? Array.Empty<ScannerPermissionWriteDto>(), ct);
            await ApplyEmployeeScannerLinkAsync(connection, (SqlTransaction)transaction, normalized.ScannerCode!, normalized.EmployeeId, ct);
            await transaction.CommitAsync(ct);
            return await GetByIdAsync(scannerId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ScannerDto?> UpdateAsync(int scannerId, UpdateScannerDto request, CancellationToken ct)
    {
        var normalized = ScannerWriteValidator.ValidateForUpdate(request);
        if (await GetByIdAsync(scannerId, ct) is null) return null;

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var duplicateCheck = new SqlCommand("SELECT TOP (1) ScannerId FROM dbo.Scanners WITH (UPDLOCK, HOLDLOCK) WHERE ScannerCode = @code AND ScannerId <> @id", connection, (SqlTransaction)transaction))
            {
                duplicateCheck.Parameters.AddWithValue("@code", normalized.ScannerCode!);
                duplicateCheck.Parameters.AddWithValue("@id", scannerId);
                if (await duplicateCheck.ExecuteScalarAsync(ct) is not null)
                    throw new ArgumentException("ScannerCode already exists.");
            }

            const string sql = @"UPDATE dbo.Scanners
SET ScannerCode = @scannerCode,
    ScannerName = @scannerName,
    Description = @description,
    IsActive = @isActive,
    UpdatedAt = SYSDATETIME()
WHERE ScannerId = @id;";

            await using var command = new SqlCommand(sql, connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", scannerId);
            command.Parameters.AddWithValue("@scannerCode", normalized.ScannerCode!.Trim());
            command.Parameters.AddWithValue("@scannerName", normalized.ScannerName!.Trim());
            command.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(normalized.Description) ? (object)DBNull.Value : normalized.Description.Trim());
            command.Parameters.AddWithValue("@isActive", normalized.IsActive);
            await command.ExecuteNonQueryAsync(ct);

            await UpsertPermissionsInTransactionAsync(connection, (SqlTransaction)transaction, scannerId, normalized.Permissions ?? Array.Empty<ScannerPermissionWriteDto>(), ct);
            await ApplyEmployeeScannerLinkAsync(connection, (SqlTransaction)transaction, normalized.ScannerCode!, normalized.EmployeeId, ct);
            await transaction.CommitAsync(ct);
            return await GetByIdAsync(scannerId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ScannerDto?> ActivateAsync(int scannerId, CancellationToken ct)
    {
        if (await GetByIdAsync(scannerId, ct) is null) return null;
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "UPDATE dbo.Scanners SET IsActive = 1, UpdatedAt = SYSDATETIME() WHERE ScannerId = @id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", scannerId);
        await command.ExecuteNonQueryAsync(ct);
        return await GetByIdAsync(scannerId, ct);
    }

    public async Task<ScannerDto?> DeactivateAsync(int scannerId, CancellationToken ct)
    {
        if (await GetByIdAsync(scannerId, ct) is null) return null;
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        const string sql = "UPDATE dbo.Scanners SET IsActive = 0, UpdatedAt = SYSDATETIME() WHERE ScannerId = @id";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", scannerId);
        await command.ExecuteNonQueryAsync(ct);
        return await GetByIdAsync(scannerId, ct);
    }

    public async Task<IReadOnlyList<ScannerPermissionDto>> UpsertPermissionsAsync(int scannerId, IReadOnlyList<ScannerPermissionWriteDto>? permissions, CancellationToken ct)
    {
        if (scannerId <= 0)
        {
            return Array.Empty<ScannerPermissionDto>();
        }

        var normalized = ScannerWriteValidator.NormalizePermissions(permissions);
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await UpsertPermissionsInTransactionAsync(connection, (SqlTransaction)transaction, scannerId, normalized, ct);
            await transaction.CommitAsync(ct);
            return await GetPermissionsAsync(scannerId, ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct) => QueryAsync("SELECT ID, TrackingCode, ScanTime FROM dbo.Live_Scan ORDER BY ScanTime DESC, ID DESC", reader => new LiveScanDto(reader.GetInt32(0), reader.NullableInt32("TrackingCode"), reader.NullableDateTime("ScanTime")), ct);

    private static async Task ApplyEmployeeScannerLinkAsync(SqlConnection connection, SqlTransaction transaction, string scannerCode, int? employeeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scannerCode))
        {
            return;
        }

        var normalizedScannerCode = scannerCode.Trim();
        if (employeeId is null || employeeId <= 0)
        {
            const string unlinkSql = @"UPDATE dbo.Employees WITH (UPDLOCK, HOLDLOCK)
SET ScannerCode = NULL,
    UpdatedAt = SYSDATETIME()
WHERE ScannerCode = @scannerCode;";

            await using var unlinkCommand = new SqlCommand(unlinkSql, connection, transaction);
            unlinkCommand.Parameters.AddWithValue("@scannerCode", normalizedScannerCode);
            await unlinkCommand.ExecuteNonQueryAsync(ct);
            return;
        }

        const string employeeSql = @"SELECT EmployeeID, EmployeeCode, EmployeeName, ScannerCode, IsActive
FROM dbo.Employees WITH (UPDLOCK, HOLDLOCK)
WHERE EmployeeID = @employeeId;";

        await using (var employeeCommand = new SqlCommand(employeeSql, connection, transaction))
        {
            employeeCommand.Parameters.AddWithValue("@employeeId", employeeId.Value);
            await using var employeeReader = await employeeCommand.ExecuteReaderAsync(ct);
            if (!await employeeReader.ReadAsync(ct))
            {
                throw new ArgumentException("EmployeeId was not found.");
            }

            var isActive = employeeReader.GetBoolean(4);
            if (!isActive)
            {
                throw new ArgumentException("Employee is inactive and cannot be linked to a scanner.");
            }
        }

        const string detachSql = @"UPDATE dbo.Employees WITH (UPDLOCK, HOLDLOCK)
SET ScannerCode = NULL,
    UpdatedAt = SYSDATETIME()
WHERE ScannerCode = @scannerCode AND EmployeeID <> @employeeId;";

        await using (var detachCommand = new SqlCommand(detachSql, connection, transaction))
        {
            detachCommand.Parameters.AddWithValue("@scannerCode", normalizedScannerCode);
            detachCommand.Parameters.AddWithValue("@employeeId", employeeId.Value);
            await detachCommand.ExecuteNonQueryAsync(ct);
        }

        const string assignSql = @"UPDATE dbo.Employees WITH (UPDLOCK, HOLDLOCK)
SET ScannerCode = @scannerCode,
    UpdatedAt = SYSDATETIME()
WHERE EmployeeID = @employeeId;";

        await using (var assignCommand = new SqlCommand(assignSql, connection, transaction))
        {
            assignCommand.Parameters.AddWithValue("@scannerCode", normalizedScannerCode);
            assignCommand.Parameters.AddWithValue("@employeeId", employeeId.Value);
            await assignCommand.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task UpsertPermissionsInTransactionAsync(SqlConnection connection, SqlTransaction transaction, int scannerId, IReadOnlyList<ScannerPermissionWriteDto> permissions, CancellationToken ct)
    {
        const string validateSql = "SELECT TOP (1) ScannerId FROM dbo.Scanners WITH (UPDLOCK, HOLDLOCK) WHERE ScannerId = @scannerId;";
        await using (var validateCommand = new SqlCommand(validateSql, connection, transaction))
        {
            validateCommand.Parameters.AddWithValue("@scannerId", scannerId);
            if (await validateCommand.ExecuteScalarAsync(ct) is null)
            {
                throw new ArgumentException("Scanner was not found.");
            }
        }

        const string deleteSql = "DELETE FROM dbo.ScannerStageProductPermissions WHERE ScannerId = @scannerId;";
        await using (var deleteCommand = new SqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("@scannerId", scannerId);
            await deleteCommand.ExecuteNonQueryAsync(ct);
        }

        if (permissions.Count == 0)
        {
            return;
        }

        const string insertSql = @"INSERT INTO dbo.ScannerStageProductPermissions (ScannerId, ProductTypeId, Stage, IsActive, CreatedAt, UpdatedAt)
VALUES (@scannerId, @productTypeId, @stage, @isActive, SYSDATETIME(), SYSDATETIME());";

        foreach (var permission in permissions)
        {
            await using var insertCommand = new SqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("@scannerId", scannerId);
            insertCommand.Parameters.AddWithValue("@productTypeId", permission.ProductTypeId);
            insertCommand.Parameters.AddWithValue("@stage", permission.Stage.Trim());
            insertCommand.Parameters.AddWithValue("@isActive", permission.IsActive);
            await insertCommand.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct)) items.Add(map(reader));
        return items;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameter, string parameterName, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        if (parameter is not null) command.Parameters.AddWithValue(parameterName, parameter);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<T>();
        while (await reader.ReadAsync(ct)) items.Add(map(reader));
        return items;
    }
}