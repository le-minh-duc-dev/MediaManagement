using MediaManagement.Contracts;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MediaManagement.UnitTests;

public class ResultTests
{
    [Fact]
    public void Factories_preserve_invariants_and_copy_errors()
    {
        Assert.True(Result.Success().IsSuccess);
        Assert.Empty(Result.Success().Errors);
        Assert.Equal(42, Result<int>.Success(42).Value);
        Assert.Null(Result<string?>.Success(null).Value);
        Assert.Throws<ArgumentException>(() => Result.Failure(ErrorType.Validation));
        Assert.Throws<ArgumentException>(() => Result<int>.Failure(ErrorType.Validation));
        Assert.Throws<ArgumentException>(() => Result.Failure(ErrorType.Validation, [null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Result.Failure((ErrorType)100, new Error("test.error"))
        );
        Assert.Throws<ArgumentException>(() => new Error(" "));

        Error[] errors = [new("post.missing")];
        var failure = Result<int>.Failure(ErrorType.NotFound, errors);
        errors[0] = new("changed.error");
        Assert.Equal("post.missing", Assert.Single(failure.Errors).Code);
        Assert.True(failure.IsFailure);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400, "validation.failed")]
    [InlineData(ErrorType.BadRequest, 400, "request.invalid")]
    [InlineData(ErrorType.Unauthorized, 401, "auth.unauthorized")]
    [InlineData(ErrorType.Forbidden, 403, "auth.forbidden")]
    [InlineData(ErrorType.NotFound, 404, "resource.not_found")]
    [InlineData(ErrorType.Conflict, 409, "resource.conflict")]
    [InlineData(ErrorType.Unexpected, 500, "server.unexpected")]
    public void All_helpers_use_the_same_failure_contract(
        ErrorType category,
        int status,
        string code
    )
    {
        var controller = new TestController
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() },
        };
        var plain = Result.Failure(category, new Error("test.error"));
        var typed = Result<int>.Failure(category, new Error("test.error"));
        IActionResult[] responses =
        [
            plain.ToOk(controller),
            plain.ToNoContent(controller),
            typed.ToOk(controller).Result!,
            typed.ToCreatedAtAction(controller, "Get", new { id = 1 }).Result!,
        ];
        foreach (var response in responses)
        {
            var result = Assert.IsType<ObjectResult>(response);
            Assert.Equal(status, result.StatusCode);
            Assert.Contains("application/problem+json", result.ContentTypes);
            var problem = Assert.IsType<ProblemDetails>(result.Value);
            Assert.Equal(code, problem.Extensions["code"]);
        }
    }

    [Fact]
    public void Success_helpers_return_unwrapped_values()
    {
        var controller = new TestController();
        Assert.IsType<OkResult>(Result.Success().ToOk(controller));
        Assert.IsType<NoContentResult>(Result.Success().ToNoContent(controller));
        Assert.Equal(
            42,
            Assert.IsType<OkObjectResult>(Result<int>.Success(42).ToOk(controller).Result).Value
        );
        var created = Assert.IsType<CreatedAtActionResult>(
            Result<int>.Success(42).ToCreatedAtAction(controller, "Get", new { id = 42 }).Result
        );
        Assert.Equal("Get", created.ActionName);
        Assert.Equal(42, created.Value);
        Assert.Equal(42, created.RouteValues!["id"]);
    }

    [Fact]
    public void Parameters_are_defensively_copied_and_reject_objects_and_nonfinite_numbers()
    {
        Dictionary<string, object?> source = new() { ["maxLength"] = 100 };
        var parameters = new ErrorParameters(source);
        source["maxLength"] = 200;
        Assert.Equal(100, parameters["maxLength"]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)parameters).Add("x", 1)
        );
        foreach (
            var value in new object[]
            {
                new { secret = "private" },
                new[] { 1 },
                double.NaN,
                double.PositiveInfinity,
            }
        )
        {
            Assert.Throws<ArgumentException>(() =>
                new ErrorParameters(new Dictionary<string, object?> { ["unsafe"] = value })
            );
        }
    }

    private sealed class TestController : ControllerBase { }
}
