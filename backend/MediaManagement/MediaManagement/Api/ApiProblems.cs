using System.Diagnostics;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MediaManagement.Api;

public static class ApiProblems
{
    public static (int Status, string Code) Map(ErrorType type) =>
        type switch
        {
            ErrorType.Validation => (400, "validation.failed"),
            ErrorType.BadRequest => (400, "request.invalid"),
            ErrorType.Unauthorized => (401, "auth.unauthorized"),
            ErrorType.Forbidden => (403, "auth.forbidden"),
            ErrorType.NotFound => (404, "resource.not_found"),
            ErrorType.Conflict => (409, "resource.conflict"),
            ErrorType.Unexpected => (500, "server.unexpected"),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string? code = null,
        IEnumerable<Error>? errors = null
    )
    {
        ProblemDetails problem = new() { Status = status };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors.ToArray();
        }

        Customize(context, problem);
        return problem;
    }

    public static void Customize(HttpContext context, ProblemDetails problem)
    {
        int status = problem.Status ?? context.Response.StatusCode;
        string code = status switch
        {
            400 => "request.invalid",
            401 => "auth.unauthorized",
            403 => "auth.forbidden",
            404 => "resource.not_found",
            405 => "request.method_not_allowed",
            409 => "resource.conflict",
            415 => "request.unsupported_media_type",
            429 => "rate_limit.exceeded",
            >= 500 => "server.unexpected",
            _ => "request.failed",
        };
        problem.Status = status;
        problem.Type = "about:blank";
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Detail = null;
        problem.Instance = context.Request.Path;
        problem.Extensions.TryAdd("code", code);
        problem.Extensions.TryAdd("errors", new[] { new Error(code) });
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
        (int status, string? code) = Map(type);
        return new ObjectResult(Create(context, status, code, errors))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
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
            contentType: "application/problem+json",
            cancellationToken: cancellationToken
        );
    }
}
