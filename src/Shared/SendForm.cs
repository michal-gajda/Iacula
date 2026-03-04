namespace Iacula.Shared;

public sealed class SendForm
{
    public required Guid Id { get; init; }
    public required string Payload { get; init; }
}
