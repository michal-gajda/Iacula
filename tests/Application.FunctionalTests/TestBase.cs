namespace Iacula.Application.FunctionalTests;

using Iacula.Application;
using Iacula.Infrastructure;
using Iacula.Infrastructure.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public abstract class TestBase : IAsyncDisposable
{
    protected ServiceProvider Provider { get; private set; } = null!;

    private readonly SqliteConnection sqliteConnection;

    protected TestBase()
    {
        var collation = new Dictionary<string, string?>
        {
            {"ConnectionStrings:DefaultConnection", "DataSource=:memory:;Cache=Shared"},
            {"Logging:LogLevel:Default","Information"},
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(collation)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        this.sqliteConnection = new SqliteConnection(connectionString);
        this.sqliteConnection.Open();

        services.RemoveAll(typeof(IaculaDbContext));

        services.AddDbContext<IaculaDbContext>(options =>
        {
            options.UseSqlite(this.sqliteConnection);
        });

        this.Provider = services.BuildServiceProvider(validateScopes: true);

        using var scope = this.Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        dbContext.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await this.Provider.DisposeAsync();
        await this.sqliteConnection.DisposeAsync();
    }
}
