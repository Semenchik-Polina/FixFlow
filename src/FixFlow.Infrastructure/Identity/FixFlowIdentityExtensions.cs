using FixFlow.Application.Identity;
using FixFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Infrastructure.Identity;

public static class FixFlowIdentityExtensions
{
    /// <summary>The lockout of S3: after this many failed sign-ins in a row.</summary>
    public const int MaxFailedSignIns = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Identity's user services on <see cref="FixFlowDbContext"/>, with the account rules:
    /// <list type="bullet">
    ///   <item>S3's password length (<see cref="PasswordPolicy"/>, no composition rules) and lockout;</item>
    ///   <item>one account per email (D5);</item>
    ///   <item>no email confirmation before Phase 2 (P3).</item>
    /// </list>
    /// The username is the user's id (plan P22), which Identity's default username characters already allow, and the
    /// signed-in principal carries ids only (<see cref="IdsOnlyClaimsPrincipalFactory"/>). The API adds the sign-in
    /// manager and the cookie on top.
    /// </summary>
    public static IdentityBuilder AddFixFlowIdentity(this IServiceCollection services) =>
        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<FixFlowDbContext>()
            .AddClaimsPrincipalFactory<IdsOnlyClaimsPrincipalFactory>();
}
