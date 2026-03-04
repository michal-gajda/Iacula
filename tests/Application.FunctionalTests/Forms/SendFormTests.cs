namespace Iacula.Application.FunctionalTests.Forms;

using FluentValidation;
using Iacula.Application.Forms.Commands;
using Iacula.Domain.Interfaces;

[TestClass]
public class SendFormTests : TestBase
{
    [TestMethod]
    public async Task CreateMessage_Should_CreateMessage()
    {
        var formId = new FormId(Guid.NewGuid());

        var command = new SendForm
        {
            Id = formId,
            Payload = $"{formId.Value}",
        };

        await using var scope = this.Provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, CancellationToken.None);

        var repository = scope.ServiceProvider.GetRequiredService<IFormRepository>();

        var sut = await repository.LoadAsync(formId, CancellationToken.None);
        sut.ShouldNotBeNull();
        sut.Id.ShouldBe(formId);
        sut.Payload.ShouldBe(command.Payload);
        sut.Status.ShouldBe(MessageStatus.Created);
    }

    [TestMethod]
    public async Task DoubleCreateMessage_Should_Throw_ValidationException()
    {
        var id = Guid.NewGuid();
        var formId = new FormId(id);

        var command = new SendForm
        {
            Id = formId,
            Payload = $"{formId.Value}",
        };

        await using var scope = this.Provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, CancellationToken.None);

        var sut = () => mediator.Send(command, CancellationToken.None);

        sut.ShouldThrow<ValidationException>();
    }
}
