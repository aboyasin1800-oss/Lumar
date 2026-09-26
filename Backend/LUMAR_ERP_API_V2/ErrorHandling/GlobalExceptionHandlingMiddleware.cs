namespace LUMAR_ERP_API_V2.ErrorHandling;

public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var correlationId = context.TraceIdentifier;
            var orderId = context.Request.RouteValues.TryGetValue("id", out var value)
                ? value?.ToString()
                : null;
            var userId = context.User.Identity?.Name;

            logger.LogError(
                exception,
                "Unhandled API exception. CorrelationId: {CorrelationId}; UserId: {UserId}; OrderId: {OrderId}; RequestPath: {RequestPath}",
                correlationId,
                userId,
                orderId,
                context.Request.Path);

            var response = ApiErrorResponseFactory.Create(
                context.Request.Path,
                correlationId,
                exception);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Append("X-Correlation-Id", correlationId);
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}