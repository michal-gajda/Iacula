namespace Iacula.Infrastructure.EntityFramework;

internal sealed class DatabaseInitializer
{
    private readonly IaculaDbContext dbContext;

    public DatabaseInitializer(IaculaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => this.dbContext.Database.EnsureCreatedAsync(cancellationToken);
}
