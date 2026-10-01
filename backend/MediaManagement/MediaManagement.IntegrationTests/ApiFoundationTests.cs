using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using FluentValidation;
using MediaManagement.Contracts;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace MediaManagement.IntegrationTests;

public class ApiFoundationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    private readonly HttpClient client;

    public ApiFoundationTests(ApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }
        );
    }

    [Fact]
    public async Task Success_results_have_correct_status_body_and_location()
    {
        var response = await client.GetAsync("/foundation/7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.False(body.TryGetProperty("isSuccess", out _));

        response = await client.PostAsJsonAsync(
            "/foundation",
            new { title = "valid", items = new[] { new { label = "ok" } } }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/foundation/7", response.Headers.Location!.ToString());
        Assert.Equal(
            7,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32()
        );

        response = await client.DeleteAsync("/foundation/7");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        response = await client.GetAsync("/foundation/ok");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation_returns_localizable_errors_and_skips_service()
    {
        var before = factory.Services.GetRequiredService<TestApplicationService>().Calls;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/foundation")
        {
            Content = JsonContent.Create(
                new { title = "PRIVATE-INPUT-TOO-LONG", items = new[] { new { label = "" } } }
            ),
        };
        request.Headers.Add("X-Correlation-Id", "test-correlation");
        var response = await client.SendAsync(request);
        var problem = await Problem(response, 400, "validation.failed");
        var errors = problem.GetProperty("errors").EnumerateArray().ToArray();
        var title = Assert.Single(errors, e => e.GetProperty("field").GetString() == "title");
        Assert.Equal("validation.max_length", title.GetProperty("code").GetString());
        Assert.Equal(10, title.GetProperty("parameters").GetProperty("maxLength").GetInt32());
        Assert.Contains(
            errors,
            e =>
                e.GetProperty("field").GetString() == "items[0].label"
                && e.GetProperty("code").GetString() == "validation.required"
        );
        Assert.Equal("test-correlation", problem.GetProperty("correlationId").GetString());
        Assert.Equal(
            "test-correlation",
            Assert.Single(response.Headers.GetValues("X-Correlation-Id"))
        );
        Assert.DoesNotContain("PRIVATE-INPUT", problem.GetRawText());
        Assert.DoesNotContain("must not", problem.GetRawText());
        Assert.Equal(before, factory.Services.GetRequiredService<TestApplicationService>().Calls);
    }

    [Fact]
    public async Task Async_rules_are_awaited_and_default_codes_and_arbitrary_state_are_not_exposed()
    {
        var response = await client.PostAsJsonAsync(
            "/foundation",
            new { title = "reserved", items = Array.Empty<object>() }
        );
        var problem = await Problem(response, 400, "validation.failed");
        Assert.Contains(
            problem.GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("code").GetString() == "post.title.reserved"
        );

        response = await client.PostAsJsonAsync("/foundation/fallback", new { title = "" });
        problem = await Problem(response, 400, "validation.failed");
        var error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal("validation.invalid", error.GetProperty("code").GetString());
        Assert.Empty(error.GetProperty("parameters").EnumerateObject());
        Assert.DoesNotContain("PRIVATE", problem.GetRawText());
    }

    [Theory]
    [InlineData("{", "application/json")]
    [InlineData("", "application/json")]
    [InlineData("{}", "application/json")]
    [InlineData("{\"title\":123}", "application/json")]
    public async Task Binding_failures_use_codes_and_skip_application_service(
        string json,
        string contentType
    )
    {
        var before = factory.Services.GetRequiredService<TestApplicationService>().Calls;
        var response = await client.PostAsync(
            "/foundation",
            new StringContent(json, Encoding.UTF8, contentType)
        );
        var problem = await Problem(response, 400, "request.invalid");
        Assert.All(
            problem.GetProperty("errors").EnumerateArray(),
            e => Assert.Equal("request.invalid", e.GetProperty("code").GetString())
        );
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.Equal(before, factory.Services.GetRequiredService<TestApplicationService>().Calls);
    }

    [Fact]
    public async Task Binding_field_paths_keep_json_names_and_indexes()
    {
        var response = await client.PostAsync(
            "/foundation",
            new StringContent(
                "{\"title\":\"ok\",\"items\":[{\"label\":123}]}",
                Encoding.UTF8,
                "application/json"
            )
        );
        var problem = await Problem(response, 400, "request.invalid");
        Assert.Contains(
            problem.GetProperty("errors").EnumerateArray(),
            e => e.GetProperty("field").GetString() == "items[0].label"
        );

        response = await client.GetAsync("/foundation/query?count=PRIVATE");
        problem = await Problem(response, 400, "request.invalid");
        Assert.Equal("count", problem.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task Requests_without_validators_and_optional_null_bodies_proceed()
    {
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await client.PostAsJsonAsync("/foundation/unvalidated", new { title = "hello" })
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await client.PostAsync(
                    "/foundation/optional",
                    new StringContent("null", Encoding.UTF8, "application/json")
                )
            ).StatusCode
        );
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400, "validation.failed")]
    [InlineData(ErrorType.BadRequest, 400, "request.invalid")]
    [InlineData(ErrorType.Unauthorized, 401, "auth.unauthorized")]
    [InlineData(ErrorType.Forbidden, 403, "auth.forbidden")]
    [InlineData(ErrorType.NotFound, 404, "resource.not_found")]
    [InlineData(ErrorType.Conflict, 409, "resource.conflict")]
    [InlineData(ErrorType.Unexpected, 500, "server.unexpected")]
    public async Task Business_failures_are_serialized_consistently(
        ErrorType type,
        int status,
        string code
    )
    {
        var response = await client.GetAsync($"/foundation/failure/{type}");
        var problem = await Problem(response, status, code);
        Assert.Equal(
            "post.test_error",
            problem.GetProperty("errors")[0].GetProperty("code").GetString()
        );
        Assert.Equal(
            JsonValueKind.Null,
            problem.GetProperty("errors")[0].GetProperty("field").ValueKind
        );
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Exceptions_return_safe_json_in_all_environments(string environment)
    {
        await using var host = new ApiFactory(environment);
        using var http = host.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }
        );
        var response = await http.GetAsync("/foundation/throw");
        var problem = await Problem(response, 500, "server.unexpected");
        Assert.DoesNotContain("PRIVATE", problem.GetRawText());
        Assert.DoesNotContain("stack", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/missing-endpoint", 404, "resource.not_found")]
    [InlineData("/foundation/empty-error", 404, "resource.not_found")]
    [InlineData("/foundation/query?count=PRIVATE", 400, "request.invalid")]
    public async Task Framework_and_empty_errors_follow_the_contract(
        string url,
        int status,
        string code
    )
    {
        await Problem(await client.GetAsync(url), status, code);
    }

    [Fact]
    public async Task Unsupported_media_and_method_return_problem_details()
    {
        await Problem(
            await client.PostAsync("/foundation", new StringContent("PRIVATE")),
            415,
            "request.unsupported_media_type"
        );
        await Problem(
            await client.PutAsJsonAsync("/foundation/7", new { }),
            405,
            "request.method_not_allowed"
        );
    }

    [Fact]
    public async Task Rate_limit_rejection_preserves_retry_afterAsync()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/foundation/limited")).StatusCode);
        var response = await client.GetAsync("/foundation/limited");
        await Problem(response, 429, "rate_limit.exceeded");
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    private static async Task<JsonElement> Problem(
        HttpResponseMessage response,
        int status,
        string code
    )
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, json.GetProperty("status").GetInt32());
        Assert.Equal("about:blank", json.GetProperty("type").GetString());
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("correlationId").GetString()));
        return json;
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string environment;

    public ApiFactory()
        : this("Production") { }

    internal ApiFactory(string environment) => this.environment = environment;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(FoundationController).Assembly);
            services.AddSingleton<TestApplicationService>();
            services.AddValidatorsFromAssemblyContaining<CreateRequestValidator>();
            services.AddRateLimiter(options =>
                options.AddFixedWindowLimiter(
                    "test",
                    limiter =>
                    {
                        limiter.PermitLimit = 1;
                        limiter.Window = TimeSpan.FromHours(1);
                        limiter.QueueLimit = 0;
                    }
                )
            );
        });
    }
}

