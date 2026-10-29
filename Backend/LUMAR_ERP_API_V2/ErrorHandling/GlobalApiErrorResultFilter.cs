using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LUMAR_ERP_API_V2.ErrorHandling;

public sealed class GlobalApiErrorResultFilter : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not IStatusCodeActionResult { StatusCode: >= 400 and <= 599 } result)
        {
            await next();
            return;
        }

        if (context.Result is ObjectResult objectResult && objectResult.Value is not null)
        {
            if (objectResult.Value is not ApiErrorResponse)
            {
                context.HttpContext.Response.Headers.Append("X-Correlation-Id", context.HttpContext.TraceIdentifier);
                await next();
                return;
            }
        }

        var correlationId = context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(ApiErrorResponseFactory.CreateForStatus(
            context.HttpContext.Request.Path,
            correlationId))
        {
            StatusCode = result.StatusCode,
            DeclaredType = typeof(ApiErrorResponse)
        };
        context.HttpContext.Response.Headers.Append("X-Correlation-Id", correlationId);

        await next();
    }
}