using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class MediaUpdateRequestTrackerTests
{
    [Fact]
    public void IsCurrent_RejectsRequestCapturedBeforeSessionChange()
    {
        var tracker = new MediaUpdateRequestTracker();
        MediaUpdateRequest firstRequest = tracker.Capture();

        tracker.InvalidatePendingRequests();
        MediaUpdateRequest secondRequest = tracker.Capture();

        Assert.False(tracker.IsCurrent(firstRequest));
        Assert.True(tracker.IsCurrent(secondRequest));
    }
}
