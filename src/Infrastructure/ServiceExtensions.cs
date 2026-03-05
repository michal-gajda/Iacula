namespace Iacula.Infrastructure;

using System.Reflection;
using Iacula.Infrastructure.EntityFramework;
using Iacula.Infrastructure.MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class ServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        services.AddEntityFramework(configuration);
        services.AddMassTransit(configuration);

        services.AddSingleton(TimeProvider.System);
    }

    public static async Task InitializeInfrastructureAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
