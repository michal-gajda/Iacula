namespace Iacula.WebApi.Controllers;

using Iacula.Application.Forms.Commands;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/forms")]
public sealed class FormsController : ControllerBase
{
    private readonly IMediator mediator;

    public FormsController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost("send")]
    public async Task<ActionResult<SendFormResponse>> SendAsync([FromBody] SendFormRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            return this.BadRequest("Payload cannot be empty.");
        }

        var formId = new FormId(Guid.NewGuid());

        var command = new SendForm
        {
            Id = formId,
            Payload = request.Payload,
        };

        await this.mediator.Send(command, cancellationToken);

        return this.Ok(new SendFormResponse
        {
            Id = formId.Value,
        });
    }
}

public sealed class SendFormRequest
{
    public required string Payload { get; init; }
}

public sealed class SendFormResponse
{
    public required Guid Id { get; init; }
}