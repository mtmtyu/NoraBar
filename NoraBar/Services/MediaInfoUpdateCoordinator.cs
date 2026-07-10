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

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<AlbumArtChangedEventArgs>? AlbumArtChanged;

        public async Task PublishAsync(
            MediaMetadata metadata,
            Func<Task<BitmapImage?>> loadAlbumArtAsync)
        {
            int updateVersion;

            lock (_syncRoot)
            {
                if (_currentMetadata == metadata)
                {
                    return;
                }

                _currentMetadata = metadata;
                updateVersion = ++_updateVersion;
                MediaInfoChanged?.Invoke(this, new MediaInfoChangedEventArgs
                {
                    Title = metadata.Title,
                    Artist = metadata.Artist,
                    AlbumTitle = metadata.AlbumTitle
                });
            }

            BitmapImage? albumArt;
            try
            {
                albumArt = await loadAlbumArtAsync();
            }
            catch
            {
                return;
            }

            lock (_syncRoot)
            {
                if (updateVersion != _updateVersion)
                {
                    return;
                }

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
                _updateVersion++;
            }
        }
    }
}