public sealed class TestApplicationService
{
    public int Calls { get; private set; }

    public Task<Result<TestResponse>> CreateAsync(CreateRequest request)
    {
        Calls++;
        return Task.FromResult(Result<TestResponse>.Success(new(7, request.Title)));
    }
}

[ApiController]
[ApiVersionNeutral]
[Route("foundation")]
public sealed class FoundationController(TestApplicationService service) : ControllerBase
{
    [HttpGet("{id:int}")]
    public ActionResult<TestResponse> Get(int id) =>
        Result<TestResponse>.Success(new(id, "hello")).ToOk(this);

    [HttpPost]
    public async Task<ActionResult<TestResponse>> Create(CreateRequest request) =>
        (await service.CreateAsync(request)).ToCreatedAtAction(this, nameof(Get), new { id = 7 });

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => Result.Success().ToNoContent(this);

    [HttpGet("ok")]
    public IActionResult PlainOk() => Result.Success().ToOk(this);

    [HttpPost("fallback")]
    public IActionResult Fallback(FallbackRequest request) => Result.Success().ToOk(this);

    [HttpPost("unvalidated")]
    public IActionResult Unvalidated(UnvalidatedRequest request) => Result.Success().ToOk(this);

    [HttpPost("optional")]
    public IActionResult Optional(UnvalidatedRequest? request) => Result.Success().ToOk(this);

    [HttpGet("failure/{type}")]
    public IActionResult Failure(ErrorType type) =>
        Result.Failure(type, new Error("post.test_error")).ToOk(this);

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException("PRIVATE exception detail");

    [HttpGet("empty-error")]
    public IActionResult EmptyError() => NotFound();

    [HttpGet("query")]
    public IActionResult Query([FromQuery] int count) => Ok(count);

    [HttpGet("limited")]
    [EnableRateLimiting("test")]
    public IActionResult Limited() => Ok();
}

public sealed record TestResponse(int Id, string Title);

public sealed record CreateRequest(string Title, List<ItemRequest>? Items);

public sealed record ItemRequest([property: JsonPropertyName("label")] string DisplayName);

public sealed record FallbackRequest(string Title);

public sealed record UnvalidatedRequest(string Title);

public sealed class CreateRequestValidator : AbstractValidator<CreateRequest>
{
    public CreateRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("validation.required");
        RuleFor(x => x.Title).MaximumLength(10).WithErrorCode("validation.max_length");

        RuleFor(x => x.Title)
            .MustAsync(
                async (title, cancellationToken) =>
                {
                    await Task.Delay(1, cancellationToken);
                    return title != "reserved";
                }
            )
            .WithErrorCode("post.title.reserved");
        RuleForEach(x => x.Items).SetValidator(new ItemRequestValidator());
    }
}

public sealed class ItemRequestValidator : AbstractValidator<ItemRequest>
{
    public ItemRequestValidator() =>
        RuleFor(x => x.DisplayName).NotEmpty().WithErrorCode("validation.required");
}

public sealed class FallbackRequestValidator : AbstractValidator<FallbackRequest>
{
    public FallbackRequestValidator() =>
        RuleFor(x => x.Title).NotEmpty().WithState(_ => new { secret = "PRIVATE" });
}
