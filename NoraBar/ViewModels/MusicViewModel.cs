using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NoraBar.Services;
using NoraBar.Models;

namespace NoraBar.ViewModels
{
    public class MusicViewModel : ViewModelBase, IMusicChangeSource
    {
        private readonly MediaControlService _mediaService;
        private readonly AudioVisualizerService _audioVisualizerService;
        private readonly ILyricsService _lyricsService;
        private readonly Func<System.Threading.Tasks.Task> _lyricsRequestDelay;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private readonly object _lyricsRequestLock = new();
        private System.Collections.Generic.List<LyricLine>? _currentLyrics;
        private LyricsRequestSnapshot? _currentLyricsRequest;
        private double _lastDurationSeconds = 0;
        private TimeSpan _lastPosition = TimeSpan.Zero;
        private string _currentTrackName = "";
        private string _currentArtistName = "";
        private string _currentAlbumName = "";

        private readonly System.Collections.Generic.Dictionary<LyricsCacheKey, LyricsResult> _lyricsCache = new();

        private string _title = "Not Playing";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _artist = "Unknown Artist";
        public string Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        private BitmapImage? _albumArt;
        public BitmapImage? AlbumArt
        {
            get => _albumArt;
            set => SetProperty(ref _albumArt, value);
        }

        private TextScrollMode _textScrollMode = TextScrollMode.Disabled;
        public TextScrollMode TextScrollMode
        {
            get => _textScrollMode;
            set => SetProperty(ref _textScrollMode, value);
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        // Array to hold 8 heights for the waveform visualizer (values between 0.0 and 1.0)
        private float[] _spectrumData = new float[8];
        public float[] SpectrumData
        {
            get => _spectrumData;
            set => SetProperty(ref _spectrumData, value);
        }

        private string _positionText = "0:00";
        public string PositionText
        {
            get => _positionText;
            set => SetProperty(ref _positionText, value);
        }

        private string _durationText = "-:--";
        public string DurationText
        {
            get => _durationText;
            set => SetProperty(ref _durationText, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private string _currentLyric = string.Empty;
        public string CurrentLyric
        {
            get => _currentLyric;
            set => SetProperty(ref _currentLyric, value);
        }

        private bool _hasMultipleSessions;
        public bool HasMultipleSessions
        {
            get => _hasMultipleSessions;
            set => SetProperty(ref _hasMultipleSessions, value);
        }

        private bool _hasActiveSession;
        public bool HasActiveSession
        {
            get => _hasActiveSession;
            private set => SetProperty(ref _hasActiveSession, value);
        }

        public ObservableCollection<DotItem> SessionDots { get; } = new ObservableCollection<DotItem>();

        public ObservableCollection<LyricLineViewModel> LyricsList { get; } = new ObservableCollection<LyricLineViewModel>();

        private int _currentLyricIndex = -1;
        public int CurrentLyricIndex
        {
            get => _currentLyricIndex;
            set => SetProperty(ref _currentLyricIndex, value);
        }

        private bool _showLyrics = true;
        public bool ShowLyrics
        {
            get => _showLyrics;
            set
            {
                if (_showLyrics == value)
                {
                    return;
                }

                if (!value)
                {
                    InvalidateLyricsRequests();
                }

                if (SetProperty(ref _showLyrics, value))
                {
                    if (value && !string.IsNullOrEmpty(_currentTrackName))
                    {
                        LyricsRequestSnapshot request = CreateRequestForCurrentTrack();
                        QueueLoadingState(request);
                        _ = FetchOrApplyCachedLyricsAsync(request, applyDelay: false);
                    }
                    else if (!value)
                    {
                        RunOnDispatcher(ClearLyricsState);
                    }
                }
            }
        }

        public ICommand PlayPauseCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }

        public ICommand SwitchToNextSessionCommand { get; }
        public ICommand SwitchToPreviousSessionCommand { get; }
        public ICommand SwitchToSessionCommand { get; }

        private int _lyricsRequestId;

        public MusicViewModel()
            : this(SettingsService.Load().ShowLyrics)
        {
        }

        internal MusicViewModel(bool initialShowLyrics)
            : this(initialShowLyrics, startRuntimeServices: true)
        {
        }

        internal MusicViewModel(
            bool initialShowLyrics,
            bool startRuntimeServices)
            : this(
                new LyricsService(),
                static () => System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2)),
                startRuntimeServices,
                initialShowLyrics: initialShowLyrics)
        {
        }

