namespace LUMAR_ERP_API_V2.Services;

using LUMAR_ERP_API_V2.DTOs.Employees;
using LUMAR_ERP_API_V2.DTOs.Payroll;
using PayrollPieceWageRateDto = LUMAR_ERP_API_V2.DTOs.Payroll.PieceWageRateDto;

public static class EmployeeWriteValidator
{
    public static CreateEmployeeDto ValidateForCreate(CreateEmployeeDto request, IReadOnlyList<PayrollPieceWageRateDto>? officialRates = null)
    {
        if (request is null) throw new ArgumentException("Employee payload is required.");

        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ArgumentException("FullName is required.");
        if (request.DepartmentId <= 0) throw new ArgumentException("DepartmentId is required.");
        if (request.HireDate is null || request.HireDate == default) throw new ArgumentException("HireDate is required.");
        if (string.IsNullOrWhiteSpace(request.Status)) throw new ArgumentException("Status is required.");
        var salaryType = NormalizeSalaryType(request.SalaryType);
        ValidateSalary(request.BasicSalary, salaryType);
        ValidatePieceRates(request.PieceRates, salaryType, officialRates);

        return request with
        {
            EmployeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status = NormalizeStatus(request.Status),
            SalaryType = salaryType
        };
    }

    public static UpdateEmployeeDto ValidateForUpdate(UpdateEmployeeDto request, IReadOnlyList<PayrollPieceWageRateDto>? officialRates = null)
    {
        if (request is null) throw new ArgumentException("Employee payload is required.");

        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ArgumentException("FullName is required.");
        if (request.DepartmentId <= 0) throw new ArgumentException("DepartmentId is required.");
        if (request.HireDate is null || request.HireDate == default) throw new ArgumentException("HireDate is required.");
        if (string.IsNullOrWhiteSpace(request.Status)) throw new ArgumentException("Status is required.");
        var salaryType = NormalizeSalaryType(request.SalaryType);
        ValidateSalary(request.BasicSalary, salaryType);
        ValidatePieceRates(request.PieceRates, salaryType, officialRates);

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

    private static void ValidatePieceRates(IReadOnlyList<EmployeePieceRateAssignmentInputDto>? pieceRates, string salaryType, IReadOnlyList<PayrollPieceWageRateDto>? officialRates = null)
    {
        if (salaryType != "PieceWage")
            return;

        if (pieceRates is null || pieceRates.Count == 0)
            throw new ArgumentException("At least one valid piece rate is required for PieceWage employees.");

        foreach (var item in pieceRates)
        {
            if (item is null)
                throw new ArgumentException("Each piece rate row must contain a value.");
            if (string.IsNullOrWhiteSpace(item.PieceType))
                throw new ArgumentException("PieceType is required for each piece rate.");
            if (string.IsNullOrWhiteSpace(item.Stage))
                throw new ArgumentException("Stage is required for each piece rate.");

            if (item.Rate < 0m)
                throw new ArgumentException("الإضافي لا يمكن أن يكون سالباً.");

            if (officialRates is not null)
            {
                var hasOfficialRate = officialRates.Any(rate =>
                    rate.IsActive
                    && string.Equals(rate.PieceType, item.PieceType.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rate.Stage, item.Stage.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!hasOfficialRate)
                {
                    var pieceTypeLabel = item.PieceType.Trim();
                    var stageLabel = item.Stage.Trim();
                    throw new ArgumentException($"لم يتم تعريف أجر القطعة لنوع القطعة {pieceTypeLabel} في المرحلة {stageLabel}. عرّف السعر أولًا من تبويب أجور القطع.");
                }
            }
        }
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
