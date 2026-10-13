using LUMAR_ERP_API_V2.DTOs.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Services;

public static class EmployeeScannerRequestGuard
{
    public static ActionResult? ValidateEmployeeAccount(CurrentUserDto? user)
    {
        if (user is null)
            return new UnauthorizedResult();

        if (!string.Equals(user.AccountType, "Employee", StringComparison.OrdinalIgnoreCase))
            return new ObjectResult("This endpoint is only for employee accounts.") { StatusCode = StatusCodes.Status403Forbidden };

        if (!user.EmployeeId.HasValue)
            return new ObjectResult("Employee account is not mapped to a valid employee record.") { StatusCode = StatusCodes.Status403Forbidden };

        return null;
    }

    public static ActionResult? ValidatePieceStageAssignment(int? employeeId, string? pieceType, string? stage, bool hasAssignment)
    {
        if (!employeeId.HasValue || employeeId.Value <= 0)
            return new ObjectResult("Employee account is not mapped to a valid employee record.") { StatusCode = StatusCodes.Status403Forbidden };

        if (string.IsNullOrWhiteSpace(pieceType))
            return new ObjectResult("Piece type could not be resolved for the supplied production item.") { StatusCode = StatusCodes.Status403Forbidden };

        var normalizedStage = string.IsNullOrWhiteSpace(stage) ? string.Empty : stage.Trim();
        if (!string.Equals(normalizedStage, "Cutting", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedStage, "Sewing", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!hasAssignment)
            return new ObjectResult($"Employee is not authorized for piece type '{pieceType}' at stage '{normalizedStage}'.") { StatusCode = StatusCodes.Status403Forbidden };

        return null;
    }
}
