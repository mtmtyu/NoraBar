using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class MediaInfoUpdateCoordinatorTests
{
    [Fact]
    public async Task PublishAsync_NotifiesMetadataBeforeArtworkLoadingCompletes()
    {
        var artworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new MediaInfoUpdateCoordinator();
        MediaInfoChangedEventArgs? receivedMetadata = null;
        bool artworkNotified = false;

        coordinator.MediaInfoChanged += (_, args) => receivedMetadata = args;
        coordinator.AlbumArtChanged += (_, _) => artworkNotified = true;

        Task updateTask = coordinator.PublishAsync(
            new MediaMetadata("New title", "New artist", "New album"),
            () => artworkSource.Task);

        Assert.Equal("New title", receivedMetadata?.Title);
        Assert.False(artworkNotified);
        Assert.False(updateTask.IsCompleted);

        artworkSource.SetResult(null);
        await updateTask;

        Assert.True(artworkNotified);
    }

    [Fact]
    public async Task PublishAsync_DoesNotPublishArtworkFromAnOlderTrack()
    {
        var firstArtworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondArtworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new MediaInfoUpdateCoordinator();
        int artworkNotificationCount = 0;

        coordinator.AlbumArtChanged += (_, _) => artworkNotificationCount++;

        Task firstUpdate = coordinator.PublishAsync(
            new MediaMetadata("First", "Artist", "Album"),
            () => firstArtworkSource.Task);
        Task secondUpdate = coordinator.PublishAsync(
            new MediaMetadata("Second", "Artist", "Album"),
            () => secondArtworkSource.Task);

        firstArtworkSource.SetResult(null);
        await firstUpdate;
        Assert.Equal(0, artworkNotificationCount);

        secondArtworkSource.SetResult(null);
        await secondUpdate;
        Assert.Equal(1, artworkNotificationCount);
    }

    [Fact]
    public async Task PublishAsync_KeepsMetadataUpdateWhenArtworkLoadingFails()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        MediaInfoChangedEventArgs? receivedMetadata = null;
        bool artworkNotified = false;

        coordinator.MediaInfoChanged += (_, args) => receivedMetadata = args;
        coordinator.AlbumArtChanged += (_, _) => artworkNotified = true;

        await coordinator.PublishAsync(
            new MediaMetadata("Available title", "Artist", "Album"),
            () => Task.FromException<System.Windows.Media.Imaging.BitmapImage?>(
                new IOException("Artwork is unavailable.")));

        Assert.Equal("Available title", receivedMetadata?.Title);
        Assert.False(artworkNotified);
    }

    [Fact]
    public async Task PublishAsync_RetriesArtworkAfterLoadingFailsForSameMetadata()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Available title", "Artist", "Album");
        int artworkNotificationCount = 0;

        coordinator.AlbumArtChanged += (_, _) => artworkNotificationCount++;

        await coordinator.PublishAsync(
            metadata,
            () => Task.FromException<System.Windows.Media.Imaging.BitmapImage?>(
                new IOException("Artwork is unavailable.")));
        await coordinator.PublishAsync(metadata, () => Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null));

        Assert.Equal(1, artworkNotificationCount);
    }

    [Fact]
    public async Task PublishAsync_RetriesArtworkWhenItIsInitiallyUnavailable()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Available title", "Artist", "Album");
        int artworkLoadCount = 0;

        await coordinator.PublishAsync(metadata, LoadArtworkAsync);
        await coordinator.PublishAsync(metadata, LoadArtworkAsync);

        Assert.Equal(2, artworkLoadCount);

        Task<System.Windows.Media.Imaging.BitmapImage?> LoadArtworkAsync()
        {
            artworkLoadCount++;
            return Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null);
        }
    }
}
