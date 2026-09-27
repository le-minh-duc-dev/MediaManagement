# Conventions

- Use [] for Collection initialization.
- Use file-scoped namespaces
- Use string interpolation
- Prefer static method if possible
- Add postfix Async for methods returning tasks. Exclude public methods in controllers.

## Ef Core
- DbContext is Not thread-safe. Don't share contexts between threads. Make sure to await all async calls before continuing to use the context instance.
An InvalidOperationException thrown by EF Core code can put the context into an unrecoverable state. Such exceptions indicate a program error and are not designed to be recovered from.

## Application Results and HTTP responses

- Application services return `Task<Result>` for commands without data and `Task<Result<T>>` for operations with data. Use `MediaManagement.Results`; these types have no HTTP dependency.
- Return expected business failures with a category and stable errors. Let unexpected exceptions propagate to the global exception handler. Do not catch every exception and turn it into a business failure.
- Repositories continue to return entities/data. Translate missing entities or business conflicts into Results in the application service.
- `Result<T>.Value` is available only on success. A failure requires at least one error. Successful nullable values are permitted when intentional.
- Controller extensions in `MediaManagement.Api` select the success response and map every failure centrally. Success payloads are unwrapped.

```csharp
// Application service example; no production endpoint is added by this foundation.
public async Task<Result<PostDto>> GetAsync(Guid id, CancellationToken cancellationToken)
{
    var post = await repository.GetByIdAsync(id, cancellationToken: cancellationToken);
    return post is null
        ? Result<PostDto>.Failure(ErrorType.NotFound, new Error("post.not_found"))
        : Result<PostDto>.Success(new PostDto(post.Id, post.Caption));
}

// Controller examples:
return (await service.GetAsync(id, cancellationToken)).ToOk(this);
var result = await service.CreateAsync(request, cancellationToken);
return result.ToCreatedAtAction(this, nameof(Get),
    result.IsSuccess ? new { id = result.Value.Id } : null);
return (await service.DeleteAsync(id, cancellationToken)).ToNoContent(this);
```

Never read a failed Result's Value. Include required route values such as `version` when generating links to versioned actions.

| Category | HTTP status | Top-level code |
| --- | --- | --- |
| Validation | 400 | `validation.failed` |
| BadRequest | 400 | `request.invalid` |
| Unauthorized | 401 | `auth.unauthorized` |
| Forbidden | 403 | `auth.forbidden` |
| NotFound | 404 | `resource.not_found` |
| Conflict | 409 | `resource.conflict` |
| Unexpected | 500 | `server.unexpected` |

Unauthorized/Forbidden Result mappings return status responses, not authentication challenges. Authentication schemes remain responsible for challenge behavior and headers when introduced.

## Validation and client localization

- Add public `AbstractValidator<TRequest>` implementations in the application assembly; assembly scanning registers them as scoped services. Use explicit child validators (`SetValidator` / `RuleForEach`) for nested objects.
- The global asynchronous action filter validates non-null bound request arguments before controller actions. It runs all registered validators sequentially with the request cancellation token. It skips service parameters and types without validators.
- Invalid binding/model state is rejected before the filter with `request.invalid`. This includes malformed JSON, missing required input, and conversion errors; framework messages are never returned.
- Automatic validation is an HTTP boundary concern. Application services must enforce business invariants for all callers; jobs and direct service calls do not run MVC filters.
- Give every rule a stable, namespaced `.WithErrorCode(...)` such as `post.title.required`. Codes without a dot, including FluentValidation defaults, become `validation.invalid`. Treat codes and parameter names as public contracts.
- Use `.WithParameters(...)` from `MediaManagement.Api.Validation` to attach explicit safe localization arguments. This uses FluentValidation custom state; do not overwrite it with another `WithState` call. Only `ErrorParameters` custom state is serialized.

```csharp
RuleFor(x => x.Title)
    .MaximumLength(100)
    .WithErrorCode("validation.max_length")
    .WithParameters(new ErrorParameters(new Dictionary<string, object?>
    {
        ["maxLength"] = 100,
    }));
```

Parameters are immutable copies containing only strings, booleans, finite numbers, or null. Supply constraints such as lengths and limits, never attempted input, credentials, exception messages, or arbitrary objects. No FluentValidation placeholder values are automatically copied.

Responses use `application/problem+json`:

```json
{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "instance": "/api/v1/posts",
  "code": "validation.failed",
  "errors": [
    {
      "code": "validation.max_length",
      "field": "title",
      "parameters": { "maxLength": 100 }
    }
  ],
  "traceId": "request-trace",
  "correlationId": "request-correlation"
}
```

Field paths follow JSON naming policy and `[JsonPropertyName]`, retaining collection indexes (for example, `items[0].label`). Model-wide/business errors use `field: null`. For custom property overrides, use JSON-facing field names. The client selects a translation by `errors[].code`, interpolates `parameters`, and uses a generic localized fallback for unknown codes. HTTP titles are generic, not user-facing localized messages.

Unhandled exceptions return `server.unexpected` in development and production; details are logged server-side. Canceled requests are not converted into failure Results. Rate-limit responses use `rate_limit.exceeded` and preserve `Retry-After`. Empty framework errors use the same envelope, including `request.method_not_allowed` (405), `request.unsupported_media_type` (415), and `resource.not_found` (404).

Integration tests supply their own demonstration controllers, services, and validators through an MVC application part; these are never registered in the production application. Run all tests with `dotnet test MediaManagement.slnx` from this directory.
