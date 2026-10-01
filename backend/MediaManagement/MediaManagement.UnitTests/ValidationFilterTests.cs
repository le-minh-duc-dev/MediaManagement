using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace MediaManagement.UnitTests;

public class ValidationFilterTests
{
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
