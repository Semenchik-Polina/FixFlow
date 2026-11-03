using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Identity;
using FixFlow.Infrastructure.Identity;
using FixFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>The account rules of <see cref="FixFlowIdentityExtensions.AddFixFlowIdentity"/>, as the app has them.</summary>
public class UserAccountTests(FixFlowApiFactory api)
{
    private const string WrongPassword = "not the password";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateUser_AssignsUuidV7Id()
    {
        var user = NewUser();

        var result = await CreateAsync(user);

        Assert.True(result.Succeeded);
        Assert.Equal(7, user.Id.Version);
        Assert.Equal(user.Id.ToString(), user.UserName);
        await using var scope = api.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(user.Email!);
        Assert.Equal(user.Id, saved?.Id);
    }

    [Fact]
    public async Task CreateUser_PasswordShorterThan10_Fails()
    {
        var result = await CreateAsync(NewUser(), "ninechars");

        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.PasswordTooShort));
    }

    [Fact]
    public async Task CreateUser_TenLowercaseLetters_Succeeds()
    {
        // Length is the only rule (S3): no digits, capitals or symbols required
        var result = await CreateAsync(NewUser(), "abcdefghij");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateUser_EmailTakenInAnotherCase_Fails()
    {
        var email = TestData.Email();
        await CreateAsync(NewUser(email));

        var result = await CreateAsync(NewUser(email.ToUpperInvariant()));

        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
    }

    [Fact]
    public async Task InsertUser_EmailTaken_ViolatesUniqueIndex()
    {
        // UserManager's check can lose a race; the index can't
        var email = TestData.Email();
        await CreateAsync(NewUser(email));
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FixFlowDbContext>();
        db.Users.Add(new User(email, "Second", "Account") { NormalizedEmail = email.ToUpperInvariant() });

        await DatabaseAssertions.AssertUniqueViolationAsync("ix_users_normalized_email", () => db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task CheckPassword_FiveFailures_LocksOut()
    {
        var user = NewUser();
        await CreateAsync(user);
        await using var scope = api.Services.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var saved = (await signIn.UserManager.FindByIdAsync(user.Id.ToString()))!;

        for (var attempt = 1; attempt < FixFlowIdentityExtensions.MaxFailedSignIns; attempt++)
        {
            var failed = await signIn.CheckPasswordSignInAsync(saved, WrongPassword, lockoutOnFailure: true);
            Assert.False(failed.IsLockedOut);
        }

        var last = await signIn.CheckPasswordSignInAsync(saved, WrongPassword, lockoutOnFailure: true);
        var rightPassword = await signIn.CheckPasswordSignInAsync(saved, TestData.Password, lockoutOnFailure: true);

        Assert.True(last.IsLockedOut);
        Assert.True(rightPassword.IsLockedOut);
    }

    [Fact]
    public async Task UpdateUser_Concurrently_ReturnsConcurrencyFailure()
    {
        var user = NewUser();
        await CreateAsync(user);
        await using var first = api.Services.CreateAsyncScope();
        await using var second = api.Services.CreateAsyncScope();
        var firstUsers = first.ServiceProvider.GetRequiredService<UserManager<User>>();
        var secondUsers = second.ServiceProvider.GetRequiredService<UserManager<User>>();
        var firstCopy = (await firstUsers.FindByIdAsync(user.Id.ToString()))!;
        var secondCopy = (await secondUsers.FindByIdAsync(user.Id.ToString()))!;

        firstCopy.ChangeName("First", "Writer");
        Assert.True((await firstUsers.UpdateAsync(firstCopy)).Succeeded);
        secondCopy.ChangeName("Second", "Writer");
        var result = await secondUsers.UpdateAsync(secondCopy);

        // Identity's own answer, not ConcurrencyConflictException: FixFlowDbContext lets EF Core's exception through
        // for users, which is the only one Identity's store recognizes
        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure));
    }

    private static User NewUser(string? email = null) => new(email ?? TestData.Email(), "Test", "User");

    private async Task<IdentityResult> CreateAsync(User user, string password = TestData.Password)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<User>>().CreateAsync(user, password);
    }
}
