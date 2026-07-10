using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class PlaybackStateCoordinatorTests
{
    [Fact]
    public void ApplyRequestedState_PausesImmediatelyAtTheCurrentPosition()
    {
        var coordinator = new PlaybackStateCoordinator();
        var now = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero);
        bool? notifiedIsPlaying = null;

        coordinator.PlaybackStateChanged += (_, args) => notifiedIsPlaying = args.IsPlaying;

        PlaybackSnapshot beforePause = coordinator.CreateSnapshot(
            reportedIsPlaying: true,
            reportedPosition: TimeSpan.FromSeconds(30),
            lastUpdatedTime: now.AddSeconds(-10),
            endTime: TimeSpan.FromMinutes(3),
            now);
        coordinator.ApplyRequestedState(
            isPlaying: false,
            beforePause.Position,
            endTime: TimeSpan.FromMinutes(3),
            now);

        PlaybackSnapshot whileWindowsIsStale = coordinator.CreateSnapshot(
            reportedIsPlaying: true,
            reportedPosition: TimeSpan.FromSeconds(30),
            lastUpdatedTime: now.AddSeconds(-10),
            endTime: TimeSpan.FromMinutes(3),
            now.AddSeconds(5));

        Assert.False(notifiedIsPlaying);
        Assert.False(whileWindowsIsStale.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(40), whileWindowsIsStale.Position);
    }

    [Fact]
    public void ApplyRequestedState_ResumesImmediatelyFromThePausedPosition()
    {
        var coordinator = new PlaybackStateCoordinator();
        var now = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero);

        coordinator.ApplyRequestedState(
            isPlaying: true,
            position: TimeSpan.FromSeconds(40),
            endTime: TimeSpan.FromMinutes(3),
            now);

        PlaybackSnapshot whileWindowsIsStale = coordinator.CreateSnapshot(
            reportedIsPlaying: false,
            reportedPosition: TimeSpan.FromSeconds(40),
            lastUpdatedTime: now.AddMinutes(-1),
            endTime: TimeSpan.FromMinutes(3),
            now.AddSeconds(5));

        Assert.True(whileWindowsIsStale.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(45), whileWindowsIsStale.Position);
    }
}
