using FluentValidation;
using MediaManagement.Api.Validation;
using MediaManagement.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaManagement.UnitTests;

public class ValidationFilterTests
{
    [Fact]
    public async Task Validators_run_sequentially_before_action()
    {
        List<int> calls = [];
        var first = new InlineValidator<Request>();
        first
            .RuleFor(x => x.Name)
            .MustAsync(
                async (_, _) =>
                {
                    calls.Add(1);
                    await Task.Yield();
                    calls.Add(2);
                    return true;
                }
            );
        var second = new InlineValidator<Request>();
        second
            .RuleFor(x => x.Name)
            .Must(_ =>
            {
                calls.Add(3);
                return true;
            });
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<Request>>(first)
            .AddSingleton<IValidator<Request>>(second)
            .BuildServiceProvider();
        var context = Context(provider);
        await new ValidationActionFilter(Options.Create(new JsonOptions())).OnActionExecutionAsync(
            context,
            () =>
            {
                calls.Add(4);
                return Task.FromResult(new ActionExecutedContext(context, [], new object()));
            }
        );
        Assert.Equal(new[] { 1, 2, 3, 4 }, calls);
    }

    [Fact]
    public async Task Request_cancellation_propagates_and_never_invokes_action_or_formats_500()
    {
        using var cancellation = new CancellationTokenSource();
        var validator = new InlineValidator<Request>();
        validator
            .RuleFor(x => x.Name)
            .MustAsync(
                async (_, token) =>
                {
                    Assert.Equal(cancellation.Token, token);
                    await cancellation.CancelAsync();
                    token.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                    return true;
                }
            );
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<Request>>(validator)
            .BuildServiceProvider();
        var context = Context(provider);
        context.HttpContext.RequestAborted = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ValidationActionFilter(Options.Create(new JsonOptions())).OnActionExecutionAsync(
                context,
                () => throw new InvalidOperationException("Action must not run")
            )
        );
        Assert.Null(context.Result);
        Assert.False(
            await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(
                context.HttpContext,
                new OperationCanceledException(cancellation.Token),
                cancellation.Token
            )
        );
    }

    private static ActionExecutingContext Context(IServiceProvider provider)
    {
        var action = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor
            {
                Parameters =
                [
                    new ParameterDescriptor { Name = "request", ParameterType = typeof(Request) },
                ],
            }
        );
        return new ActionExecutingContext(
            action,
            [],
            new Dictionary<string, object?> { ["request"] = new Request("hello") },
            new object()
        );
    }

    private sealed record Request(string Name);
}
