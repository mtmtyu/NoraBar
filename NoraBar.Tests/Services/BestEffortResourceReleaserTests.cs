using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class BestEffortResourceReleaserTests
{
    [Fact]
    public void ReleaseAll_ContinuesAfterFailuresAndAggregatesExceptions()
    {
        var firstFailure = new InvalidOperationException("settings");
        var secondFailure = new InvalidOperationException("visibility");
        bool notifyIconDisposed = false;

        AggregateException exception = Assert.Throws<AggregateException>(() =>
            BestEffortResourceReleaser.ReleaseAll(
                () => throw firstFailure,
                () => throw secondFailure,
                () => notifyIconDisposed = true));

        Assert.True(notifyIconDisposed);
        Assert.Equal(new Exception[] { firstFailure, secondFailure }, exception.InnerExceptions);
    }

    [Fact]
    public void ReleaseAll_CompletesWhenEveryOperationSucceeds()
    {
        int calls = 0;

        BestEffortResourceReleaser.ReleaseAll(
            () => calls++,
            () => calls++);

        Assert.Equal(2, calls);
    }
}
