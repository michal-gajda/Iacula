namespace Iacula.Infrastructure.EntityFramework.Models;

internal sealed class OutboxDbEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessAfterUtc { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    [Required] public int AttemptCount { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    [Required] public string MessageType { get; set; } = string.Empty;

    [Required] public string PayloadJson { get; set; } = string.Empty;

    public string? LastError { get; set; }
}
