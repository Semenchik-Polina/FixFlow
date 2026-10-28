using FixFlow.Application.Identity.ResetPassword;

namespace FixFlow.Application.Tests.Identity;

public class PasswordResetCodeTests
{
    // Base64url, like Identity's token: letters, digits, '-' and '_'
    private const string ProviderToken = "reset-code_from-identity";

    [Fact]
    public void Format_ThenTryParse_GivesBackTheIdAndToken()
    {
        var userId = Guid.CreateVersion7();

        var code = PasswordResetCode.Format(userId, ProviderToken);

        Assert.True(PasswordResetCode.TryParse(code, out var parsedId, out var parsedToken));
        Assert.Equal(userId, parsedId);
        Assert.Equal(ProviderToken, parsedToken);
    }

    [Fact]
    public void Format_IsUrlSafe()
    {
        var code = PasswordResetCode.Format(Guid.CreateVersion7(), ProviderToken);

        Assert.Equal(code, Uri.EscapeDataString(code));
        // The id as 32 hex digits, then the separator
        Assert.Equal(32, code.IndexOf('.', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("not-a-guid.reset_code")]
    // A guid in another format than "N"
    [InlineData("0192e0d4-7b6a-7c3e-9a1b-2c3d4e5f6a7b.reset_code")]
    // An id with nothing after the separator
    [InlineData("0192e0d47b6a7c3e9a1b2c3d4e5f6a7b.")]
    [InlineData(".reset_code")]
    public void TryParse_Malformed_ReturnsFalse(string? code)
    {
        Assert.False(PasswordResetCode.TryParse(code, out var userId, out var token));
        Assert.Equal(Guid.Empty, userId);
        Assert.Null(token);
    }
}
