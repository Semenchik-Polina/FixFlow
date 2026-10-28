using FixFlow.Application.Errors;
using Microsoft.AspNetCore.Identity;

namespace FixFlow.Application.Identity;

/// <summary>
/// Reads an <see cref="IdentityResult"/> by its error codes only: some descriptions quote the email (S26).
/// </summary>
internal static class IdentityResultExtensions
{
    public static bool HasError(this IdentityResult result, string code) =>
        result.Errors.Any(error => error.Code == code);

    /// <summary>
    /// For an update the validators can't fail: a lost race on the user's concurrency stamp becomes a 409 (plan P11);
    /// anything else is a bug.
    /// </summary>
    /// <param name="action">What failed, for the exception's message, e.g. "Changing the name".</param>
    public static void ThrowIfFailed(this IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.HasError(nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            throw new ConcurrencyConflictException();
        }

        throw result.Unexpected(action);
    }

    /// <summary>An error the validators should have prevented, named by its codes.</summary>
    public static InvalidOperationException Unexpected(this IdentityResult result, string action) =>
        new($"{action} failed: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
}
