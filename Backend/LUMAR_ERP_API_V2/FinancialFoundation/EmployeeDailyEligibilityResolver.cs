using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.FinancialFoundation;

public sealed record DailyEligibilityResult(string Status, bool IsEligible);

public sealed class EmployeeDailyEligibilityResolver
{
    public async Task<DailyEligibilityResult> ResolveAsync(SqlConnection connection, SqlTransaction? transaction, int employeeId, DateOnly date, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT e.SalaryType,e.HireDate,e.TerminationDate,
              (SELECT TOP(1) Status FROM dbo.EmployeeStatusHistory WHERE EmployeeId=e.EmployeeID AND EffectiveDate<=@date ORDER BY EffectiveDate DESC),
              (SELECT TOP(1) PaymentClassification FROM dbo.LeaveRequests WHERE EmployeeId=e.EmployeeID AND Status=N'Approved' AND @date BETWEEN CAST(StartDate AS date) AND CAST(EndDate AS date)),
              (SELECT TOP(1) IsAbsent FROM dbo.EmployeeAttendances WHERE EmployeeId=e.EmployeeID AND CAST(AttendanceDate AS date)=@date)
            FROM dbo.Employees e WHERE e.EmployeeID=@employeeId;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@date", date.ToDateTime(TimeOnly.MinValue));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Employee was not found.");
        if (reader.GetString(0) == "PieceWage") return new("NotApplicablePieceWage", false);
        if (date < DateOnly.FromDateTime(reader.GetDateTime(1))) return new("IneligibleBeforeStartDate", false);
        if (!reader.IsDBNull(2) && date >= DateOnly.FromDateTime(reader.GetDateTime(2))) return new("IneligibleInactiveEmployee", false);
        if (!reader.IsDBNull(3) && reader.GetString(3) != "Active") return new("IneligibleInactiveEmployee", false);
        if (!reader.IsDBNull(4)) return reader.GetString(4) == "Paid" ? new("EligiblePaidLeave", true) : new("IneligibleUnpaidLeave", false);
        if (!reader.IsDBNull(5) && reader.GetBoolean(5)) return new("IneligibleAbsence", false);
        return new("EligibleWorkedDay", true);
    }
}