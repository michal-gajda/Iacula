namespace Iacula.Infrastructure.EntityFramework.Interfaces;

using Iacula.Infrastructure.EntityFramework.Models;

internal interface IOutboxRepository
{
    void Add(OutboxDbEntity message);
}
