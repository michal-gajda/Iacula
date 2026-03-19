namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Interfaces;
using Iacula.Infrastructure.EntityFramework.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

internal static class ServiceExtensions
{
    public static void AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? "Data Source=iacula.db"
            : connectionString;

        connectionString = NormalizeSqliteConnectionString(connectionString);

        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        var isInMemoryDatabase = sqliteBuilder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || sqliteBuilder.Mode is SqliteOpenMode.Memory;

        if (!isInMemoryDatabase)
        {
            sqliteBuilder.DefaultTimeout = 30;
            connectionString = sqliteBuilder.ConnectionString;
        }

        if (isInMemoryDatabase)
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

    private static string NormalizeSqliteConnectionString(string connectionString)
    {
        var normalizedConnectionString = Regex.Replace(
            connectionString,
            @"(^|;)\s*Timeout\s*=\s*[^;]+",
            string.Empty,
            RegexOptions.IgnoreCase);

        normalizedConnectionString = Regex.Replace(
            normalizedConnectionString,
            @"(^|;)\s*Mode\s*=\s*Wal\s*(?=;|$)",
            string.Empty,
            RegexOptions.IgnoreCase);

        normalizedConnectionString = normalizedConnectionString.Trim(';', ' ');

        return string.IsNullOrWhiteSpace(normalizedConnectionString)
            ? "Data Source=iacula.db"
            : normalizedConnectionString;
    }
}
