namespace Iacula.Infrastructure.EntityFramework.Models;

internal sealed record class FormDbEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public string Payload { get; set; } = string.Empty;
    [Required] public int Status { get; set; } = 1;
    [Required] public int AttemptCount { get; set; } = 0;
    [ConcurrencyCheck] public long Version { get; set; }
}
