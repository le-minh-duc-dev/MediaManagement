using MediaManagement.Interfaces;
using Microsoft.Extensions.Primitives;

namespace MediaManagement.Middlewares;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(
        HttpContext context,
        ICorrelationIdAccessor accessor,
        ILogger<CorrelationIdMiddleware> logger
    )
    {
        // Try to get correlation ID from incoming request
        string correlationId = GetOrCreateCorrelationId(context, logger);

        // Store it for the duration of the request
        accessor.CorrelationId = correlationId;

        // Add to response headers so clients can see it
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });

        // Add to the HttpContext.Items for easy access
        context.Items[CorrelationIdHeader] = correlationId;

        await next(context);
    }

    private string GetOrCreateCorrelationId(
        HttpContext context,
        ILogger<CorrelationIdMiddleware> logger
    )
    {
        // Check if the request already has a correlation ID
        if (
            context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues existingId)
            && !string.IsNullOrWhiteSpace(existingId)
        )
        {
            logger.LogInformation(
                "Using existing correlation ID: {CorrelationId}",
                existingId.ToString()
            );
            return existingId.ToString();
        }

        string newCorrelationId = Guid.NewGuid().ToString("N");
        logger.LogInformation("Generated new correlation ID: {CorrelationId}", newCorrelationId);
        // Generate a new one if not present
        return newCorrelationId;
    }
}
