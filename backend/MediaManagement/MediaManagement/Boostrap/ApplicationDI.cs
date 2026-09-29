using FluentValidation;
using MediaManagement.Implementation;
using MediaManagement.Implementation.Services;
using MediaManagement.Implementation.Services.ApiServices;
using MediaManagement.Interfaces;
using MediaManagement.Interfaces.Services;

namespace MediaManagement.Boostrap;

public static class ApplicationDI
{
    public static IServiceCollection AddApplication(
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
}
