namespace Iacula.Application.FunctionalTests;

using Iacula.Application;
using Iacula.Infrastructure;
using Iacula.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public abstract class TestBase : IAsyncDisposable
{
    protected ServiceProvider Provider { get; private set; } = null!;

    protected TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppSettingsPath(), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Database:Provider", "Oracle"},
                {"Logging:LogLevel:Default", "Information"},
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        this.Provider = services.BuildServiceProvider(validateScopes: true);

        using var scope = this.Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        dbContext.Database.EnsureCreated();
        dbContext.Database.ExecuteSqlRaw("DELETE FROM \"Outbox\"");
        dbContext.Database.ExecuteSqlRaw("DELETE FROM \"Forms\"");
    }

    public async ValueTask DisposeAsync()
    {
        await this.Provider.DisposeAsync();
    }

    private static string GetAppSettingsPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "WebApi",
            "appsettings.json"));
    }
}
