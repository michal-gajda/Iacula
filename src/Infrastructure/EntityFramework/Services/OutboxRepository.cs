namespace Iacula.Infrastructure.EntityFramework.Services;

using Iacula.Infrastructure.EntityFramework.Interfaces;
using Iacula.Infrastructure.EntityFramework.Models;

internal sealed class OutboxRepository : IOutboxRepository
{
    private readonly IaculaDbContext dbContext;

    public OutboxRepository(IaculaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public void Add(OutboxDbEntity message)
    {
        this.dbContext.Outbox.Add(message);
    }
}
