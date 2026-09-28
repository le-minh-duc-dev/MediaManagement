using System.Diagnostics;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MediaManagement.Contracts;

public static class ApiProblems
{
    private const string ProblemContentType = "application/problem+json";
    private const string CodeKey = "code";
    private const string ErrorsKey = "errors";

    public static void Customize(HttpContext context, ProblemDetails problem)
    {
        var status = problem.Status ?? context.Response.StatusCode;
        var code = status switch
        {
            StatusCodes.Status400BadRequest => ApiErrorCodes.BadRequest,
            StatusCodes.Status401Unauthorized => ApiErrorCodes.Unauthorized,
            StatusCodes.Status403Forbidden => ApiErrorCodes.Forbidden,
            StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
            StatusCodes.Status405MethodNotAllowed => ApiErrorCodes.MethodNotAllowed,
            StatusCodes.Status409Conflict => ApiErrorCodes.Conflict,
            StatusCodes.Status415UnsupportedMediaType => ApiErrorCodes.UnsupportedMediaType,
            StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimitExceeded,
            >= StatusCodes.Status500InternalServerError => ApiErrorCodes.Unexpected,
            _ => ApiErrorCodes.RequestFailed,
        };
        problem.Status = status;
        problem.Type = "about:blank";
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Detail = null;
        problem.Instance = context.Request.Path;
        problem.Extensions.TryAdd(CodeKey, code);
        problem.Extensions.TryAdd(ErrorsKey, new[] { new Error(code) });
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        problem.Extensions["correlationId"] =
            context.Items["X-Correlation-Id"]?.ToString() ?? context.TraceIdentifier;
    }

    public static ObjectResult ToActionResult(
        HttpContext context,
        ErrorType type,
        IEnumerable<Error> errors
    )
    {
        (var status, var code) = Map(type);
        return new ObjectResult(Create(context, status, code, errors))
        {
            StatusCode = status,
            ContentTypes = { ProblemContentType },
        };
    }

    public static Task WriteAsync(
        HttpContext context,
        int status,
        CancellationToken cancellationToken = default
    )
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            Create(context, status),
            options: null,
            contentType: ProblemContentType,
            cancellationToken: cancellationToken
        );
    }

    private static ProblemDetails Create(
        HttpContext context,
        int status,
        string? code = null,
        IEnumerable<Error>? errors = null
    )
    {
        ProblemDetails problem = new() { Status = status };
        if (code is not null)
        {
            problem.Extensions[CodeKey] = code;
        }

        if (errors is not null)
        {
            problem.Extensions[ErrorsKey] = errors.ToArray();
        }

        Customize(context, problem);
        return problem;
    }

    private static (int Status, string Code) Map(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => (400, ApiErrorCodes.Validation),
            ErrorType.BadRequest => (400, ApiErrorCodes.BadRequest),
            ErrorType.Unauthorized => (401, ApiErrorCodes.Unauthorized),
            ErrorType.Forbidden => (403, ApiErrorCodes.Forbidden),
            ErrorType.NotFound => (404, ApiErrorCodes.NotFound),
            ErrorType.Conflict => (409, ApiErrorCodes.Conflict),
            ErrorType.Unexpected => (500, ApiErrorCodes.Unexpected),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
