using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class VisualizerRestartCoordinatorTests
{
    [Fact]
    public async Task RestartAsync_SerializesConcurrentRestarts()
    {
        var coordinator = new VisualizerRestartCoordinator();
        var firstRestartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRestart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int runningCount = 0;
        int maximumRunningCount = 0;

        async Task<bool> RestartAsync()
        {
            int currentRunningCount = Interlocked.Increment(ref runningCount);
            maximumRunningCount = Math.Max(maximumRunningCount, currentRunningCount);
            firstRestartEntered.TrySetResult();
            await releaseFirstRestart.Task;
            Interlocked.Decrement(ref runningCount);
            return true;
        }

        Task<bool> first = coordinator.RestartAsync(RestartAsync);
        await firstRestartEntered.Task;
        Task<bool> second = coordinator.RestartAsync(RestartAsync);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(1, runningCount);

        releaseFirstRestart.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumRunningCount);
    }
}
