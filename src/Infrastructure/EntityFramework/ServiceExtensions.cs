namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Interfaces;
using Iacula.Infrastructure.EntityFramework.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class ServiceExtensions
{
    public static void AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? "Data Source=iacula.db"
            : connectionString;

        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);

        if (sqliteBuilder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || sqliteBuilder.Mode == SqliteOpenMode.Memory)
        {
            services.AddSingleton(sp =>
            {
                var connection = new SqliteConnection(connectionString);
                connection.Open();
                return connection;
            });

            services.AddDbContext<IaculaDbContext>((serviceProvider, options) =>
            {
                var connection = serviceProvider.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection);
            });
        }
        else
        {
            services.AddDbContext<IaculaDbContext>(options =>
            {
                options.UseSqlite(connectionString);
            });
        }

        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<IUnitOfWork, EntityFrameworkUnitOfWork>();
        services.AddScoped<IFormRepository, FormRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddHostedService<OutboxPublisherService>();
    }
}
