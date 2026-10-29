using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Monitoring;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeWriteInfrastructureTests
{
    [Fact]
    public void CreateEmployee_Valid_Request_Passes_Validation()
    {
        var request = new CreateEmployeeDto
        {
            EmployeeCode = "EMP-001",
            FullName = "علي أحمد",
            DepartmentId = 17,
            Status = "Active",
            BasicSalary = 2500m,
            HireDate = new DateTime(2025, 1, 15)
        };

        var exception = Record.Exception(() => EmployeeWriteValidator.ValidateForCreate(request));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateAsync_BasicSalaryEmployee_KeepsLegacyPieceWageRateZero()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-EMP");

        try
        {
            var repository = CreateRepository(connectionString);
            var created = await repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف راتب أساسي اختبار",
                DepartmentId = 17,
                BasicSalary = 60000m,
                HireDate = new DateTime(2025, 1, 15),
                Status = "Active",
                SalaryType = "BasicSalary"
            }, CancellationToken.None);

            Assert.NotNull(created);
            Assert.Equal(60000m, created!.BasicSalary);
            Assert.Equal(0m, created.PieceWageRate);
            Assert.Equal(60000m, await ReadDecimalAsync(connectionString, "SELECT BasicSalary FROM dbo.Employees WHERE EmployeeCode = @code", code));
            Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT PieceWageRate FROM dbo.Employees WHERE EmployeeCode = @code", code));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task CreateAsync_PieceWageEmployee_PersistsZeroLegacyRateAndIndependentAssignments()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-PIECE");

        try
        {
            var repository = CreateRepository(connectionString);
            var created = await repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف قطعة اختبار",
                DepartmentId = 17,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 2, 10),
                Status = "Active",
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto("بنطلون", "Cutting", 22.50m, null, null, true)
                ]
            }, CancellationToken.None);

            Assert.NotNull(created);
            Assert.Equal(0m, created!.BasicSalary);
            Assert.Equal(0m, created.PieceWageRate);
            Assert.Equal(0m, await ReadDecimalAsync(connectionString, "SELECT PieceWageRate FROM dbo.Employees WHERE EmployeeCode = @code", code));
            Assert.Equal(1, await CountAssignmentsAsync(connectionString, created.EmployeeId));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task CreateAsync_PieceWageWithInvalidPieceRate_RollsBackEntireEmployee()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-ROLL");

        try
        {
            var repository = CreateRepository(connectionString);
            await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(new CreateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف قطعة فاشل",
                DepartmentId = 17,
                BasicSalary = 0m,
                HireDate = new DateTime(2025, 3, 14),
                Status = "Active",
                SalaryType = "PieceWage",
                PieceRates =
                [
                    new EmployeePieceRateAssignmentInputDto(string.Empty, "Cutting", 22.50m, null, null, true)
                ]
            }, CancellationToken.None));

            Assert.Equal(0, await CountEmployeesAsync(connectionString, code));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public void CreateEmployee_Rejects_Missing_Data()
    {
        var request = new CreateEmployeeDto
        {
            EmployeeCode = "   ",
            FullName = "",
            Status = "",
            BasicSalary = 0m
        };

        var exception = Assert.Throws<ArgumentException>(() => EmployeeWriteValidator.ValidateForCreate(request));

        Assert.Contains("FullName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_Code_Validation_Rejects_Existing_Code()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EmployeeWriteValidator.EnsureUniqueCode("EMP-001", new[] { "EMP-001" }, null));

        Assert.Contains("EmployeeCode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateEmployee_Valid_Request_Passes_Validation()
    {
        var request = new UpdateEmployeeDto
        {
            EmployeeCode = "EMP-001",
            FullName = "علي أحمد",
            DepartmentId = 17,
            Status = "Active",
            BasicSalary = 2800m,
            HireDate = new DateTime(2025, 1, 15),
            IsActive = true
        };

        var exception = Record.Exception(() => EmployeeWriteValidator.ValidateForUpdate(request));

        Assert.Null(exception);
    }

    [Fact]
    public void ActivateEmployee_Normalizes_Status_To_Active()
    {
        Assert.Equal("Active", EmployeeWriteValidator.NormalizeStatus("active"));
    }

    [Fact]
    public void DeactivateEmployee_Normalizes_Status_To_Inactive()
    {
        Assert.Equal("Inactive", EmployeeWriteValidator.NormalizeStatus("inactive"));
    }

    [Fact]
    public async Task GetWorkflowAsync_UsesOfficialPieceRateAssignments_AndIgnoresLegacyWorkflowTable()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-WORK");

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف سير عمل اختبار",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 6, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var workflow = await repository.GetWorkflowAsync(code, CancellationToken.None);

            Assert.NotEmpty(workflow);
            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) && string.Equals(item.WorkStage, "Cutting", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task GetWorkflowAsync_IncludesLinkedScannerPermissionStages_ForGeneralStageExecution()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-GEN");
        var scannerCode = $"SCN-{Guid.NewGuid():N}"[..30];

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف تجهيز عام اختبار",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 6, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            await using (var scannerCommand = new SqlCommand(@"INSERT INTO dbo.Scanners (ScannerCode, ScannerName, Description, IsActive, CreatedAt, UpdatedAt)
VALUES (@scannerCode, @scannerName, @description, 1, SYSDATETIME(), SYSDATETIME());", connection))
            {
                scannerCommand.Parameters.AddWithValue("@scannerCode", scannerCode);
                scannerCommand.Parameters.AddWithValue("@scannerName", $"Scanner {scannerCode}");
                scannerCommand.Parameters.AddWithValue("@description", "General stage workflow regression");
                await scannerCommand.ExecuteNonQueryAsync();
            }

            var scannerId = await ReadIntAsync(connectionString, "SELECT ScannerId FROM dbo.Scanners WHERE ScannerCode = @code", scannerCode);
            await using (var permissionCommand = new SqlCommand(@"INSERT INTO dbo.ScannerStageProductPermissions (ScannerId, ProductTypeId, Stage, IsActive, CreatedAt, UpdatedAt)
VALUES (@scannerId, 1, 'Assembly', 1, SYSDATETIME(), SYSDATETIME());", connection))
            {
                permissionCommand.Parameters.AddWithValue("@scannerId", scannerId);
                await permissionCommand.ExecuteNonQueryAsync();
            }

            await using (var linkCommand = new SqlCommand(@"UPDATE dbo.Employees SET ScannerCode = @scannerCode, UpdatedAt = SYSDATETIME() WHERE EmployeeId = @employeeId;", connection))
            {
                linkCommand.Parameters.AddWithValue("@scannerCode", scannerCode);
                linkCommand.Parameters.AddWithValue("@employeeId", created!.EmployeeId);
                await linkCommand.ExecuteNonQueryAsync();
            }
        }

        try
        {
            var workflow = await repository.GetWorkflowAsync(code, CancellationToken.None);

            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) &&
                string.Equals(item.WorkStage, "Assembly", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(workflow, item => string.Equals(item.EmployeeCode, code, StringComparison.Ordinal) &&
                string.Equals(item.WorkStage, "Cutting", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using (var cleanup = new SqlCommand(@"DELETE FROM dbo.ScannerStageProductPermissions WHERE ScannerId IN (SELECT ScannerId FROM dbo.Scanners WHERE ScannerCode = @scannerCode);
DELETE FROM dbo.Scanners WHERE ScannerCode = @scannerCode;
UPDATE dbo.Employees SET ScannerCode = NULL, UpdatedAt = SYSDATETIME() WHERE EmployeeCode = @employeeCode;", connection))
                {
                    cleanup.Parameters.AddWithValue("@scannerCode", scannerCode);
                    cleanup.Parameters.AddWithValue("@employeeCode", code);
                    await cleanup.ExecuteNonQueryAsync();
                }
            }

            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task UpdateAsync_BasicSalaryEmployee_ClearsStalePieceAssignments()
    {
        var connectionString = GetConnectionString();
        var code = CreateUniqueEmployeeCode("ES7-STALE");

        var repository = CreateRepository(connectionString);
        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = code,
            FullName = "موظف تحديث مرتبطة بالقطعة",
            DepartmentId = 17,
            BasicSalary = 0m,
            HireDate = new DateTime(2025, 7, 1),
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("كوت", "Cutting", 42.50m, null, null, true),
                new EmployeePieceRateAssignmentInputDto("بنطلون", "Sewing", 35.00m, null, null, true)
            ]
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var updated = await repository.UpdateAsync(created!.EmployeeId, new UpdateEmployeeDto
            {
                EmployeeCode = code,
                FullName = "موظف تحديث راتب أساسي",
                DepartmentId = 17,
                BasicSalary = 4200m,
                HireDate = new DateTime(2025, 7, 1),
                Status = "Active",
                IsActive = true,
                SalaryType = "BasicSalary",
                PieceRates = null
            }, CancellationToken.None);

            Assert.NotNull(updated);
            Assert.Equal(4200m, updated!.BasicSalary);
            Assert.Equal("BasicSalary", updated.SalaryType);
            Assert.Equal(0, await CountAssignmentsAsync(connectionString, created.EmployeeId));
            Assert.Empty(await repository.GetWorkflowAsync(code, CancellationToken.None));
        }
        finally
        {
            await DeleteEmployeeByCodeAsync(connectionString, code);
        }
    }

    [Fact]
    public async Task SafeRequestCaptureMiddleware_WhenLogFileIsLocked_DoesNotThrow() 
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LUMAR_ERP_API_V2",
            "safe-live-monitor.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(logPath, string.Empty);

        await using var lockedHandle = new FileStream(logPath, FileMode.Open, FileAccess.Write, FileShare.None);
        var middleware = new SafeRequestCaptureMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/production/scan";
        context.Response.StatusCode = StatusCodes.Status200OK;

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAllAsync_And_GetDepartmentsAsync_DoNotThrow_WhenParametersAreNull()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);

        var employees = await repository.GetAllAsync(CancellationToken.None);
        var departments = await repository.GetDepartmentsAsync(CancellationToken.None);

        Assert.NotNull(employees);
        Assert.NotNull(departments);
        Assert.NotEmpty(employees);
        Assert.NotEmpty(departments);
    }

    [Fact]
    public async Task GetSelfFinancialSummaryAsync_WithPeriodId_ReturnsSummary_WithoutArgumentException()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);
        var employeeCode = CreateUniqueEmployeeCode("ES7-FIN");

        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف مالي اختبار",
            DepartmentId = 17,
            BasicSalary = 4200m,
            HireDate = new DateTime(2025, 4, 1),
            Status = "Active",
            SalaryType = "BasicSalary"
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var rowValue = await EnsurePayrollRecordExistsAsync(connectionString, created!.EmployeeId);

            var exception = await Record.ExceptionAsync(() => repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, rowValue.PayrollPeriodId, CancellationToken.None));

            Assert.Null(exception);
            var summary = await repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, rowValue.PayrollPeriodId, CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(rowValue.EmployeeId, summary!.EmployeeId);
            Assert.Equal(rowValue.PayrollPeriodId, summary.PayrollPeriodId);
        }
        finally
        {
            await DeletePayrollRecordAsync(connectionString, created!.EmployeeId);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
        }
    }

    [Fact]
    public async Task GetSelfFinancialSummaryAsync_WithoutPeriodId_UsesAuthenticatedEmployeeId_WithoutArgumentException()
    {
        var connectionString = GetConnectionString();
        var repository = CreateRepository(connectionString);
        var employeeCode = CreateUniqueEmployeeCode("ES7-FIN");

        var created = await repository.CreateAsync(new CreateEmployeeDto
        {
            EmployeeCode = employeeCode,
            FullName = "موظف مالي اختبار بدون فترة",
            DepartmentId = 17,
            BasicSalary = 5000m,
            HireDate = new DateTime(2025, 4, 2),
            Status = "Active",
            SalaryType = "BasicSalary"
        }, CancellationToken.None);

        Assert.NotNull(created);

        try
        {
            var rowValue = await EnsurePayrollRecordExistsAsync(connectionString, created!.EmployeeId);

            var exception = await Record.ExceptionAsync(() => repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, null, CancellationToken.None));

            Assert.Null(exception);
            var summary = await repository.GetSelfFinancialSummaryAsync(rowValue.EmployeeId, null, CancellationToken.None);
            Assert.NotNull(summary);
            Assert.Equal(rowValue.EmployeeId, summary!.EmployeeId);
        }
        finally
        {
            await DeletePayrollRecordAsync(connectionString, created!.EmployeeId);
            await DeleteEmployeeByCodeAsync(connectionString, employeeCode);
        }
    }

    private static EmployeeRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new EmployeeRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");

        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Employee integration tests require LUMAR_ERP_ES_VALIDATION.");

        return builder.ConnectionString;
    }

    private static string CreateUniqueEmployeeCode(string prefix, int maxLength = 20)
    {
        var candidate = $"{prefix}-{Guid.NewGuid():N}";
        return candidate.Length <= maxLength ? candidate : candidate[..maxLength];
    }

    private static async Task<decimal> ReadDecimalAsync(string connectionString, string sql, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? 0m : Convert.ToDecimal(value);
    }

    private static async Task<int> ReadIntAsync(string connectionString, string sql, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", code);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? 0 : Convert.ToInt32(value);
    }

    private static async Task<int> CountAssignmentsAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId", connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<(int EmployeeId, int PayrollPeriodId)> EnsurePayrollRecordExistsAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var existing = new SqlCommand(@"SELECT TOP (1) EmployeeId, PayrollPeriodId FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId ORDER BY PayrollPeriodId DESC, CreatedAt DESC", connection))
        {
            existing.Parameters.AddWithValue("@employeeId", employeeId);
            await using var reader = await existing.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetInt32(1));
            }
        }

        await using (var period = new SqlCommand(@"INSERT INTO dbo.PayrollPeriods (PeriodCode, StartDate, EndDate, Status, Notes, GeneratedAt, CreatedAt) OUTPUT INSERTED.PayrollPeriodId VALUES (@periodCode, @startDate, @endDate, @status, @notes, @generatedAt, @createdAt)", connection))
        {
            var startDate = DateTime.UtcNow.AddMonths(-1).Date;
            var endDate = startDate.AddMonths(1).AddDays(-1);
            period.Parameters.AddWithValue("@periodCode", $"TEST-{Guid.NewGuid():N}");
            period.Parameters.AddWithValue("@startDate", startDate);
            period.Parameters.AddWithValue("@endDate", endDate);
            period.Parameters.AddWithValue("@status", "Generated");
            period.Parameters.AddWithValue("@notes", (object?)DBNull.Value);
            period.Parameters.AddWithValue("@generatedAt", DateTime.UtcNow);
            period.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            var periodId = await period.ExecuteScalarAsync();
            if (periodId is not int payrollPeriodId)
                throw new InvalidOperationException("Payroll period could not be created for the financial summary test.");

            await using (var record = new SqlCommand(@"INSERT INTO dbo.PayrollRecords (PayrollPeriodId, EmployeeId, BasicSalaryAmount, PieceWageAmount, AttendanceAdjustmentAmount, OvertimeAmount, GrossAmount, DeductionsAmount, NetAmount, Status, Notes, CreatedAt) OUTPUT INSERTED.PayrollPeriodId VALUES (@periodId, @employeeId, @basicSalaryAmount, @pieceWageAmount, @attendanceAdjustmentAmount, @overtimeAmount, @grossAmount, @deductionsAmount, @netAmount, @status, @notes, @createdAt)", connection))
            {
                record.Parameters.AddWithValue("@periodId", payrollPeriodId);
                record.Parameters.AddWithValue("@employeeId", employeeId);
                record.Parameters.AddWithValue("@basicSalaryAmount", 4200m);
                record.Parameters.AddWithValue("@pieceWageAmount", 0m);
                record.Parameters.AddWithValue("@attendanceAdjustmentAmount", 0m);
                record.Parameters.AddWithValue("@overtimeAmount", 0m);
                record.Parameters.AddWithValue("@grossAmount", 4200m);
                record.Parameters.AddWithValue("@deductionsAmount", 0m);
                record.Parameters.AddWithValue("@netAmount", 4200m);
                record.Parameters.AddWithValue("@status", "Approved");
                record.Parameters.AddWithValue("@notes", (object?)DBNull.Value);
                record.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

                var insertedPeriodId = await record.ExecuteScalarAsync();
                if (insertedPeriodId is not int insertedPayrollPeriodId)
                    throw new InvalidOperationException("Payroll record could not be created for the financial summary test.");

                return (employeeId, insertedPayrollPeriodId);
            }
        }
    }

    private static async Task DeletePayrollRecordAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var selectPeriodIds = new SqlCommand("SELECT DISTINCT PayrollPeriodId FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection))
        {
            selectPeriodIds.Parameters.AddWithValue("@employeeId", employeeId);
            await using var reader = await selectPeriodIds.ExecuteReaderAsync();
            var periodIds = new List<int>();
            while (await reader.ReadAsync())
            {
                periodIds.Add(reader.GetInt32(0));
            }

            if (periodIds.Count > 0)
            {
                await using (var deleteRecords = new SqlCommand("DELETE FROM dbo.PayrollRecords WHERE EmployeeId = @employeeId", connection))
                {
                    deleteRecords.Parameters.AddWithValue("@employeeId", employeeId);
                    await deleteRecords.ExecuteNonQueryAsync();
                }

                foreach (var periodId in periodIds)
                {
                    await using var deletePeriod = new SqlCommand("DELETE FROM dbo.PayrollPeriods WHERE PayrollPeriodId = @periodId", connection);
                    deletePeriod.Parameters.AddWithValue("@periodId", periodId);
                    await deletePeriod.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private static async Task<int> CountEmployeesAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Employees WHERE EmployeeCode = @code", connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task DeleteEmployeeByCodeAsync(string connectionString, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var find = new SqlCommand("SELECT EmployeeId FROM dbo.Employees WHERE EmployeeCode = @code", connection))
        {
            find.Parameters.AddWithValue("@code", code);
            var employeeId = await find.ExecuteScalarAsync();
            if (employeeId is int id)
            {
                await using (var deleteContracts = new SqlCommand("DELETE FROM dbo.EmployeeContracts WHERE EmployeeId = @employeeId", connection))
                {
                    deleteContracts.Parameters.AddWithValue("@employeeId", id);
                    await deleteContracts.ExecuteNonQueryAsync();
                }

                await using (var deleteAssignments = new SqlCommand("DELETE FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId", connection))
                {
                    deleteAssignments.Parameters.AddWithValue("@employeeId", id);
                    await deleteAssignments.ExecuteNonQueryAsync();
                }

                await using (var deleteEmployee = new SqlCommand("DELETE FROM dbo.Employees WHERE EmployeeId = @employeeId", connection))
                {
                    deleteEmployee.Parameters.AddWithValue("@employeeId", id);
                    await deleteEmployee.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
