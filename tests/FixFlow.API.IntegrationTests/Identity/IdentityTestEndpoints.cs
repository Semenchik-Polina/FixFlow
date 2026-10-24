using FixFlow.API.Endpoints;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Auth;
using FixFlow.Application.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// Test-only endpoints (registered by <see cref="FixFlowApiFactory"/>) that sign a new account in through the app's
/// Identity setup and read the session back, until T1.3 adds signup and login.
/// </summary>
public sealed class IdentityTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-identity";

    /// <summary>Meets the password policy: long enough, nothing else required.</summary>
    public const string Password = "correct horse battery";

    public string Prefix => "/test-identity";

    public void Map(RouteGroupBuilder group)
    {
        // Anonymous, or the fallback policy would answer 401 before anyone could sign in
        group.MapPost("/sign-in", async (UserManager<User> users, SignInManager<User> signIn) =>
            {
                var user = new User(TestData.Email(), "Test", "User");
                var created = await users.CreateAsync(user, Password);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Code)));
                }

                await signIn.SignInAsync(user, isPersistent: false);
                return TypedResults.Ok(new SignedIn(user.Id, user.Email!));
            })
            .AllowAnonymous();

        group.MapGet("/session", (ICurrentUser currentUser, HttpContext context) => TypedResults.Ok(
            new Session(currentUser.UserId!.Value, [.. context.User.Claims.Select(claim => claim.Value)])));
    }
}

public sealed record SignedIn(Guid UserId, string Email);

/// <param name="ClaimValues">Every claim of the request's principal, to check that none is personal data.</param>
public sealed record Session(Guid UserId, string[] ClaimValues);
