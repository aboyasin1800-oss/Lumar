using System.Data;
using LUMAR_ERP_API_V2.FinancialFoundation;
using Microsoft.Data.SqlClient;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class EmployeeSupplierLedgerFoundationIntegrationTests
{
    [Fact]
    public async Task Foundation_WritesDerivedBalancesPreventsDuplicatesAndAllowsOneReversal()
    {
        var connectionString = GetConnectionString();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var employeeId = await SeedEmployeeAsync(connection, suffix);
        var supplierId = await SeedSupplierAsync(connection, suffix);
        var employeeWriter = new EmployeeLedgerWriter();
        var supplierWriter = new SupplierLedgerWriter();

        try
        {
            await using (var typeCommand = new SqlCommand("SELECT SalaryType FROM dbo.Employees WHERE EmployeeID=@employeeId", connection))
            {
                typeCommand.Parameters.AddWithValue("@employeeId", employeeId);
                Assert.Equal("BasicSalary", Convert.ToString(await typeCommand.ExecuteScalarAsync()));
            }

            var salaryOperation = Guid.NewGuid();
            long salaryEntryId;
            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                var salary = await employeeWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(
                    "SalaryAccrual", 100m, 100m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "PayrollAccrual", 101, salaryOperation, $"EMP-SAL-{suffix}", "foundation-test"), CancellationToken.None);
                salaryEntryId = salary.EntryId;
                var retry = await employeeWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(
                    "SalaryAccrual", 100m, 100m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "PayrollAccrual", 101, salaryOperation, $"EMP-SAL-{suffix}", "foundation-test"), CancellationToken.None);
                Assert.True(retry.IsExisting);
                Assert.Equal(salary.EntryId, retry.EntryId);
                await transaction.CommitAsync();
            }

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await employeeWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(
                    "Reversal", 100m, -100m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "PayrollAccrual", 101, Guid.NewGuid(), $"EMP-REV-{suffix}", "foundation-test", salaryEntryId, "test reversal", "foundation-test", DateTime.UtcNow), CancellationToken.None);
                await transaction.CommitAsync();
            }

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => employeeWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(
                    "Reversal", 100m, -100m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "PayrollAccrual", 101, Guid.NewGuid(), $"EMP-REV-2-{suffix}", "foundation-test", salaryEntryId, "duplicate reversal", "foundation-test", DateTime.UtcNow), CancellationToken.None));
                await transaction.RollbackAsync();
            }

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await supplierWriter.PostAsync(connection, transaction, supplierId, new LedgerEntryRequest(
                    "SupplierInvoice", 250m, 250m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "SupplierInvoice", 201, Guid.NewGuid(), $"SUP-INV-{suffix}", "foundation-test"), CancellationToken.None);
                await supplierWriter.PostAsync(connection, transaction, supplierId, new LedgerEntryRequest(
                    "SupplierPayment", 75m, -75m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "SupplierPayment", 202, Guid.NewGuid(), $"SUP-PAY-{suffix}", "foundation-test"), CancellationToken.None);
                await transaction.CommitAsync();
            }

            var employeeBalance = await new EmployeeLedgerReader().GetBalanceAsync(connection, employeeId, CancellationToken.None);
            var supplierBalance = await new SupplierLedgerReader().GetBalanceAsync(connection, supplierId, CancellationToken.None);
            Assert.Equal(0m, employeeBalance.NetBalance);
            Assert.Equal(175m, supplierBalance.CurrentBalance);
            Assert.Equal(250m, supplierBalance.TotalInvoices);
            Assert.Equal(75m, supplierBalance.TotalPayments);

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await employeeWriter.PostAsync(connection, transaction, employeeId, new LedgerEntryRequest(
                    "Advance", 50m, -50m, DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), "EmployeeDraw", 301, Guid.NewGuid(), $"EMP-RB-{suffix}", "foundation-test"), CancellationToken.None);
                await transaction.RollbackAsync();
            }
            Assert.Equal(0m, (await new EmployeeLedgerReader().GetBalanceAsync(connection, employeeId, CancellationToken.None)).NetBalance);
        }
        finally
        {
            await CleanupAsync(connection, employeeId, supplierId, suffix);
        }
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phase ES-1 tests require an approved ES validation database.");
        return builder.ConnectionString;
    }

    private static async Task<int> SeedEmployeeAsync(SqlConnection connection, string suffix)
    {
        const string sql = """
            DECLARE @departmentId int = (SELECT TOP (1) DepartmentId FROM dbo.Departments ORDER BY DepartmentId);
            IF @departmentId IS NULL
            BEGIN
                INSERT INTO dbo.Departments (DepartmentCode, DepartmentName, IsActive, CreatedAt)
                VALUES (@departmentCode, N'قسم اختبار الدفتر', 1, SYSUTCDATETIME());
                SET @departmentId = SCOPE_IDENTITY();
            END;
            INSERT INTO dbo.Employees (EmployeeCode, EmployeeName, FullName, DepartmentId, BasicSalary, SalaryType, HireDate, Status, IsActive, CreatedAt, PieceWageRate, OvertimeHourlyRate)
            OUTPUT INSERTED.EmployeeID
            VALUES (@employeeCode, N'موظف اختبار الدفتر', N'موظف اختبار الدفتر', @departmentId, 100, N'BasicSalary', CAST(SYSUTCDATETIME() AS date), N'Active', 1, SYSUTCDATETIME(), 0, 25);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@departmentCode", $"ES1-{suffix}");
        command.Parameters.AddWithValue("@employeeCode", $"ES1E-{suffix}");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> SeedSupplierAsync(SqlConnection connection, string suffix)
    {
        const string sql = "INSERT INTO dbo.Suppliers (SupplierCode, SupplierName, IsActive, CreatedAt) OUTPUT INSERTED.SupplierId VALUES (@code, N'مورد اختبار الدفتر', 1, SYSUTCDATETIME());";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", $"ES1S-{suffix}");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task CleanupAsync(SqlConnection connection, int employeeId, int supplierId, string suffix)
    {
        await using var command = new SqlCommand("""
            DELETE FROM dbo.EmployeeLedgerEntries WHERE EmployeeId=@employeeId;
            DELETE FROM dbo.SupplierLedgerEntries WHERE SupplierId=@supplierId AND Status <> N'Legacy';
            DELETE FROM dbo.Employees WHERE EmployeeID=@employeeId;
            DELETE FROM dbo.Suppliers WHERE SupplierId=@supplierId;
            DELETE FROM dbo.Departments WHERE DepartmentCode=@departmentCode;
            """, connection);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        command.Parameters.AddWithValue("@departmentCode", $"ES1-{suffix}");
        await command.ExecuteNonQueryAsync();
    }
}