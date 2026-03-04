namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Domain.Interfaces;

internal sealed class EntityFrameworkUnitOfWork : IUnitOfWork
{
    private readonly IaculaDbContext dbContext;

    public EntityFrameworkUnitOfWork(IaculaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await using var transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await action(cancellationToken);

            await this.dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
