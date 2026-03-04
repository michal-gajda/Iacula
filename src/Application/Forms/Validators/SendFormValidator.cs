namespace Iacula.Application.Forms.Validators;

using FluentValidation;
using Iacula.Application.Forms.Commands;
using Iacula.Domain.Interfaces;

internal sealed class SendFormValidator : AbstractValidator<SendForm>
{
    private readonly IFormRepository repository;

    public SendFormValidator(IFormRepository repository)
    {
        this.repository = repository;

        base.RuleFor(command => command.Id)
            .NotEmpty()
            .MustAsync(this.BeUniqueAsync)
            .WithMessage("Form with the same id already exists.")
            ;
    }

    private async Task<bool> BeUniqueAsync(FormId id, CancellationToken cancellationToken)
    {
        return await this.repository.LoadAsync(id, cancellationToken) is null;
    }
}
