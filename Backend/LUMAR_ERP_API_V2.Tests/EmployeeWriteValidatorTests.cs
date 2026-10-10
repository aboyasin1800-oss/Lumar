using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class EmployeeWriteValidatorTests
{
    [Fact]
    public void ValidateForCreate_AllowsPieceWageWithZeroBasicSalaryAndValidStage()
    {
        var request = new CreateEmployeeDto
        {
            FullName = "مستخدم قطعة",
            DepartmentId = 1,
            BasicSalary = 0m,
            HireDate = DateTime.UtcNow,
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("بنطلون", "Cutting", 15.5m, null, null, true)
            ]
        };

        var validated = EmployeeWriteValidator.ValidateForCreate(request);

        Assert.Equal(0m, validated.BasicSalary);
        Assert.Equal("PieceWage", validated.SalaryType);
        Assert.Single(validated.PieceRates!);
        Assert.Equal("Cutting", validated.PieceRates[0].Stage);
    }

    [Fact]
    public void ValidateForCreate_RejectsPieceWageWithNonZeroBasicSalary()
    {
        var request = new CreateEmployeeDto
        {
            FullName = "مستخدم قطعة",
            DepartmentId = 1,
            BasicSalary = 0.01m,
            HireDate = DateTime.UtcNow,
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("بنطلون", "Cutting", 15.5m, null, null, true)
            ]
        };

        var exception = Assert.Throws<ArgumentException>(() => EmployeeWriteValidator.ValidateForCreate(request));
        Assert.Contains("BasicSalary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForCreate_RejectsPieceRateWithoutStage()
    {
        var request = new CreateEmployeeDto
        {
            FullName = "مستخدم قطعة",
            DepartmentId = 1,
            BasicSalary = 0m,
            HireDate = DateTime.UtcNow,
            Status = "Active",
            SalaryType = "PieceWage",
            PieceRates =
            [
                new EmployeePieceRateAssignmentInputDto("بنطلون", string.Empty, 15.5m, null, null, true)
            ]
        };

        var exception = Assert.Throws<ArgumentException>(() => EmployeeWriteValidator.ValidateForCreate(request));
        Assert.Contains("Stage", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
