namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Interfaces;
using Iacula.Infrastructure.EntityFramework.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class ServiceExtensions
{
    public static void AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IaculaDbContext>(options =>
        {
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")!);
        });

        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<IUnitOfWork, EntityFrameworkUnitOfWork>();
        services.AddScoped<IFormRepository, FormRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddHostedService<OutboxPublisherService>();
    }
}
