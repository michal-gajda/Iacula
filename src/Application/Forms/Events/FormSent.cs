namespace Iacula.Application.Forms.Events;

public sealed record class FormSent : INotification
{
    public required FormId Id { get; init; }
    public required string Payload { get; init; }
}
