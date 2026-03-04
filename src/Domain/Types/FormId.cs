namespace Iacula.Domain.Types;

public readonly record struct FormId
{
    public Guid Value { get; init; }

    public FormId(Guid value)
    {
        this.Value = value;
    }

    public static implicit operator Guid(FormId formId) => formId.Value;
    public static implicit operator FormId(Guid value) => new(value);
}
