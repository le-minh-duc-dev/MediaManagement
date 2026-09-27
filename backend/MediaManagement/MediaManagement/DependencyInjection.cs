using System.Threading.RateLimiting;
using Asp.Versioning;
using MediaManagement.Database;
using MediaManagement.Implementation;
using MediaManagement.Implementation.Repositories;
using MediaManagement.Interfaces;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Middlewares;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace MediaManagement;

public static class DependencyInjection
{
    public const string CorsPolicyName = "DefaultPolicy";

    public static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.AddApi(configuration, environment);
        services.AddApplication(configuration, environment);
        services.AddInfrastructure(configuration, environment);
        return services;
    }

    public static WebApplication RegisterMiddlewares(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
        }
        else
        {
            // This middleware should go first in the pipeline to catch exceptions from other middlewares
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        // this middleware should go before routing to ensure that all requests are redirected to HTTPS, and be not limited to Production environment only,
        // as it is a security best practice to enforce HTTPS in all environments.
        app.UseHttpsRedirection();

        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseRouting();

        app.UseCors(CorsPolicyName);

        app.UseAuthentication();

        // Authorization should be placed after authentication and before endpoint mapping to ensure
        // that the user is authenticated before checking their permissions for a specific resource.
        // This is a performance optimization as it avoids unnecessary authorization checks.
        app.UseRateLimiter();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }

    private static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
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
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(
                            retryAfter.TotalSeconds
                        )
                        .ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "rate_limit_exceeded" },
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

    private static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found."
            );

        services.AddDbContext<MediaManagementContext>(options =>
        {
            options.UseSqlite(connectionString);

            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddSerilog(
            (services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(configuration)
                    .ReadFrom.Services(services);
            }
        );

        services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();

        return services;
    }
}
