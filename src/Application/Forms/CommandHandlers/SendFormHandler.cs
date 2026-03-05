namespace Iacula.Application.Forms.CommandHandlers;

using Iacula.Application.Forms.Commands;
using Iacula.Application.Forms.Events;
using Iacula.Domain.Entities;
using Iacula.Domain.Interfaces;

internal sealed class SendFormHandler : IRequestHandler<SendForm>
{
    private readonly ILogger<SendFormHandler> logger;
    private readonly IMediator mediator;
    private readonly IFormRepository repository;
    private readonly IUnitOfWork unitOfWork;

    public SendFormHandler(ILogger<SendFormHandler> logger, IMediator mediator, IFormRepository repository, IUnitOfWork unitOfWork)
    {
        this.logger = logger;
        this.mediator = mediator;
        this.repository = repository;
        this.unitOfWork = unitOfWork;
    }

    public async Task Handle(SendForm request, CancellationToken cancellationToken)
    {
        var entity = new FormEntity(request.Id, request.Payload);

        this.logger.LogInformation("Saving form with id {Id}", request.Id.Value);
        await this.unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await this.repository.UpsertAsync(entity, ct);
        }, cancellationToken);
        this.logger.LogInformation("Form with id {Id} saved successfully", request.Id.Value);

        var @event = new FormSent
        {
            Id = request.Id,
            Payload = request.Payload,
        };

        this.logger.LogInformation("Publishing FormSent event for form with id {Id}", request.Id.Value);
        await mediator.Publish(@event, cancellationToken);
    }
}
