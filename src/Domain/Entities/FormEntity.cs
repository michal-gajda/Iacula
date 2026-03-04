namespace Iacula.Domain.Entities;

public sealed class FormEntity
{
    public FormId Id { get; private init; }
    public string Payload { get; private set; } = string.Empty;
    public MessageStatus Status { get; private set; } = MessageStatus.Created;
    internal long Version { get; private set; } = 0;

    public FormEntity(FormId id, string payload)
    {
        this.Id = id;
        this.SetPayload(payload);
    }

    internal FormEntity(FormId id, string payload, MessageStatus status, long version)
    {
        this.Id = id;
        this.SetPayload(payload);
        this.Status = status;
        this.Version = version;
    }

    public void SetPayload(string payload)
    {
        this.Payload = payload;
    }

    public void BeginProcessing()
    {
        if (this.Status is not (MessageStatus.Created or MessageStatus.Failed))
        {
            throw new InvalidOperationException($"Cannot begin processing from status {this.Status}");
        }

        this.Status = MessageStatus.InProgress;
    }

    public void MarkAsPublished()
    {
        if (this.Status is not MessageStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot publish message from status {this.Status}");
        }

        this.Status = MessageStatus.Published;
    }

    public void MarkAsFailed()
    {
        if (this.Status is not MessageStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot fail message from status {this.Status}");
        }

        this.Status = MessageStatus.Failed;
    }
}
