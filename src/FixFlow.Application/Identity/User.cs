using System.Diagnostics.CodeAnalysis;
using FixFlow.Domain;
using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace FixFlow.Application.Identity;

/// <summary>
/// A person's one global account (D4): sign-in email, password and name. It isn't organization-owned; access to an
/// organization is an <see cref="Domain.Organizations.OrganizationMembership"/>. It derives from Identity's user, so
/// it can't live in Domain; here, handlers can still name it in their projections (plan P22).
/// <c>IFixFlowDbContext</c> has no unfiltered set of users: a handler reaches other users only through queries
/// scoped to the organization.
/// </summary>
/// <remarks>
/// <para>
/// Create accounts with <c>UserManager.CreateAsync</c>, never by adding a <see cref="User"/> to the context (seeds
/// included). The manager sets what the constructor can't: the normalized username and email (which lookups and the
/// unique indexes use), the password hash, the security stamp and the lockout.
/// </para>
/// <para>
/// The username is the id, not the email (plan P22). Identity puts the username into the principal's name claim,
/// where an email could reach the logs (S26). Sign-in finds the user by email.
/// </para>
/// </remarks>
public sealed class User : IdentityUser<Guid>, IAuditable
{
    public const int NameMaxLength = 100;

    /// <param name="email">The sign-in email, unique across all users. The caller has validated it.</param>
    /// <param name="firstName">Trimmed; the caller has validated it.</param>
    /// <param name="lastName">Trimmed; the caller has validated it.</param>
    public User(string email, string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Assigned when the object is created, like an Entity's (D10), so it's known before the insert
        Id = Guid.CreateVersion7();
        UserName = Id.ToString();
        Email = email;
        ChangeName(firstName, lastName);
    }

    // For EF Core
    private User()
    {
        FirstName = "";
        LastName = "";
    }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public Instant CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public Instant? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <param name="firstName">Trimmed; the caller has validated it.</param>
    /// <param name="lastName">Trimmed; the caller has validated it.</param>
    [MemberNotNull(nameof(FirstName), nameof(LastName))]
    public void ChangeName(string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }
}
