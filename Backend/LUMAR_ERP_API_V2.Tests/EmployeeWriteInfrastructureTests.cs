using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Services;
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
            DepartmentId = 1,
            Status = "Active",
            BasicSalary = 2500m,
            HireDate = new DateTime(2025, 1, 15)
        };

        var exception = Record.Exception(() => EmployeeWriteValidator.ValidateForCreate(request));

        Assert.Null(exception);
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

        Assert.Contains("EmployeeCode", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            DepartmentId = 1,
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
}
