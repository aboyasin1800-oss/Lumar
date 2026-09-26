using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LUMAR_ERP_API_V2.ErrorHandling;

public sealed class GlobalApiErrorResultFilter : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is IStatusCodeActionResult { StatusCode: >= 400 and <= 599 } result)
        {
            var correlationId = context.HttpContext.TraceIdentifier;
            context.Result = new ObjectResult(ApiErrorResponseFactory.CreateForStatus(
                context.HttpContext.Request.Path,
                correlationId))
            {
                StatusCode = result.StatusCode,
                DeclaredType = typeof(ApiErrorResponse)
            };
            context.HttpContext.Response.Headers.Append("X-Correlation-Id", correlationId);
        }

        await next();
    }
}