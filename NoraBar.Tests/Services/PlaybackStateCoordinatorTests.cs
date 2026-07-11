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
            now.AddSeconds(1));

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
            now.AddSeconds(1));

        Assert.True(whileWindowsIsStale.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(41), whileWindowsIsStale.Position);
    }

    [Fact]
    public void CreateSnapshot_FallsBackToReportedStateAfterRequestTimeout()
    {
        var coordinator = new PlaybackStateCoordinator();
        var now = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero);
        coordinator.ApplyRequestedState(true, TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(3), now);
        PlaybackSnapshot snapshot = coordinator.CreateSnapshot(
            false, TimeSpan.FromSeconds(40), now, TimeSpan.FromMinutes(3), now.AddSeconds(2));
        Assert.False(snapshot.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(40), snapshot.Position);
    }

    [Fact]
    public void CreateSnapshot_ClearsRequestWhenReportedStateMatches()
    {
        var coordinator = new PlaybackStateCoordinator();
        var now = new DateTimeOffset(2026, 7, 11, 3, 0, 0, TimeSpan.Zero);
        coordinator.ApplyRequestedState(true, TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(3), now);
        PlaybackSnapshot snapshot = coordinator.CreateSnapshot(
            true, TimeSpan.FromSeconds(50), now, TimeSpan.FromMinutes(3), now.AddSeconds(1));
        Assert.True(snapshot.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(51), snapshot.Position);
    }
}
