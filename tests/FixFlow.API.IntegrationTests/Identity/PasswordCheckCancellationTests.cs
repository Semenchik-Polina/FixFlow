using System.Diagnostics;
using System.Net.Http.Json;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Identity;

/// <summary>
/// A password check waits for the account's row lock while another attempt holds it (S3). A caller who leaves ends
/// that wait, so their request doesn't keep a connection open behind the attempt ahead.
/// </summary>
public class PasswordCheckCancellationTests(FixFlowApiFactory api)
{
    private const string WrongPassword = "not the password";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public enum PasswordCheck
    {
        LogIn,
        ChangePassword,
    }

    [Theory]
    [InlineData(PasswordCheck.LogIn)]
    [InlineData(PasswordCheck.ChangePassword)]
    public async Task PasswordCheck_CallerLeavesWhileTheAccountIsLocked_StopsWaiting(PasswordCheck check)
    {
        using var client = api.CreateApiClient();
        var account = await client.SignUpAsync();
        var userId = (await api.FindUserAsync(account.Email))!.Id;
        // The attempt ahead: the account's row locked, as SignInManagerAuthSession locks it
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FixFlowDbContext>();
        await using var attemptAhead = await db.Database.BeginTransactionAsync(_ct);
        var holder = await db.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(_ct);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.users WHERE id = {userId} FOR UPDATE", _ct);
        using var leaves = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var request = Send(client, check, account, leaves.Token);
        await WaitUntilAsync(async () => await WaitingBehindAsync(holder) == 1);

        await leaves.CancelAsync();

        // Before the request is awaited: TestServer completes it only once the app is done, which without the
        // cancellation would be the command timeout (30 s), and the wait would look ended
        await WaitUntilAsync(async () => await WaitingBehindAsync(holder) == 0);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client, PasswordCheck check, SignedUp account, CancellationToken cancellationToken) => check switch
        {
            PasswordCheck.LogIn => client.PostAsJsonAsync(
                IdentityClientExtensions.LogInPath,
                new { email = account.Email, password = WrongPassword, rememberMe = false },
                cancellationToken),
            PasswordCheck.ChangePassword => client.PostAsJsonAsync(
                IdentityClientExtensions.ChangePasswordPath,
                new { currentPassword = WrongPassword, newPassword = "a brand new passphrase" },
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(check), check, null),
        };

    /// <summary>How many database sessions wait for a lock that <paramref name="holder"/> holds.</summary>
    private async Task<int> WaitingBehindAsync(int holder)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FixFlowDbContext>().Database
            .SqlQuery<int>(
                $"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))")
            .SingleAsync(_ct);
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var waited = Stopwatch.StartNew();
        while (!await condition())
        {
            Assert.True(waited.Elapsed < _timeout, $"Not true within {_timeout.TotalSeconds} s.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);
        }
    }
}
