namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class ServiceExtensions
{
    public static void AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("Missing configuration value 'Database:Provider'.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing connection string 'ConnectionStrings:DefaultConnection'.");
        }

        var normalizedProvider = provider.Trim();

        services.AddDbContext<IaculaDbContext>(options =>
        {
            if (normalizedProvider.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                options.UseOracle(connectionString);
                return;
            }

            throw new InvalidOperationException($"Unsupported database provider '{normalizedProvider}'.");
        });

        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<IUnitOfWork, EntityFrameworkUnitOfWork>();
        services.AddScoped<IFormRepository, FormRepository>();

        services.AddHostedService<FormPublisherService>();
    }
}
