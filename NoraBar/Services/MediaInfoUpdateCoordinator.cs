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
                    MediaInfoChanged?.Invoke(this, new MediaInfoChangedEventArgs
                    {
                        Title = metadata.Title,
                        Artist = metadata.Artist,
                        AlbumTitle = metadata.AlbumTitle
                    });
                }

                updateVersion = _updateVersion;
                _isAlbumArtLoading = true;
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

            lock (_syncRoot)
            {
                if (updateVersion != _updateVersion)
                {
                    return;
                }

                _isAlbumArtLoading = false;
                _hasLoadedAlbumArt = albumArt != null;
                AlbumArtChanged?.Invoke(this, new AlbumArtChangedEventArgs
                {
                    AlbumArt = albumArt
                });
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
