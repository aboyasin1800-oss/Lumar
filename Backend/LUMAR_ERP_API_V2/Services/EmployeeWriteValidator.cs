namespace LUMAR_ERP_API_V2.Services;

using LUMAR_ERP_API_V2.DTOs.Employees;

public static class EmployeeWriteValidator
{
    public static CreateEmployeeDto ValidateForCreate(CreateEmployeeDto request)
    {
        if (request is null) throw new ArgumentException("Employee payload is required.");

        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ArgumentException("FullName is required.");
        if (request.DepartmentId <= 0) throw new ArgumentException("DepartmentId is required.");
        if (request.HireDate is null || request.HireDate == default) throw new ArgumentException("HireDate is required.");
        if (string.IsNullOrWhiteSpace(request.Status)) throw new ArgumentException("Status is required.");
        var salaryType = NormalizeSalaryType(request.SalaryType);
        ValidateSalary(request.BasicSalary, salaryType);

        return request with
        {
            EmployeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status = NormalizeStatus(request.Status),
            SalaryType = salaryType
        };
    }

    public static UpdateEmployeeDto ValidateForUpdate(UpdateEmployeeDto request)
    {
        if (request is null) throw new ArgumentException("Employee payload is required.");

        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ArgumentException("FullName is required.");
        if (request.DepartmentId <= 0) throw new ArgumentException("DepartmentId is required.");
        if (request.HireDate is null || request.HireDate == default) throw new ArgumentException("HireDate is required.");
        if (string.IsNullOrWhiteSpace(request.Status)) throw new ArgumentException("Status is required.");
        var salaryType = NormalizeSalaryType(request.SalaryType);
        ValidateSalary(request.BasicSalary, salaryType);

        return request with
        {
            EmployeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status = NormalizeStatus(request.Status),
            SalaryType = salaryType
        };
    }

    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Active";

        return status.Trim() switch
        {
            "active" or "Active" or "ACTIVE" => "Active",
            "inactive" or "Inactive" or "INACTIVE" => "Inactive",
            "suspended" or "Suspended" => "Suspended",
            "terminated" or "Terminated" => "Terminated",
            "onleave" or "OnLeave" or "on_leave" or "On_Leave" => "OnLeave",
            _ => status.Trim(),
        };
    }

    private static string NormalizeSalaryType(string? salaryType) => salaryType?.Trim() switch
    {
        "BasicSalary" => "BasicSalary",
        "PieceWage" => "PieceWage",
        _ => throw new ArgumentException("SalaryType must be BasicSalary or PieceWage.")
    };

    private static void ValidateSalary(decimal basicSalary, string salaryType)
    {
        if (salaryType == "BasicSalary" && basicSalary <= 0m)
            throw new ArgumentException("BasicSalary must be greater than zero for BasicSalary employees.");
        if (salaryType == "PieceWage" && basicSalary != 0m)
            throw new ArgumentException("BasicSalary must be zero for PieceWage employees.");
    }

    public static void EnsureUniqueCode(string? employeeCode, IEnumerable<string>? existingCodes, string? currentEmployeeCode = null)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) throw new ArgumentException("EmployeeCode is required.");

        var normalized = employeeCode.Trim();
        var codes = existingCodes ?? [];
        var duplicate = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Any(code => string.Equals(code, normalized, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(code, currentEmployeeCode?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicate) throw new ArgumentException("EmployeeCode already exists.");
    }
}
