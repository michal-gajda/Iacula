namespace Iacula.Infrastructure.EntityFramework.Services;

using AutoMapper;
using Iacula.Domain.Entities;
using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class FormRepository : IFormRepository
{
    private readonly IaculaDbContext dbContext;
    private readonly ILogger<FormRepository> logger;
    private readonly IMapper mapper;

    public FormRepository(IaculaDbContext dbContext, ILogger<FormRepository> logger, IMapper mapper)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.mapper = mapper;
    }

    public async Task<FormEntity?> LoadAsync(FormId id, CancellationToken cancellationToken = default)
    {
        var dbEntity = await this.dbContext.Forms
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken);

        return this.mapper.Map<FormEntity?>(dbEntity);
    }

    public async Task UpsertAsync(FormEntity entity, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Upserting form with id {FormId}", entity.Id.Value);

        var existingDbEntity = await this.dbContext.Forms
            .SingleOrDefaultAsync(item => item.Id == entity.Id.Value, cancellationToken);

        if (existingDbEntity is null)
        {
            var newDbEntity = this.mapper.Map<FormDbEntity>(entity);
            newDbEntity.Version = 1;
            this.dbContext.Forms.Add(newDbEntity);

            return;
        }

        this.dbContext.Entry(existingDbEntity)
            .Property(item => item.Version)
            .OriginalValue = entity.Version;

        this.mapper.Map(entity, existingDbEntity);

        existingDbEntity.Version = entity.Version + 1;
    }
}
