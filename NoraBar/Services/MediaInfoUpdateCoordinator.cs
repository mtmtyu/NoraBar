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
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                bool metadataChanged = _currentMetadata != metadata;
                if (!metadataChanged &&
                    (_albumArtLoadState is AlbumArtLoadState.Loading or
                        AlbumArtLoadState.Loaded ||
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
                        UpdateVersion = _updateVersion,
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
                try
                {
                    MediaInfoChanged?.Invoke(this, metadataArgs);
                }
                catch
                {
                    ResetCanceledLoad(updateVersion);
                    throw;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ResetCanceledLoad(updateVersion);
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
                if (updateVersion != _updateVersion)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _albumArtLoadAttempts--;
                    _albumArtLoadState = AlbumArtLoadState.NotAttempted;
                    return;
                }

                if (albumArt is null)
                {
                    _albumArtLoadAttempts--;
                }

                _albumArtLoadState = albumArt is null
                    ? AlbumArtLoadState.NotAvailable
                    : AlbumArtLoadState.Loaded;
                albumArtArgs = new AlbumArtChangedEventArgs
                {
                    UpdateVersion = updateVersion,
                    AlbumArt = albumArt
                };
            }

            if (albumArtArgs is not null && !cancellationToken.IsCancellationRequested)
            {
                AlbumArtChanged?.Invoke(this, albumArtArgs);
            }
        }

        private void ResetCanceledLoad(int updateVersion)
        {
            lock (_syncRoot)
            {
                if (updateVersion != _updateVersion)
                {
                    return;
                }

                _albumArtLoadAttempts--;
                _albumArtLoadState = AlbumArtLoadState.NotAttempted;
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

        public void Clear()
        {
            Reset();
            int updateVersion;
            lock (_syncRoot)
            {
                updateVersion = _updateVersion;
            }
            MediaInfoChanged?.Invoke(this, new MediaInfoChangedEventArgs
            {
                UpdateVersion = updateVersion
            });
            AlbumArtChanged?.Invoke(this, new AlbumArtChangedEventArgs
            {
                UpdateVersion = updateVersion
            });
        }
    }
}
