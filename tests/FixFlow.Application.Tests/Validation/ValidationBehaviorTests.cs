using FixFlow.Application.Validation;
using FluentValidation;
using MediatR;

namespace FixFlow.Application.Tests.Validation;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_InvalidRequest_ThrowsFailuresOfAllValidatorsWithoutCallingHandler()
    {
        var behavior = new ValidationBehavior<Ping, string>([new NotEmptyValidator(), new MinimumLengthValidator()]);
        var handled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Ping(""),
            _ =>
            {
                handled = true;
                return Task.FromResult("pong");
            },
            TestContext.Current.CancellationToken));

        Assert.False(handled);
        Assert.Equal(2, exception.Errors.Count());
        Assert.All(exception.Errors, failure => Assert.Equal(nameof(Ping.Name), failure.PropertyName));
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsHandler()
    {
        var behavior = new ValidationBehavior<Ping, string>([new NotEmptyValidator(), new MinimumLengthValidator()]);

        var response = await behavior.Handle(
            new Ping("valid"), _ => Task.FromResult("pong"), TestContext.Current.CancellationToken);

        Assert.Equal("pong", response);
    }

    [Fact]
    public async Task Handle_NoValidators_CallsHandler()
    {
        var behavior = new ValidationBehavior<Ping, string>([]);

        var response = await behavior.Handle(new Ping(""), _ => Task.FromResult("pong"), TestContext.Current.CancellationToken);

        Assert.Equal("pong", response);
    }

    private sealed record Ping(string Name) : IRequest<string>;

    private sealed class NotEmptyValidator : AbstractValidator<Ping>
    {
        public NotEmptyValidator() => RuleFor(ping => ping.Name).NotEmpty();
    }

    private sealed class MinimumLengthValidator : AbstractValidator<Ping>
    {
        public MinimumLengthValidator() => RuleFor(ping => ping.Name).MinimumLength(3);
    }
}
