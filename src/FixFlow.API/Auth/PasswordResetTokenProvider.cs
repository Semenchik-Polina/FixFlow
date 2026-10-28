using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FixFlow.Application.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace FixFlow.API.Auth;

/// <summary>
/// Identity's password reset tokens, on <see cref="IClock"/> (plan T1.4 D2). It works like Identity's
/// <c>DataProtectorTokenProvider</c>, which reads the system clock: the token is a Data Protection payload of when it was
/// made, the user's id, the purpose and the security stamp, encoded as base64url so a link carries it as it is.
/// </summary>
/// <remarks>
/// A token is valid for <see cref="Lifespan"/> (S4), for its own user and purpose, and only while the security stamp is
/// unchanged. A reset or a password change changes the stamp, so a token works once, and every other outstanding token
/// stops working too. Data Protection authenticates the payload, so a token that was changed, cut short or made with
/// another key ring fails like an expired one.
/// </remarks>
internal sealed class PasswordResetTokenProvider(IDataProtectionProvider dataProtection, IClock clock)
    : IUserTwoFactorTokenProvider<User>
{
    /// <summary>The name Identity knows the provider by (<c>IdentityOptions.Tokens</c>).</summary>
    public const string Name = "FixFlowPasswordReset";

    /// <summary>S4: a reset link works for an hour. The email says so.</summary>
    public static readonly Duration Lifespan = Duration.FromHours(1);

    private readonly IDataProtector _protector = dataProtection.CreateProtector("FixFlow.Identity.PasswordResetToken");

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(clock.GetCurrentInstant().ToUnixTimeTicks());
            writer.Write(await manager.GetUserIdAsync(user));
            writer.Write(purpose);
            writer.Write(await manager.GetSecurityStampAsync(user));
        }

        return Base64Url.EncodeToString(_protector.Protect(payload.ToArray()));
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        return Read(token) is { } payload
            && clock.GetCurrentInstant() < payload.Created + Lifespan
            && payload.UserId == await manager.GetUserIdAsync(user)
            && payload.Purpose == purpose
            && payload.SecurityStamp == await manager.GetSecurityStampAsync(user);
    }

    /// <summary>Only for two-factor sign-in, which this provider isn't for.</summary>
    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user) => Task.FromResult(false);

    private Payload? Read(string token)
    {
        try
        {
            using var reader = new BinaryReader(
                new MemoryStream(_protector.Unprotect(Base64Url.DecodeFromChars(token))), Encoding.UTF8);
            var payload = new Payload(
                Instant.FromUnixTimeTicks(reader.ReadInt64()), reader.ReadString(), reader.ReadString(), reader.ReadString());

            return reader.BaseStream.Position == reader.BaseStream.Length ? payload : null;
        }
        // Not base64url, changed or made with another key ring, or a payload that ends too soon
        catch (Exception exception) when (exception is FormatException or CryptographicException or EndOfStreamException)
        {
            return null;
        }
    }

    private sealed record Payload(Instant Created, string UserId, string Purpose, string SecurityStamp);
}
