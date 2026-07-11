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
        private const int ProgressRefreshIntervalMilliseconds = 500;
        private const int MediaRefreshIntervalMilliseconds = 10_000;

        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly MediaInfoUpdateCoordinator _mediaInfoUpdateCoordinator = new();
        private readonly PlaybackStateCoordinator _playbackStateCoordinator = new();
        private readonly SemaphoreSlim _mediaPropertiesRefreshLock = new(1, 1);
        private readonly SemaphoreSlim _playPauseLock = new(1, 1);
        private readonly object _sessionUpdateLock = new();
        private CancellationTokenSource _sessionCancellation = new();

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<AlbumArtChangedEventArgs>? AlbumArtChanged;
        public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
        public event EventHandler<MediaTimelineChangedEventArgs>? MediaTimelineChanged;

        private System.Threading.Timer? _progressTimer;
        private System.Threading.Timer? _mediaRefreshTimer;

        public MediaControlService()
        {
            _mediaInfoUpdateCoordinator.MediaInfoChanged += (_, args) =>
            {
                _playbackStateCoordinator.ClearPendingRequest();
                MediaInfoChanged?.Invoke(this, args);
            };
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
                    ProgressRefreshIntervalMilliseconds);
                _mediaRefreshTimer = new System.Threading.Timer(
                    UpdateMediaProperties,
                    null,
                    MediaRefreshIntervalMilliseconds,
                    MediaRefreshIntervalMilliseconds);
            }
            catch (Exception)
            {
                // Ignored for prototype
            }
        }

        private void UpdateProgress(object? state)
        {
            var session = _currentSession;
            if (session == null) return;
            try
            {
                var timeline = session.GetTimelineProperties();
                var playbackInfo = session.GetPlaybackInfo();

                if (timeline != null && playbackInfo != null && session == _currentSession)
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

        private void UpdateMediaProperties(object? state)
        {
            _ = UpdateMediaPropertiesAsync();
        }

        private void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            UpdateCurrentSession(sender.GetCurrentSession());
        }

        private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
        {
            lock (_sessionUpdateLock)
            {
                if (_currentSession != null)
                {
                    _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
                }

                _currentSession = session;
                _sessionCancellation.Cancel();
                _sessionCancellation.Dispose();
                _sessionCancellation = new CancellationTokenSource();

                if (_currentSession != null)
                {
                    _mediaInfoUpdateCoordinator.Reset();
                    _playbackStateCoordinator.Reset();
                    _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                    _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;

                    _ = UpdateMediaPropertiesAsync();
                    UpdatePlaybackInfo();
                }
                else
                {
                    _mediaInfoUpdateCoordinator.Clear();
                    _playbackStateCoordinator.Clear();
                    MediaTimelineChanged?.Invoke(this, new MediaTimelineChangedEventArgs
                    {
                        Position = TimeSpan.Zero,
                        EndTime = TimeSpan.Zero
                    });
                }
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
            GlobalSystemMediaTransportControlsSessionMediaProperties? properties = null;
            GlobalSystemMediaTransportControlsSession? session;
            CancellationToken cancellationToken;
            lock (_sessionUpdateLock)
            {
                session = _currentSession;
                cancellationToken = _sessionCancellation.Token;
            }

            if (session == null)
            {
                return;
            }

            bool lockAcquired = false;

            try
            {
                await _mediaPropertiesRefreshLock.WaitAsync(cancellationToken);
                lockAcquired = true;
                properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                // Ignored
            }
            finally
            {
                if (lockAcquired)
                {
                    _mediaPropertiesRefreshLock.Release();
                }
            }

            if (properties == null ||
                cancellationToken.IsCancellationRequested ||
                session != _currentSession)
            {
                return;
            }

            try
            {
                await _mediaInfoUpdateCoordinator.PublishForSessionAsync(
                    new MediaMetadata(properties.Title, properties.Artist, properties.AlbumTitle),
                    () => LoadAlbumArtAsync(properties, cancellationToken),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
            }
        }

        private static async Task<BitmapImage?> LoadAlbumArtAsync(
            GlobalSystemMediaTransportControlsSessionMediaProperties properties,
            CancellationToken cancellationToken)
        {
            if (properties.Thumbnail == null)
            {
                return null;
            }

            using var stream = await properties.Thumbnail.OpenReadAsync().AsTask(cancellationToken);
            if (stream == null)
            {
                return null;
            }

            var winStream = stream.AsStreamForRead();
            using var memStream = await CopyThumbnailToMemoryAsync(winStream, cancellationToken);
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

        private static async Task<MemoryStream?> CopyThumbnailToMemoryAsync(
            Stream source,
            CancellationToken cancellationToken)
        {
            var memoryStream = new MemoryStream();
            byte[] buffer = new byte[StreamCopyBufferSize];

            try
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
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
            var session = _currentSession;
            if (session == null) return;

            try
            {
                var playbackInfo = session.GetPlaybackInfo();
                if (playbackInfo == null || session != _currentSession)
                {
                    return;
                }

                bool isPlaying =
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                _playbackStateCoordinator.ApplyAuthoritativeState(isPlaying);
            }
            catch
            {
            }
        }

        public async Task PlayPauseAsync()
        {
            if (!await _playPauseLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                await PlayPauseCoreAsync();
            }
            finally
            {
                _playPauseLock.Release();
            }
        }

        private async Task PlayPauseCoreAsync()
        {
            var session = _currentSession;
            if (session == null)
            {
                return;
            }

            GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo;
            GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline;
            try
            {
                playbackInfo = session.GetPlaybackInfo();
                timeline = session.GetTimelineProperties();
            }
            catch
            {
                await TryTogglePlayPauseAsync(session);
                return;
            }
            if (playbackInfo == null || timeline == null)
            {
                await TryTogglePlayPauseAsync(session);
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

            bool succeeded;
            try
            {
                succeeded = await session.TryTogglePlayPauseAsync();
            }
            catch
            {
                return;
            }
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

        private static async Task TryTogglePlayPauseAsync(
            GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                await session.TryTogglePlayPauseAsync();
            }
            catch
            {
            }
        }

        public async Task NextAsync()
        {
            var session = _currentSession;
            if (session == null)
            {
                return;
            }

            try
            {
                bool succeeded = await session.TrySkipNextAsync();
                if (succeeded && session == _currentSession)
                {
                    await UpdateMediaPropertiesAsync();
                }
            }
            catch
            {
            }
        }

        public async Task PreviousAsync()
        {
            var session = _currentSession;
            if (session == null)
            {
                return;
            }

            try
            {
                bool succeeded = await session.TrySkipPreviousAsync();
                if (succeeded && session == _currentSession)
                {
                    await UpdateMediaPropertiesAsync();
                }
            }
            catch
            {
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
        public int UpdateVersion { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? AlbumTitle { get; set; }
    }

    public class AlbumArtChangedEventArgs : EventArgs
    {
        public int UpdateVersion { get; set; }
        public BitmapImage? AlbumArt { get; set; }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; set; }
    }
}
