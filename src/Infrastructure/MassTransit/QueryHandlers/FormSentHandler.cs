namespace Iacula.Infrastructure.MassTransit.QueryHandlers;

using global::MassTransit;
using Iacula.Application.Forms.Events;
using Iacula.Domain.Entities;
using Iacula.Domain.Interfaces;
using Iacula.Shared;
using Microsoft.EntityFrameworkCore;

internal sealed class FormSentHandler : INotificationHandler<FormSent>
{
    private readonly ILogger<FormSentHandler> logger;
    private readonly IFormRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ISendEndpointProvider sendEndpointProvider;

    public FormSentHandler(ILogger<FormSentHandler> logger, IFormRepository repository, IUnitOfWork unitOfWork, ISendEndpointProvider sendEndpointProvider)
    {
        this.logger = logger;
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.sendEndpointProvider = sendEndpointProvider;
    }

    public async Task Handle(FormSent notification, CancellationToken cancellationToken)
    {
        var formId = new FormId(notification.Id);
        FormEntity? entity = null;

        try
        {
            await this.unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                entity = await this.repository.LoadAsync(formId, ct);

                if (entity is null)
                {
                    this.logger.LogError("Form with id {FormId} not found", notification.Id.Value);
                    return;
                }

                if (entity.Status is MessageStatus.Published)
                {
                    this.logger.LogInformation("Form with id {FormId} already published", notification.Id.Value);
                    entity = null;
                    return;
                }

                if (entity.Status is MessageStatus.Created or MessageStatus.Failed)
                {
                    entity.BeginProcessing();
                    await this.repository.UpsertAsync(entity, ct);
                }
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            this.logger.LogWarning(exception, "Concurrency conflict for FormId {FormId}", notification.Id.Value);
            return;
        }

        if (entity is null)
        {
            return;
        }

        var published = false;

        try
        {
            var endpoint = await this.sendEndpointProvider.GetSendEndpoint(new Uri("queue:send-form"));
            await endpoint.Send(new SendForm { Id = notification.Id.Value, Payload = notification.Payload }, cancellationToken);
            published = true;
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Failed to publish form {FormId} to queue", notification.Id.Value);
        }

        await this.unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var current = await this.repository.LoadAsync(formId, ct);
            if (current is null) return;

            if (published)
                current.MarkAsPublished();
            else
                current.MarkAsFailed();

            await this.repository.UpsertAsync(current, ct);
        }, cancellationToken);
    }
}
