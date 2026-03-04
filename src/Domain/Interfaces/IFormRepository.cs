namespace Iacula.Domain.Interfaces;

using Iacula.Domain.Entities;

public interface IFormRepository
{
    Task<FormEntity?> LoadAsync(FormId id, CancellationToken cancellationToken = default);
    Task UpsertAsync(FormEntity entity, CancellationToken cancellationToken = default);
}
