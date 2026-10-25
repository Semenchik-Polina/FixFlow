using FixFlow.Application.Auth;
using FixFlow.Application.Errors;
using FixFlow.Application.Organizations;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using FixFlow.Domain.Organizations;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.SignUp;

/// <summary>
/// Creates the account, the organization and the founder's Admin membership in one transaction, then signs the new
/// user in. The organization context comes from a trusted scope (plan P25) that opens before anything is saved and
/// closes after the commit, so the post-commit handlers run inside it.
/// </summary>
internal sealed partial class SignUpHandler(
    IFixFlowDbContext db,
    UserManager<User> users,
    OrganizationCreator creator,
    ICurrentUser currentUser,
    ITrustedOrganizationScope trustedScope,
    IAuthSession session,
    ILogger<SignUpHandler> logger) : IRequestHandler<SignUpCommand, Result<OrganizationCreated>>
{
    public async Task<Result<OrganizationCreated>> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        // The audit stamps would credit the signed-in user with someone else's account, organization and membership
        if (currentUser.UserId is not null)
        {
            return Result.Unprocessable(
                "You're already signed in. Sign out to create a new account, or create another organization from your account.");
        }

        var organization = Organization.Create(command.OrganizationName, command.Slug);

        // Fails fast, before hashing the password and inserting the account; the unique index still catches a race
        if (await db.Organizations.AnyAsync(o => o.Slug == organization.Slug, cancellationToken))
        {
            return OrganizationCreator.SlugTaken();
        }

        // Trimmed like the validator checked it, so a stray space can't make a second account
        var user = new User(command.Email.Trim(), command.FirstName, command.LastName);

        // The organization's id exists before the insert (D10), so the scope can open before anything is saved
        using (trustedScope.Open(organization.Id))
        {
            // UserManager saves the whole context, so one transaction covers its save and the creator's
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var account = await CreateAccountAsync(user, command.Password);
            if (!account.IsSuccess)
            {
                return account.Error;
            }

            // On a failure the transaction isn't committed: disposing it rolls the account back
            var created = await creator.CreateAsync(organization, user.Id, cancellationToken);
            if (!created.IsSuccess)
            {
                return created.Error;
            }

            // The post-commit events run here, still inside the scope
            await transaction.CommitAsync(cancellationToken);
        }

        await session.SignInAsync(user, isPersistent: false);
        LogSignedUp(user.Id, organization.Id);

        return OrganizationCreated.From(organization);
    }

    private async Task<Result> CreateAccountAsync(User user, string password)
    {
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, password);
        }
        // UserManager's own check lost a race with another signup, and the unique index caught it
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueIndexes.UserEmail)
        {
            return EmailTaken();
        }

        if (result.Succeeded)
        {
            return Result.Success();
        }

        // Codes only: the description of DuplicateEmail holds the address (S26)
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return EmailTaken();
        }

        // The validator mirrors Identity's rules, so anything else is a bug
        throw new InvalidOperationException(
            $"Creating the account failed: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
    }

    private static ResultError EmailTaken() =>
        Result.FieldError(nameof(SignUpCommand.Email), "An account with this email already exists.");

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed up with organization {OrganizationId}")]
    private partial void LogSignedUp(Guid userId, Guid organizationId);
}
