namespace LUMAR_ERP_API_V2.ErrorHandling;

public sealed record ApiErrorResponse(
    bool Success,
    string ErrorCode,
    string UserFriendlyMessage,
    string CorrelationId);