namespace MediaManagement.Boostrap;

public static class DependencyInjection
{
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
}
