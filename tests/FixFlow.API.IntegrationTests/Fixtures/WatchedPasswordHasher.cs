using FixFlow.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Identity's password hasher with its default options, as the app has it, watched: it counts verifications, and can
/// run an action once, just before its next hash. A password reset or change hashes the new password after it loaded
/// the user and before it saves, so that action is a save to the account that comes first. Installed on a host of its
/// own (<see cref="WatchedPasswordHasherExtensions.WithPasswordHasher"/>).
/// </summary>
public sealed class WatchedPasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _hasher = new();
    private int _verifications;
    private Action<User>? _beforeNextHash;

    /// <summary>How many passwords the host checked against a stored hash.</summary>
    public int Verifications => Volatile.Read(ref _verifications);

    /// <summary>Runs <paramref name="action"/> once, with the user, just before the next hash.</summary>
    public void BeforeNextHash(Action<User> action) => Volatile.Write(ref _beforeNextHash, action);

    public string HashPassword(User user, string password)
    {
        Interlocked.Exchange(ref _beforeNextHash, null)?.Invoke(user);
        return _hasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        Interlocked.Increment(ref _verifications);
        return _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

public static class WatchedPasswordHasherExtensions
{
    /// <summary>A host of its own, on the run's containers and mail sink, that hashes with <paramref name="hasher"/>.</summary>
    public static WebApplicationFactory<Program> WithPasswordHasher(
        this FixFlowApiFactory api, WatchedPasswordHasher hasher) =>
        api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordHasher<User>>();
            services.AddSingleton<IPasswordHasher<User>>(hasher);
        }));
}
