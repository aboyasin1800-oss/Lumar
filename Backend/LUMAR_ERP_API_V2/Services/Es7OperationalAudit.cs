using LUMAR_ERP_API_V2.DTOs.Auth;

namespace LUMAR_ERP_API_V2.Services;

public sealed class Es7OperationalAudit(ILogger<Es7OperationalAudit> logger)
{
    public void Record(CurrentUserDto user, string action, Guid sourceOperationId, long referenceId, string correlationId)
    {
        logger.LogInformation(
            "ES7 operational audit. UserId: {UserId}; Username: {Username}; Action: {Action}; SourceOperationId: {SourceOperationId}; ReferenceId: {ReferenceId}; CorrelationId: {CorrelationId}",
            user.UserId, user.Username, action, sourceOperationId, referenceId, correlationId);
    }
}