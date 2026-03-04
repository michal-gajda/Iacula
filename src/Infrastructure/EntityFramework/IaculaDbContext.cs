namespace Iacula.Infrastructure.EntityFramework;

using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class IaculaDbContext : DbContext
{
    public DbSet<FormDbEntity> Forms => Set<FormDbEntity>();
    public DbSet<OutboxDbEntity> Outbox => Set<OutboxDbEntity>();

    public IaculaDbContext(DbContextOptions<IaculaDbContext> options) : base(options)
    {
    }
}
