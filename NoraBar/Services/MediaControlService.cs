using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace NoraBar.Services
{
    public class MediaControlService
    {
        private const int MaxAlbumArtBytes = 5 * 1024 * 1024;
        private const int MaxAlbumArtDecodePixels = 512;
        private const int StreamCopyBufferSize = 81920;
        private const int MediaRefreshIntervalMilliseconds = 500;

        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly MediaInfoUpdateCoordinator _mediaInfoUpdateCoordinator = new();
        private readonly PlaybackStateCoordinator _playbackStateCoordinator = new();
        private int _isRefreshingMediaProperties;

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<AlbumArtChangedEventArgs>? AlbumArtChanged;
        public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
        public event EventHandler<MediaTimelineChangedEventArgs>? MediaTimelineChanged;

        private System.Threading.Timer? _progressTimer;

        public MediaControlService()
        {
            _mediaInfoUpdateCoordinator.MediaInfoChanged += (_, args) =>
                MediaInfoChanged?.Invoke(this, args);
            _mediaInfoUpdateCoordinator.AlbumArtChanged += (_, args) =>
                AlbumArtChanged?.Invoke(this, args);
            _playbackStateCoordinator.PlaybackStateChanged += (_, args) =>
                PlaybackStateChanged?.Invoke(this, args);
        }

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    UpdateCurrentSession(_sessionManager.GetCurrentSession());
                }

                _progressTimer = new System.Threading.Timer(
                    UpdateProgress,
                    null,
                    0,
                    MediaRefreshIntervalMilliseconds);
            }
            catch (Exception)
            {
                // Ignored for prototype
            }
        }

        private void UpdateProgress(object? state)
        {
            _ = UpdateMediaPropertiesAsync();

            if (_currentSession == null) return;
            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                var playbackInfo = _currentSession.GetPlaybackInfo();

                if (timeline != null && playbackInfo != null)
                {
                    bool reportedIsPlaying =
                        playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    PlaybackSnapshot snapshot = _playbackStateCoordinator.CreateSnapshot(
                        reportedIsPlaying,
                        timeline.Position,
                        timeline.LastUpdatedTime,
                        timeline.EndTime,
                        DateTimeOffset.UtcNow);

                    MediaTimelineChanged?.Invoke(this, new MediaTimelineChangedEventArgs
                    {
                        Position = snapshot.Position,
                        EndTime = timeline.EndTime
                    });
                }
            }
            catch { }
        }

        private void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            UpdateCurrentSession(sender.GetCurrentSession());
        }

        private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
            }

            _currentSession = session;
            _mediaInfoUpdateCoordinator.Reset();
            _playbackStateCoordinator.Reset();

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;

                _ = UpdateMediaPropertiesAsync();
                UpdatePlaybackInfo();
            }
        }

        private async void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            await UpdateMediaPropertiesAsync();
        }

        private void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            UpdatePlaybackInfo();
        }

        private async Task UpdateMediaPropertiesAsync()
        {
            if (Interlocked.CompareExchange(ref _isRefreshingMediaProperties, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var session = _currentSession;
                if (session == null)
                {
                    return;
                }

                var properties = await session.TryGetMediaPropertiesAsync();
                if (properties == null) return;
                if (session != _currentSession) return;

                _ = _mediaInfoUpdateCoordinator.PublishAsync(
                    new MediaMetadata(properties.Title, properties.Artist, properties.AlbumTitle),
                    () => LoadAlbumArtAsync(properties));
            }
            catch (Exception)
            {
                // Ignored
            }
            finally
            {
                Volatile.Write(ref _isRefreshingMediaProperties, 0);
            }
        }

        private static async Task<BitmapImage?> LoadAlbumArtAsync(
            GlobalSystemMediaTransportControlsSessionMediaProperties properties)
        {
            if (properties.Thumbnail == null)
            {
                return null;
            }

            using var stream = await properties.Thumbnail.OpenReadAsync();
            if (stream == null)
            {
                return null;
            }

            var winStream = stream.AsStreamForRead();
            using var memStream = await CopyThumbnailToMemoryAsync(winStream);
            if (memStream == null)
            {
                return null;
            }

            BitmapImage? albumArt = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = MaxAlbumArtDecodePixels;
                bitmap.DecodePixelHeight = MaxAlbumArtDecodePixels;
                bitmap.StreamSource = memStream;
                bitmap.EndInit();
                bitmap.Freeze();
                albumArt = bitmap;
            });

            return albumArt;
        }

        private static async Task<MemoryStream?> CopyThumbnailToMemoryAsync(Stream source)
        {
            var memoryStream = new MemoryStream();
            byte[] buffer = new byte[StreamCopyBufferSize];

            try
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (memoryStream.Length + bytesRead > MaxAlbumArtBytes)
                    {
                        memoryStream.Dispose();
                        return null;
                    }

                    memoryStream.Write(buffer, 0, bytesRead);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
        }

        private void UpdatePlaybackInfo()
        {
            if (_currentSession == null) return;

            var playbackInfo = _currentSession.GetPlaybackInfo();
            if (playbackInfo != null)
            {
                bool isPlaying =
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                _playbackStateCoordinator.ApplyAuthoritativeState(isPlaying);
            }
        }

        public async Task PlayPauseAsync()
        {
            var session = _currentSession;
            if (session == null)
            {
                return;
            }

            var playbackInfo = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            if (playbackInfo == null || timeline == null)
            {
                await session.TryTogglePlayPauseAsync();
                return;
            }

            var requestStartedAt = DateTimeOffset.UtcNow;
            bool reportedIsPlaying =
                playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            PlaybackSnapshot currentSnapshot = _playbackStateCoordinator.CreateSnapshot(
                reportedIsPlaying,
                timeline.Position,
                timeline.LastUpdatedTime,
                timeline.EndTime,
                requestStartedAt);

            bool succeeded = await session.TryTogglePlayPauseAsync();
            if (!succeeded || session != _currentSession)
            {
                return;
            }

            var completedAt = DateTimeOffset.UtcNow;
            TimeSpan position = currentSnapshot.Position;
            if (currentSnapshot.IsPlaying)
            {
                position += completedAt - requestStartedAt;
            }

            PlaybackSnapshot requestedSnapshot = _playbackStateCoordinator.ApplyRequestedState(
                !currentSnapshot.IsPlaying,
                position,
                timeline.EndTime,
                completedAt);
            MediaTimelineChanged?.Invoke(this, new MediaTimelineChangedEventArgs
            {
                Position = requestedSnapshot.Position,
                EndTime = timeline.EndTime
            });
        }

        public async Task NextAsync()
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipNextAsync();
                await UpdateMediaPropertiesAsync();
            }
        }

        public async Task PreviousAsync()
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipPreviousAsync();
                await UpdateMediaPropertiesAsync();
            }
        }
    }

    public class MediaTimelineChangedEventArgs : EventArgs
    {
        public TimeSpan Position { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class MediaInfoChangedEventArgs : EventArgs
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? AlbumTitle { get; set; }
    }

    public class AlbumArtChangedEventArgs : EventArgs
    {
        public BitmapImage? AlbumArt { get; set; }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; set; }
    }
}
