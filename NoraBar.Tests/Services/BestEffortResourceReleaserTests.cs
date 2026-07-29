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

    [Fact]
    public void ReleaseAllAndReport_WhenCleanupFails_CompletesBoundaryActionAndDoesNotThrow()
    {
        var failure = new InvalidOperationException("preview");
        Exception? reported = null;
        bool hidden = false;

        BestEffortResourceReleaser.ReleaseAllAndReport(
            exception => reported = exception,
            () => throw failure,
            () => hidden = true);

        Assert.True(hidden);
        AggregateException aggregate = Assert.IsType<AggregateException>(reported);
        Assert.Same(failure, Assert.Single(aggregate.InnerExceptions));
    }

    [Fact]
    public void ReleaseAllAndReport_WhenReporterFails_DoesNotEscapeUiBoundary()
    {
        bool reporterCalled = false;

        BestEffortResourceReleaser.ReleaseAllAndReport(
            _ =>
            {
                reporterCalled = true;
                throw new InvalidOperationException("trace");
            },
            () => throw new InvalidOperationException("cleanup"));

        Assert.True(reporterCalled);
    }
}
