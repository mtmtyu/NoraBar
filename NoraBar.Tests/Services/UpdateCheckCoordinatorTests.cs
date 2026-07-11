using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class UpdateCheckCoordinatorTests
{
    [Fact]
    public async Task CheckAsync_SharesAnInFlightRequest()
    {
        var releaseSource = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int requestCount = 0;
        var coordinator = new UpdateCheckCoordinator(() =>
        {
            requestCount++;
            return releaseSource.Task;
        });

        Task<UpdateCheckResult> startupCheck = coordinator.CheckAsync();
        Task<UpdateCheckResult> manualCheck = coordinator.CheckAsync();
        var expected = new UpdateCheckResult("v2.0.0", "https://example.com/release");
        releaseSource.SetResult(expected);

        UpdateCheckResult[] results = await Task.WhenAll(startupCheck, manualCheck);

        Assert.Equal(1, requestCount);
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    [Fact]
    public async Task CheckAsync_StartsANewRequestAfterThePreviousRequestCompletes()
    {
        int requestCount = 0;
        var coordinator = new UpdateCheckCoordinator(() =>
        {
            requestCount++;
            return Task.FromResult(new UpdateCheckResult(null, null));
        });

        await coordinator.CheckAsync();
        await coordinator.CheckAsync();

        Assert.Equal(2, requestCount);
    }
}
