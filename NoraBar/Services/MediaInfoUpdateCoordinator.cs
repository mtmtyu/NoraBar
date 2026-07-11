using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NoraBar.Services
{
    internal readonly record struct MediaMetadata(string? Title, string? Artist, string? AlbumTitle);

    internal enum AlbumArtLoadState
    {
        NotAttempted,
        Loading,
        Loaded,
        NotAvailable,
        Failed
    }

    internal sealed class MediaInfoUpdateCoordinator
    {
        private const int MaxAlbumArtLoadAttempts = 3;
        private readonly object _syncRoot = new();
        private MediaMetadata? _currentMetadata;
        private int _updateVersion;
        private int _albumArtLoadAttempts;
        private AlbumArtLoadState _albumArtLoadState;

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<AlbumArtChangedEventArgs>? AlbumArtChanged;

        public Task PublishAsync(
            MediaMetadata metadata,
            Func<Task<BitmapImage?>> loadAlbumArtAsync)
        {
            return PublishForSessionAsync(metadata, loadAlbumArtAsync, CancellationToken.None);
        }

        public async Task PublishForSessionAsync(
            MediaMetadata metadata,
            Func<Task<BitmapImage?>> loadAlbumArtAsync,
            CancellationToken cancellationToken)
        {
            int updateVersion;
            MediaInfoChangedEventArgs? metadataArgs = null;

            lock (_syncRoot)
            {
                bool metadataChanged = _currentMetadata != metadata;
                if (!metadataChanged &&
                    (_albumArtLoadState is AlbumArtLoadState.Loading or
                        AlbumArtLoadState.Loaded or
                        AlbumArtLoadState.NotAvailable ||
                     _albumArtLoadAttempts >= MaxAlbumArtLoadAttempts))
                {
                    return;
                }

                if (metadataChanged)
                {
                    _currentMetadata = metadata;
                    _albumArtLoadAttempts = 0;
                    _albumArtLoadState = AlbumArtLoadState.NotAttempted;
                    _updateVersion++;
                    metadataArgs = new MediaInfoChangedEventArgs
                    {
                        Title = metadata.Title,
                        Artist = metadata.Artist,
                        AlbumTitle = metadata.AlbumTitle
                    };
                }

                updateVersion = _updateVersion;
                _albumArtLoadAttempts++;
                _albumArtLoadState = AlbumArtLoadState.Loading;
            }

            if (metadataArgs is not null && !cancellationToken.IsCancellationRequested)
            {
                MediaInfoChanged?.Invoke(this, metadataArgs);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            BitmapImage? albumArt;
            try
            {
                albumArt = await loadAlbumArtAsync();
            }
            catch
            {
                lock (_syncRoot)
                {
                    if (updateVersion == _updateVersion)
                    {
                        _albumArtLoadState = AlbumArtLoadState.Failed;
                    }
                }
                return;
            }

            AlbumArtChangedEventArgs? albumArtArgs = null;
            lock (_syncRoot)
            {
                if (updateVersion != _updateVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _albumArtLoadState = albumArt is null
                    ? AlbumArtLoadState.NotAvailable
                    : AlbumArtLoadState.Loaded;
                albumArtArgs = new AlbumArtChangedEventArgs
                {
                    AlbumArt = albumArt
                };
            }

            if (albumArtArgs is not null && !cancellationToken.IsCancellationRequested)
            {
                AlbumArtChanged?.Invoke(this, albumArtArgs);
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _currentMetadata = null;
                _albumArtLoadAttempts = 0;
                _albumArtLoadState = AlbumArtLoadState.NotAttempted;
                _updateVersion++;
            }
        }
    }
}