        internal MusicViewModel(
            ILyricsService lyricsService,
            Func<System.Threading.Tasks.Task> lyricsRequestDelay,
            bool startRuntimeServices,
            bool initialShowLyrics)
        {
            _mediaService = new MediaControlService();
            _audioVisualizerService = new AudioVisualizerService();
            _lyricsService = lyricsService;
            _lyricsRequestDelay = lyricsRequestDelay;
            _dispatcher = startRuntimeServices
                ? System.Windows.Application.Current?.Dispatcher
                    ?? System.Windows.Threading.Dispatcher.CurrentDispatcher
                : System.Windows.Threading.Dispatcher.CurrentDispatcher;
            _showLyrics = initialShowLyrics;

            PlayPauseCommand = new RelayCommand(async _ => await _mediaService.PlayPauseAsync());
            NextCommand = new RelayCommand(async _ => await _mediaService.NextAsync());
            PreviousCommand = new RelayCommand(async _ => await _mediaService.PreviousAsync());

            SwitchToNextSessionCommand = new RelayCommand(_ => _mediaService.SwitchToNextSession());
            SwitchToPreviousSessionCommand = new RelayCommand(_ => _mediaService.SwitchToPreviousSession());
            SwitchToSessionCommand = new RelayCommand(param => {
                if (param is int index) {
                    _mediaService.SwitchToSession(index);
                }
            });

            _mediaService.MediaInfoChanged += async (_, e) =>
                await ProcessMediaInfoChangedAsync(e);
            _mediaService.PlaybackStateChanged += (s, e) =>
            {
                _dispatcher.Invoke(() =>
                {
                    IsPlaying = e.IsPlaying;
                });
            };

            _mediaService.MediaTimelineChanged += (_, e) =>
                ProcessMediaTimelineChanged(e.Position, e.EndTime);

            _mediaService.SessionsInfoChanged += (s, e) =>
            {
                _dispatcher.InvokeAsync(() =>
                {
                    HasActiveSession = e.SessionCount > 0;
                    HasMultipleSessions = e.SessionCount > 1;
                    
                    if (SessionDots.Count != e.SessionCount)
                    {
                        SessionDots.Clear();
                        for (int i = 0; i < e.SessionCount; i++)
                        {
                            SessionDots.Add(new DotItem { Index = i, IsActive = (i == e.CurrentSessionIndex) });
                        }
                    }
                    else
                    {
                        for (int i = 0; i < SessionDots.Count; i++)
                        {
                            SessionDots[i].IsActive = (i == e.CurrentSessionIndex);
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            };
            
            if (startRuntimeServices)
            {
                _ = _mediaService.InitializeAsync();
                _audioVisualizerService.SpectrumDataUpdated += AudioVisualizerService_SpectrumDataUpdated;
                _audioVisualizerService.Start();
            }
        }

        private void AudioVisualizerService_SpectrumDataUpdated(
            object? sender,
            float[] data)
        {
            System.Windows.Threading.Dispatcher? dispatcher =
                System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null
                || dispatcher.HasShutdownStarted
                || dispatcher.HasShutdownFinished)
            {
                return;
            }

            dispatcher.InvokeAsync(
                () => SpectrumData = data,
                System.Windows.Threading.DispatcherPriority.Render);
        }

        public void Cleanup()
        {
            _audioVisualizerService.SpectrumDataUpdated -= AudioVisualizerService_SpectrumDataUpdated;
            _audioVisualizerService.Stop();
            _audioVisualizerService.Dispose();
        }

        public void RestartVisualizer()
        {
            _audioVisualizerService.Stop();
            _audioVisualizerService.Start();
        }

        internal async System.Threading.Tasks.Task ProcessMediaInfoChangedAsync(
            MediaInfoChangedEventArgs mediaInfo)
        {
            var track = new LyricsTrackIdentity(
                mediaInfo.Title ?? string.Empty,
                mediaInfo.Artist ?? string.Empty,
                mediaInfo.AlbumTitle ?? string.Empty);

            lock (_lyricsRequestLock)
            {
                if (track.Matches(_currentTrackName, _currentArtistName, _currentAlbumName))
                {
                    QueueTrackState(track, mediaInfo.AlbumArt);
                    return;
                }

                _lyricsRequestId++;
                _currentTrackName = track.Title;
                _currentArtistName = track.Artist;
                _currentAlbumName = track.Album;
                _lastPosition = TimeSpan.Zero;
                _lastDurationSeconds = 0;
            }

            QueueTrackState(track, mediaInfo.AlbumArt);
            if (!ShowLyrics)
            {
                RunOnDispatcher(ClearLyricsState);
                return;
            }

            LyricsRequestSnapshot request = CreateRequestForCurrentTrack();
            QueueLoadingState(request);
            await FetchOrApplyCachedLyricsAsync(request, applyDelay: true);
        }

        internal void ProcessMediaTimelineChanged(TimeSpan position, TimeSpan endTime)
        {
            _lastPosition = position;
            _lastDurationSeconds = endTime.TotalSeconds;
            UpdateCurrentLyric(position);

            _dispatcher.InvokeAsync(() =>
            {
                PositionText = position.ToString(@"m\:ss");
                DurationText = endTime.ToString(@"m\:ss");
                ProgressValue = endTime.TotalSeconds > 0
                    ? (position.TotalSeconds / endTime.TotalSeconds) * 100.0
                    : 0;
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void UpdateCurrentLyric(TimeSpan position)
        {
            LyricsRequestSnapshot? request = _currentLyricsRequest;
            System.Collections.Generic.List<LyricLine>? lyrics = _currentLyrics;
            if (request is null || lyrics is null || lyrics.Count == 0 || !ShowLyrics)
            {
                return;
            }

            int newIndex = -1;
            for (int i = 0; i < lyrics.Count; i++)
            {
                if (position >= lyrics[i].StartTime)
                {
                    newIndex = i;
                }
                else
                {
                    break;
                }
            }

            _dispatcher.InvokeAsync(() =>
            {
                if (!IsLyricsRequestCurrent(request.Value)
                    || !Equals(request, _currentLyricsRequest)
                    || !ReferenceEquals(lyrics, _currentLyrics))
                {
                    return;
                }

                ApplyCurrentLyric(lyrics, newIndex);
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private async System.Threading.Tasks.Task FetchOrApplyCachedLyricsAsync(
            LyricsRequestSnapshot request,
            bool applyDelay)
        {
            if (!IsLyricsRequestCurrent(request))
            {
                return;
            }

            LyricsResult? cachedResult;
            lock (_lyricsRequestLock)
            {
                _lyricsCache.TryGetValue(request.CacheKey, out cachedResult);
            }
            if (cachedResult is not null)
            {
                QueueLyricsResult(request, cachedResult);
                return;
            }

            if (applyDelay)
            {
                await _lyricsRequestDelay();
            }
            if (!IsLyricsRequestCurrent(request))
            {
                return;
            }

            LyricsResult result = await _lyricsService.GetLyricsAsync(
                request.Track.Title,
                request.Track.Artist,
                request.Track.Album,
                request.DurationSeconds);
            if (IsLyricsRequestCurrent(request))
            {
                QueueLyricsResult(request, result);
            }
        }

        private LyricsRequestSnapshot CreateRequestForCurrentTrack()
        {
            lock (_lyricsRequestLock)
            {
                _lyricsRequestId++;
                var track = new LyricsTrackIdentity(
                    _currentTrackName,
                    _currentArtistName,
                    _currentAlbumName);
                return new LyricsRequestSnapshot(
                    _lyricsRequestId,
                    track,
                    _lastDurationSeconds,
                    new LyricsCacheKey(
                        track.Title,
                        track.Artist,
                        track.Album,
                        Math.Round(_lastDurationSeconds)));
            }
        }

        private void InvalidateLyricsRequests()
        {
            lock (_lyricsRequestLock)
            {
                _lyricsRequestId++;
            }
        }

        private bool IsLyricsRequestCurrent(LyricsRequestSnapshot request)
        {
            if (!ShowLyrics)
            {
                return false;
            }

            lock (_lyricsRequestLock)
            {
                return request.Generation == _lyricsRequestId
                    && request.Track.Matches(
                        _currentTrackName,
                        _currentArtistName,
                        _currentAlbumName);
            }
        }

        private bool IsTrackCurrent(LyricsTrackIdentity track)
        {
            lock (_lyricsRequestLock)
            {
                return track.Matches(
                    _currentTrackName,
                    _currentArtistName,
                    _currentAlbumName);
            }
        }

        private void QueueTrackState(
            LyricsTrackIdentity track,
            BitmapImage? albumArt)
        {
            _ = _dispatcher.InvokeAsync(() =>
            {
                if (!IsTrackCurrent(track))
                {
                    return;
                }

                Title = string.IsNullOrEmpty(track.Title) ? "Unknown" : track.Title;
                Artist = string.IsNullOrEmpty(track.Artist) ? "Unknown" : track.Artist;
                AlbumArt = albumArt;
            });
        }

        private void QueueLoadingState(LyricsRequestSnapshot request)
        {
            _ = _dispatcher.InvokeAsync(() =>
            {
                if (!IsLyricsRequestCurrent(request))
                {
                    return;
                }

                ClearLyricsState();
                CurrentLyric = LocalizationService.GetText(
                    SettingsService.Load().Language,
                    LocalizationKey.LoadingLyrics);
            });
        }

        private void QueueLyricsResult(
            LyricsRequestSnapshot request,
            LyricsResult result)
        {
            _ = _dispatcher.InvokeAsync(() =>
            {
                if (!IsLyricsRequestCurrent(request))
                {
                    return;
                }

                lock (_lyricsRequestLock)
                {
                    _lyricsCache[request.CacheKey] = result;
                }

                _currentLyricsRequest = request;
                _currentLyrics = result.Lyrics;
                RebuildLyricsList(result.Lyrics);
                if (result.Lyrics is { Count: > 0 } lyrics)
                {
                    ApplyCurrentLyric(lyrics, FindCurrentLyricIndex(lyrics, _lastPosition));
                    return;
                }

                CurrentLyric = result.Error switch
                {
                    LyricsResultError.NotFound => LocalizationService.GetText(
                        SettingsService.Load().Language,
                        LocalizationKey.LyricsNotFound),
                    LyricsResultError.NetworkError => LocalizationService.GetText(
                        SettingsService.Load().Language,
                        LocalizationKey.LyricsNetworkError),
                    _ => string.Empty
                };
            });
        }

        private void RebuildLyricsList(
            System.Collections.Generic.IReadOnlyList<LyricLine>? lyrics)
        {
            LyricsList.Clear();
            CurrentLyricIndex = -1;
            if (lyrics is null)
            {
                return;
            }

            foreach (LyricLine line in lyrics)
            {
                LyricsList.Add(new LyricLineViewModel(line));
            }
        }

        private static int FindCurrentLyricIndex(
            System.Collections.Generic.IReadOnlyList<LyricLine> lyrics,
            TimeSpan position)
        {
            int newIndex = -1;
            for (int i = 0; i < lyrics.Count; i++)
            {
                if (position < lyrics[i].StartTime)
                {
                    break;
                }

                newIndex = i;
            }

            return newIndex;
        }

        private void ApplyCurrentLyric(
            System.Collections.Generic.IReadOnlyList<LyricLine> lyrics,
            int newIndex)
        {
            if (newIndex != _currentLyricIndex)
            {
                if (_currentLyricIndex >= 0 && _currentLyricIndex < LyricsList.Count)
                {
                    LyricsList[_currentLyricIndex].IsCurrent = false;
                }

                CurrentLyricIndex = newIndex;
                if (_currentLyricIndex >= 0 && _currentLyricIndex < LyricsList.Count)
                {
                    LyricsList[_currentLyricIndex].IsCurrent = true;
                }
            }

            CurrentLyric = _currentLyricIndex >= 0 && _currentLyricIndex < lyrics.Count
                ? lyrics[_currentLyricIndex].Text
                : string.Empty;
        }

        private void ClearLyricsState()
        {
            _currentLyricsRequest = null;
            _currentLyrics = null;
            LyricsList.Clear();
            CurrentLyricIndex = -1;
            CurrentLyric = string.Empty;
        }

        private void RunOnDispatcher(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _dispatcher.Invoke(action);
        }

        private readonly record struct LyricsTrackIdentity(
            string Title,
            string Artist,
            string Album)
        {
            internal bool Matches(string title, string artist, string album) =>
                string.Equals(Title, title, StringComparison.Ordinal)
                && string.Equals(Artist, artist, StringComparison.Ordinal)
                && string.Equals(Album, album, StringComparison.Ordinal);
        }

        private readonly record struct LyricsCacheKey(
            string Title,
            string Artist,
            string Album,
            double DurationSeconds);

        private readonly record struct LyricsRequestSnapshot(
            int Generation,
            LyricsTrackIdentity Track,
            double DurationSeconds,
            LyricsCacheKey CacheKey);
    }

    public class DotItem : ViewModelBase
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int Index { get; set; }
    }
}
