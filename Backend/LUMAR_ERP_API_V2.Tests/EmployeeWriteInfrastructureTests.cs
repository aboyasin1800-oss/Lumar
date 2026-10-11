using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
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

    private static async Task<int> CountAssignmentsAsync(string connectionString, int employeeId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.EmployeePieceRateAssignments WHERE EmployeeId = @employeeId", connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
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
