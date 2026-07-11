using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NoraBar.Services
{
    internal readonly record struct MediaMetadata(string? Title, string? Artist, string? AlbumTitle);

    internal sealed class MediaInfoUpdateCoordinator
    {
        private readonly object _syncRoot = new();
        private MediaMetadata? _currentMetadata;
        private int _updateVersion;
        private bool _isAlbumArtLoading;
        private bool _hasLoadedAlbumArt;

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<AlbumArtChangedEventArgs>? AlbumArtChanged;

        public async Task PublishAsync(
            MediaMetadata metadata,
            Func<Task<BitmapImage?>> loadAlbumArtAsync)
        {
            int updateVersion;
            MediaInfoChangedEventArgs? metadataArgs = null;

            lock (_syncRoot)
            {
                bool metadataChanged = _currentMetadata != metadata;
                if (!metadataChanged && (_isAlbumArtLoading || _hasLoadedAlbumArt))
                {
                    return;
                }

                if (metadataChanged)
                {
                    _currentMetadata = metadata;
                    _hasLoadedAlbumArt = false;
                    _updateVersion++;
                    metadataArgs = new MediaInfoChangedEventArgs
                    {
                        Title = metadata.Title,
                        Artist = metadata.Artist,
                        AlbumTitle = metadata.AlbumTitle
                    };
                }

                updateVersion = _updateVersion;
                _isAlbumArtLoading = true;
            }

            if (metadataArgs is not null)
            {
                MediaInfoChanged?.Invoke(this, metadataArgs);
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
                        _isAlbumArtLoading = false;
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

                _isAlbumArtLoading = false;
                _hasLoadedAlbumArt = albumArt != null;
                albumArtArgs = new AlbumArtChangedEventArgs
                {
                    AlbumArt = albumArt
                };
            }

            if (albumArtArgs is not null)
            {
                AlbumArtChanged?.Invoke(this, albumArtArgs);
            }
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _currentMetadata = null;
                _isAlbumArtLoading = false;
                _hasLoadedAlbumArt = false;
                _updateVersion++;
            }
        }
    }
}
