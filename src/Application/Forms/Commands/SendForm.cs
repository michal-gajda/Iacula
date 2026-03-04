namespace Iacula.Application.Forms.Commands;

public sealed record class SendForm : IRequest
{
    public required FormId Id { get; init; }
    public required string Payload { get; init; }
}
