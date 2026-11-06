namespace LUMAR_ERP_API_V2.DTOs.Settings;

public sealed record TestDataResetExecutionResultDto(
    bool Success,
    string Message,
    string DatabaseName,
    string? Details = null,
    bool Verified = false);
