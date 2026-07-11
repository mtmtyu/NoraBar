using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class MediaInfoUpdateCoordinatorTests
{
    [Fact]
    public async Task Reset_AssignsANewerVersionToSubsequentNotifications()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        MediaInfoChangedEventArgs? firstMetadata = null;
        MediaInfoChangedEventArgs? secondMetadata = null;
        AlbumArtChangedEventArgs? secondArtwork = null;

        coordinator.MediaInfoChanged += (_, args) =>
        {
            if (firstMetadata is null)
            {
                firstMetadata = args;
            }
            else
            {
                secondMetadata = args;
            }
        };
        coordinator.AlbumArtChanged += (_, args) => secondArtwork = args;

        await coordinator.PublishAsync(
            new MediaMetadata("First", "Artist", "Album"),
            () => Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null));
        coordinator.Reset();
        await coordinator.PublishAsync(
            new MediaMetadata("Second", "Artist", "Album"),
            () => Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null));

        Assert.NotNull(firstMetadata);
        Assert.NotNull(secondMetadata);
        Assert.NotNull(secondArtwork);
        Assert.True(secondMetadata.UpdateVersion > firstMetadata.UpdateVersion);
        Assert.Equal(secondMetadata.UpdateVersion, secondArtwork.UpdateVersion);
    }

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
    public async Task PublishAsync_RetriesArtworkWhenItBecomesAvailableForTheSameMetadata()
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

    [Fact]
    public async Task PublishForSessionAsync_AllowsRetryAfterAnAlreadyCanceledUpdate()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Title", "Artist", "Album");
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        int artworkLoadCount = 0;

        await coordinator.PublishForSessionAsync(metadata, LoadArtworkAsync, cancellationSource.Token);
        await coordinator.PublishAsync(metadata, LoadArtworkAsync);

        Assert.Equal(1, artworkLoadCount);

        Task<System.Windows.Media.Imaging.BitmapImage?> LoadArtworkAsync()
        {
            artworkLoadCount++;
            return Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null);
        }
    }

    [Fact]
    public async Task PublishAsync_RetriesArtworkAfterThreeUnavailableResults()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Available title", "Artist", "Album");
        int artworkLoadCount = 0;
        var availableArtwork = new System.Windows.Media.Imaging.BitmapImage();

        for (int attempt = 0; attempt < 4; attempt++)
        {
            await coordinator.PublishAsync(metadata, LoadArtworkAsync);
        }

        Assert.Equal(4, artworkLoadCount);

        Task<System.Windows.Media.Imaging.BitmapImage?> LoadArtworkAsync()
        {
            artworkLoadCount++;
            return Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(
                artworkLoadCount == 4 ? availableArtwork : null);
        }
    }

    [Fact]
    public async Task PublishAsync_SetsLoadingStateBeforeNotifyingMetadataSubscribers()
    {
        var artworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Title", "Artist", "Album");
        int artworkLoadCount = 0;

        coordinator.MediaInfoChanged += (_, _) => _ = coordinator.PublishAsync(metadata, LoadArtworkAsync);

        Task updateTask = coordinator.PublishAsync(metadata, LoadArtworkAsync);

        Assert.Equal(1, artworkLoadCount);

        artworkSource.SetResult(null);
        await updateTask;

        Task<System.Windows.Media.Imaging.BitmapImage?> LoadArtworkAsync()
        {
            artworkLoadCount++;
            return artworkSource.Task;
        }
    }

    [Fact]
    public async Task PublishAsync_StopsRetryingArtworkAfterThreeFailures()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Title", "Artist", "Album");
        int artworkLoadCount = 0;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            await coordinator.PublishAsync(metadata, LoadArtworkAsync);
        }

        Assert.Equal(3, artworkLoadCount);

        Task<System.Windows.Media.Imaging.BitmapImage?> LoadArtworkAsync()
        {
            artworkLoadCount++;
            return Task.FromException<System.Windows.Media.Imaging.BitmapImage?>(
                new IOException("Artwork is unavailable."));
        }
    }

    [Fact]
    public async Task Reset_SuppressesInFlightArtworkAndAllowsNewMetadata()
    {
        var firstArtworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new MediaInfoUpdateCoordinator();
        int metadataNotificationCount = 0;
        int artworkNotificationCount = 0;

        coordinator.MediaInfoChanged += (_, _) => metadataNotificationCount++;
        coordinator.AlbumArtChanged += (_, _) => artworkNotificationCount++;

        Task firstUpdate = coordinator.PublishAsync(
            new MediaMetadata("First", "Artist", "Album"),
            () => firstArtworkSource.Task);

        coordinator.Reset();
        firstArtworkSource.SetResult(null);
        await firstUpdate;

        Assert.Equal(0, artworkNotificationCount);

        await coordinator.PublishAsync(
            new MediaMetadata("Second", "Artist", "Album"),
            () => Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null));

        Assert.Equal(2, metadataNotificationCount);
        Assert.Equal(1, artworkNotificationCount);
    }

    [Fact]
    public async Task Clear_PublishesEmptyMediaInformationAndSuppressesInFlightArtwork()
    {
        var artworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new MediaInfoUpdateCoordinator();
        MediaInfoChangedEventArgs? receivedMetadata = null;
        AlbumArtChangedEventArgs? receivedArtwork = null;
        int artworkNotificationCount = 0;
        coordinator.MediaInfoChanged += (_, args) => receivedMetadata = args;
        coordinator.AlbumArtChanged += (_, args) =>
        {
            receivedArtwork = args;
            artworkNotificationCount++;
        };

        Task update = coordinator.PublishAsync(
            new MediaMetadata("Title", "Artist", "Album"),
            () => artworkSource.Task);

        coordinator.Clear();
        artworkSource.SetResult(null);
        await update;

        Assert.Null(receivedMetadata?.Title);
        Assert.Null(receivedMetadata?.Artist);
        Assert.Null(receivedMetadata?.AlbumTitle);
        Assert.Null(receivedArtwork?.AlbumArt);
        Assert.Equal(1, artworkNotificationCount);
    }

    [Fact]
    public async Task PublishForSessionAsync_AllowsRetryWhenCanceledDuringArtworkLoading()
    {
        var coordinator = new MediaInfoUpdateCoordinator();
        var metadata = new MediaMetadata("Title", "Artist", "Album");
        var artworkSource = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapImage?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();
        int artworkLoadCount = 0;

        Task canceledUpdate = coordinator.PublishForSessionAsync(
            metadata,
            () =>
            {
                artworkLoadCount++;
                return artworkSource.Task;
            },
            cancellationSource.Token);
        cancellationSource.Cancel();
        artworkSource.SetResult(null);
        await canceledUpdate;

        await coordinator.PublishAsync(
            metadata,
            () =>
            {
                artworkLoadCount++;
                return Task.FromResult<System.Windows.Media.Imaging.BitmapImage?>(null);
            });

        Assert.Equal(2, artworkLoadCount);
    }
}
