using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private IReadOnlyList<GlobalSystemMediaTransportControlsSession> _sessions = new List<GlobalSystemMediaTransportControlsSession>();

        public event EventHandler<MediaInfoChangedEventArgs>? MediaInfoChanged;
        public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
        public event EventHandler<MediaTimelineChangedEventArgs>? MediaTimelineChanged;
        public event EventHandler<SessionsInfoChangedEventArgs>? SessionsInfoChanged;

        private System.Threading.Timer? _progressTimer;

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    UpdateSessionsList();
                }

                _progressTimer = new System.Threading.Timer(UpdateProgress, null, 0, 500);
            }
            catch (Exception)
            {
                // Ignored for prototype
            }
        }

        private void UpdateProgress(object? state)
        {
            if (_currentSession == null) return;
            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                var playbackInfo = _currentSession.GetPlaybackInfo();
                
                if (timeline != null && playbackInfo != null)
                {
                    var position = timeline.Position;
                    bool isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    
                    if (isPlaying && timeline.LastUpdatedTime != default)
                    {
                        var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime.ToUniversalTime();
                        position += elapsed;
                    }

                    if (position > timeline.EndTime) position = timeline.EndTime;
                    if (position < TimeSpan.Zero) position = TimeSpan.Zero;

                    MediaTimelineChanged?.Invoke(this, new MediaTimelineChangedEventArgs
                    {
                        Position = position,
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

        private void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            UpdateSessionsList();
        }

        private void UpdateSessionsList()
        {
            if (_sessionManager == null) return;
            
            _sessions = _sessionManager.GetSessions();
            
            if (_currentSession != null && !_sessions.Any(s => s.SourceAppUserModelId == _currentSession.SourceAppUserModelId))
            {
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }
            else if (_currentSession == null)
            {
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }

            NotifySessionsInfoChanged();
        }

        private void NotifySessionsInfoChanged()
        {
            int currentIndex = -1;
            if (_currentSession != null)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    if (_sessions[i].SourceAppUserModelId == _currentSession.SourceAppUserModelId)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            SessionsInfoChanged?.Invoke(this, new SessionsInfoChangedEventArgs
            {
                SessionCount = _sessions.Count,
                CurrentSessionIndex = currentIndex
            });
        }

        private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
            }

            _currentSession = session;

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
                
                _ = UpdateMediaPropertiesAsync();
                UpdatePlaybackInfo();
            }

            NotifySessionsInfoChanged();
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
            if (_currentSession == null) return;

            try
            {
                var properties = await _currentSession.TryGetMediaPropertiesAsync();
                if (properties == null) return;

                BitmapImage? albumArt = null;
                if (properties.Thumbnail != null)
                {
                    using var stream = await properties.Thumbnail.OpenReadAsync();
                    if (stream != null)
                    {
                        var winStream = stream.AsStreamForRead();
                        var memStream = await CopyThumbnailToMemoryAsync(winStream);

                        if (memStream != null)
                        {
                            using (memStream)
                            {
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
                            }
                        }
                    }
                }

                MediaInfoChanged?.Invoke(this, new MediaInfoChangedEventArgs
                {
                    Title = properties.Title,
                    Artist = properties.Artist,
                    AlbumTitle = properties.AlbumTitle,
                    AlbumArt = albumArt
                });
            }
            catch (Exception)
            {
                // Ignored
            }
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
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
                {
                    IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                });
            }
        }

        public async Task PlayPauseAsync()
        {
            if (_currentSession != null)
            {
                await _currentSession.TryTogglePlayPauseAsync();
            }
        }

        public async Task NextAsync()
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipNextAsync();
            }
        }

        public async Task PreviousAsync()
        {
            if (_currentSession != null)
            {
                await _currentSession.TrySkipPreviousAsync();
            }
        }

        public void SwitchToNextSession()
        {
            if (_sessions.Count <= 1) return;
            
            int currentIndex = -1;
            for (int i = 0; i < _sessions.Count; i++)
            {
                if (_currentSession != null && _sessions[i].SourceAppUserModelId == _currentSession.SourceAppUserModelId)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + 1) % _sessions.Count;
            UpdateCurrentSession(_sessions[nextIndex]);
        }

        public void SwitchToPreviousSession()
        {
            if (_sessions.Count <= 1) return;
            
            int currentIndex = -1;
            for (int i = 0; i < _sessions.Count; i++)
            {
                if (_currentSession != null && _sessions[i].SourceAppUserModelId == _currentSession.SourceAppUserModelId)
                {
                    currentIndex = i;
                    break;
                }
            }

            int prevIndex = currentIndex - 1;
            if (prevIndex < 0) prevIndex = _sessions.Count - 1;
            UpdateCurrentSession(_sessions[prevIndex]);
        }

        public void SwitchToSession(int index)
        {
            if (index >= 0 && index < _sessions.Count)
            {
                UpdateCurrentSession(_sessions[index]);
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
        public BitmapImage? AlbumArt { get; set; }
    }

    public class PlaybackStateChangedEventArgs : EventArgs
    {
        public bool IsPlaying { get; set; }
    }

    public class SessionsInfoChangedEventArgs : EventArgs
    {
        public int SessionCount { get; set; }
        public int CurrentSessionIndex { get; set; }
    }
}
