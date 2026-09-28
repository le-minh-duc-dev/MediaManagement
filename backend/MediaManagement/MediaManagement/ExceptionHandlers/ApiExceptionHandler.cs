using MediaManagement.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace MediaManagement.ExceptionHandlers;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (
            exception is OperationCanceledException
            && httpContext.RequestAborted.IsCancellationRequested
        )
        {
            return false;
        }

        logger.LogError(
            exception,
            "Unhandled request exception. TraceId: {TraceId}, CorrelationId: {CorrelationId}",
            httpContext.TraceIdentifier,
            httpContext.Items["X-Correlation-Id"]
        );
        await ApiProblems.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            cancellationToken
        );
        return true;
    }
}
