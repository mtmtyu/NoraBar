using Xunit;

namespace NoraBar.Tests;

public sealed class StaTestRunnerTests
{
    [Fact]
    public void Run_WhenActionTimesOut_CancelsAndJoinsActionBeforeReturning()
    {
        using var actionStarted = new ManualResetEventSlim();
        using var actionExited = new ManualResetEventSlim();

        TimeoutException exception = Assert.Throws<TimeoutException>(() =>
            StaTestRunner.Run(
                cancellationToken =>
                {
                    actionStarted.Set();
                    cancellationToken.WaitHandle.WaitOne();
                    actionExited.Set();
                },
                TimeSpan.FromMilliseconds(100)));

        Assert.True(actionStarted.IsSet);
        Assert.True(actionExited.IsSet);
        Assert.Contains("STA test action", exception.Message, StringComparison.Ordinal);
    }
}
