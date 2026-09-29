using Amazon;
using Amazon.S3;
using MediaManagement.BackgroundWorkers;
using MediaManagement.Database;
using MediaManagement.Implementation.Repositories;
using MediaManagement.Implementation.Services;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace MediaManagement.Boostrap;

public static class InfrastructureDI
{
    public static IServiceCollection AddInfrastructure(
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
