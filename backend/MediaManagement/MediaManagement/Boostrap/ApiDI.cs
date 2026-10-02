using System.Threading.RateLimiting;
using Asp.Versioning;
using MediaManagement.ActionFilters;
using MediaManagement.Contracts;
using MediaManagement.Contracts.ErrorCodes;
using MediaManagement.Models.Results;
using MediaManagement.Database;
using MediaManagement.ExceptionHandlers;
using MediaManagement.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MediaManagement.Boostrap;

public static class ApiDI
{
    public const string CorsPolicyName = "DefaultPolicy";

    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.AddScoped<ValidationActionFilter>();
        services.AddControllers(options => options.Filters.AddService<ValidationActionFilter>());
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var jsonOptions = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => new Error(ApiErrorCodes.BadRequest,
                        JsonFieldPath.Normalize(entry.Key, null, jsonOptions)));
                return ApiProblems.ToActionResult(context.HttpContext, ErrorType.BadRequest, errors);
            });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                ApiProblems.Customize(context.HttpContext, context.ProblemDetails)
        );
        services.AddExceptionHandler<ApiExceptionHandler>();
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"];
                options.MapInboundClaims = false;
            });
        services.AddAuthorization();

        services
            .AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<MediaManagementContext>();

        services
            .AddApiVersioning(options =>
            {
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.AssumeDefaultVersionWhenUnspecified = false;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
            })
            .AddMvc();

        services.AddOpenApi();

        services.AddCors(options =>
        {
            options.AddPolicy(
                CorsPolicyName,
                policy =>
                {
                    policy.WithOrigins("https://myfrontend.com").AllowAnyHeader().AllowAnyMethod();
                }
            );
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(
                            retryAfter.TotalSeconds
                        )
                        .ToString();
                }

                await ApiProblems.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    ct
                );
            };

            options.AddTokenBucketLimiter(
                "api",
                limiter =>
                {
                    limiter.TokenLimit = 100;
                    limiter.TokensPerPeriod = 20;
                    limiter.ReplenishmentPeriod = TimeSpan.FromSeconds(10);

                    limiter.QueueLimit = 0;
                }
            );

            options.AddConcurrencyLimiter(
                "expensive",
                limiter =>
                {
                    limiter.PermitLimit = 10;
                    limiter.QueueLimit = 0;
                }
            );
        });

        return services;
    }

    public static WebApplication RegisterMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(context =>
            ApiProblems.WriteAsync(
                context.HttpContext,
                context.HttpContext.Response.StatusCode,
                context.HttpContext.RequestAborted
            )
        );

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "v1");
            });
        }
        else
        {
            app.UseHsts();
        }

        // this middleware should go before routing to ensure that all requests are redirected to HTTPS, and be not limited to Production environment only,
        // as it is a security best practice to enforce HTTPS in all environments.
        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseCors(CorsPolicyName);

        app.UseAuthentication();

        // Authorization should be placed after authentication and before endpoint mapping to ensure
        // that the user is authenticated before checking their permissions for a specific resource.
        // This is a performance optimization as it avoids unnecessary authorization checks.
        app.UseRateLimiter();

        app.UseAuthorization();

        app.MapControllers();

        app.MapIdentityApi<IdentityUser>();

        return app;
    }
}
