using System.Threading.RateLimiting;
using Amazon;
using Amazon.S3;
using Asp.Versioning;
using FluentValidation;
using MediaManagement.ActionFilters;
using MediaManagement.BackgroundWorkers;
using MediaManagement.Contracts;
using MediaManagement.Contracts.Validation;
using MediaManagement.Database;
using MediaManagement.ExceptionHandlers;
using MediaManagement.Implementation;
using MediaManagement.Implementation.Repositories;
using MediaManagement.Implementation.Services;
using MediaManagement.Implementation.Services.ApiServices;
using MediaManagement.Interfaces;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Interfaces.Services;
using MediaManagement.Middlewares;
using MediaManagement.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    private static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.AddScoped<ValidationActionFilter>();
        services
            .AddControllers(options => options.Filters.AddService<ValidationActionFilter>())
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = InvalidRequestResponse.Create;
                // Empty MVC error responses are formatted by the shared status-code handler.
                options.SuppressMapClientErrors = true;
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

    private static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IUploadSessionService, UploadSessionService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<UploadCleanupService>();
        return services;
    }

    private static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        var connectionString =
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
        services.AddScoped<IPostRepository, PostRepository>();

        services
            .AddOptions<UploadOptions>()
            .Bind(configuration.GetSection(UploadOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Region), "Uploads:Region is required.")
            .Validate(
                x =>
                    x.MaxItems is > 0 and <= 100
                    && x.MaxFileSizeBytes is > 0 and <= 5L * 1024 * 1024 * 1024,
                "Upload limits must allow 1-100 items and files up to 5 GiB."
            )
            .Validate(
                x =>
                    x.AllowedContentTypes is { Length: > 0 }
                    && x.AllowedContentTypes.All(t => !string.IsNullOrWhiteSpace(t)),
                "At least one allowed content type is required."
            )
            .Validate(
                x =>
                    x.UrlLifetimeMinutes > 0
                    && x.UrlLifetimeMinutes <= x.SessionLifetimeMinutes
                    && x.SessionLifetimeMinutes < x.CleanupAfterHours * 60
                    && x.CleanupAfterHours is > 0 and < 24
                    && x.CleanupIntervalMinutes > 0
                    && x.CleanupAfterHours * 60 + x.CleanupIntervalMinutes < 24 * 60
                    && x.CleanupBatchSize is > 0 and <= 1000,
                "Upload expiry and cleanup settings are invalid."
            );

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            return string.IsNullOrWhiteSpace(options.BucketName)
                ? throw new InvalidOperationException(
                    "Configure Uploads:BucketName before using uploads."
                )
                : (IAmazonS3)new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
        });

        services.AddScoped<IUploadStorage, S3UploadStorage>();
        services.AddHostedService<UploadCleanupWorker>();

        return services;
    }
}
