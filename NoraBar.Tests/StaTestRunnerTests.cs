using Xunit;

namespace NoraBar.Tests;

public sealed class StaTestRunnerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMilliseconds(100);

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
                TestTimeout));

        Assert.True(actionStarted.IsSet);
        Assert.True(actionExited.IsSet);
        Assert.Contains("STA test action", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WhenActionWaitsForExternalSignal_TimeoutCleanupReleasesAndJoinsAction()
    {
        using var actionStarted = new ManualResetEventSlim();
        using var releaseAction = new ManualResetEventSlim();
        using var actionExited = new ManualResetEventSlim();

        Assert.Throws<TimeoutException>(() =>
            StaTestRunner.Run(
                _ =>
                {
                    actionStarted.Set();
                    releaseAction.Wait();
                    actionExited.Set();
                },
                TestTimeout,
                releaseAction.Set));

        Assert.True(actionStarted.IsSet);
        Assert.True(actionExited.IsSet);
    }

    [Fact]
    public void Run_WhenTimeoutCleanupFails_PreservesTimeoutFirstAndReportsCleanupFailure()
    {
        using var actionStarted = new ManualResetEventSlim();
        using var cancellationObserved = new ManualResetEventSlim();
        var cleanupFailure = new InvalidOperationException("cleanup");

        AggregateException exception = Assert.Throws<AggregateException>(() =>
            StaTestRunner.Run(
                cancellationToken =>
                {
                    actionStarted.Set();
                    cancellationToken.WaitHandle.WaitOne();
                    cancellationObserved.Set();
                },
                TestTimeout,
                () => throw cleanupFailure));

        Assert.True(actionStarted.IsSet);
        Assert.True(cancellationObserved.IsSet);
        Assert.IsType<TimeoutException>(exception.InnerExceptions[0]);
        Assert.Same(cleanupFailure, exception.InnerExceptions[1]);
    }

    [Fact]
    public void Run_WhenActionInitiallyIgnoresCancellation_CleanupCanReleaseDelayedExit()
    {
        using var actionStarted = new ManualResetEventSlim();
        using var releaseAction = new ManualResetEventSlim();
        using var actionExited = new ManualResetEventSlim();

        Assert.Throws<TimeoutException>(() =>
            StaTestRunner.Run(
                _ =>
                {
                    actionStarted.Set();
                    releaseAction.Wait();
                    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                    actionExited.Set();
                },
                TestTimeout,
                releaseAction.Set));

        Assert.True(actionStarted.IsSet);
        Assert.True(actionExited.IsSet);
    }
}
