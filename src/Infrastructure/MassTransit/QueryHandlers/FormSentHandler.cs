namespace Iacula.Infrastructure.MassTransit.QueryHandlers;

using System.Text.Json;
using Iacula.Application.Forms.Events;
using Iacula.Domain.Interfaces;
using Iacula.Infrastructure.EntityFramework.Interfaces;
using Iacula.Infrastructure.EntityFramework.Models;
using Iacula.Shared;
using Microsoft.EntityFrameworkCore;

internal sealed class FormSentHandler : INotificationHandler<FormSent>
{
    private readonly ILogger<FormSentHandler> logger;
    private readonly IFormRepository repository;
    private readonly IOutboxRepository outboxRepository;
    private readonly IUnitOfWork unitOfWork;

    public FormSentHandler(ILogger<FormSentHandler> logger, IFormRepository repository, IOutboxRepository outboxRepository, IUnitOfWork unitOfWork)
    {
        this.logger = logger;
        this.repository = repository;
        this.outboxRepository = outboxRepository;
        this.unitOfWork = unitOfWork;
    }

    public async Task Handle(FormSent notification, CancellationToken cancellationToken)
    {
        var formId = new FormId(notification.Id);

        try
        {
            await this.unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var entity = await this.repository.LoadAsync(formId, ct);

                if (entity is null)
                {
                    this.logger.LogError("Form with id {FormId} not found", notification.Id.Value);

                    return;
                }

                if (entity.Status is MessageStatus.Published)
                {
                    this.logger.LogInformation("Form with id {FormId} already published", notification.Id.Value);

                    return;
                }

                if (entity.Status is MessageStatus.Created or MessageStatus.Failed)
                {
                    entity.BeginProcessing();
                    await this.repository.UpsertAsync(entity, ct);
                }

                var message = new SendForm
                {
                    Id = notification.Id,
                    Payload = notification.Payload,
                };

                var outboxItem = new OutboxDbEntity
                {
                    Id = Guid.NewGuid(),
                    CreatedAtUtc = DateTime.UtcNow,
                    AttemptCount = 0,
                    MessageType = typeof(SendForm).AssemblyQualifiedName!,
                    PayloadJson = JsonSerializer.Serialize(message),
                };

                this.outboxRepository.Add(outboxItem);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(exception, "Concurrency conflict while enqueuing outbox for FormId {FormId}", notification.Id.Value);
        }
    }
}
