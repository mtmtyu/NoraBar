using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NoraBar.Services;
using NoraBar.Models;

namespace NoraBar.ViewModels
{
    public class MusicViewModel : ViewModelBase
    {
        private readonly MediaControlService _mediaService;
        private readonly AudioVisualizerService _audioVisualizerService;
        private readonly LyricsService _lyricsService;
        private System.Collections.Generic.List<LyricLine>? _currentLyrics;
        private double _lastDurationSeconds = 0;
        private TimeSpan _lastPosition = TimeSpan.Zero;
        private string _currentTrackName = "";
        private string _currentArtistName = "";
        private string _currentAlbumName = "";

        private readonly System.Collections.Generic.Dictionary<string, Services.LyricsResult> _lyricsCache = new();

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

        public ObservableCollection<DotItem> SessionDots { get; } = new ObservableCollection<DotItem>();

        private bool _showLyrics = true;
        public bool ShowLyrics
        {
            get => _showLyrics;
            set
            {
                if (SetProperty(ref _showLyrics, value))
                {
                    if (value && _currentLyrics == null && !string.IsNullOrEmpty(_currentTrackName))
                    {
                        int currentRequestId = ++_lyricsRequestId;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LoadingLyrics);
                        });
                        _ = FetchLyricsAsync(currentRequestId, _currentTrackName, _currentArtistName, _currentAlbumName);
                    }
                    else if (!value)
                    {
                        CurrentLyric = string.Empty;
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

        private int _lyricsRequestId = 0;

        public MusicViewModel()
        {
            _mediaService = new MediaControlService();
            _audioVisualizerService = new AudioVisualizerService();
            _lyricsService = new LyricsService();

            ShowLyrics = SettingsService.Load().ShowLyrics;

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

            _mediaService.MediaInfoChanged += async (s, e) =>
            {
                string newTrackName = e.Title ?? "";
                string newArtistName = e.Artist ?? "";
                string newAlbumName = e.AlbumTitle ?? "";

                if (_currentTrackName == newTrackName && _currentArtistName == newArtistName)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Title = string.IsNullOrEmpty(_currentTrackName) ? "Unknown" : _currentTrackName;
                        Artist = string.IsNullOrEmpty(_currentArtistName) ? "Unknown" : _currentArtistName;
                        AlbumArt = e.AlbumArt;
                    });
                    return;
                }

                int currentRequestId = ++_lyricsRequestId;

                _currentTrackName = newTrackName;
                _currentArtistName = newArtistName;
                _currentAlbumName = newAlbumName;
                
                string cacheKey = $"{_currentTrackName}|{_currentArtistName}";

                if (_lyricsCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    _currentLyrics = cachedResult.Lyrics;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Title = string.IsNullOrEmpty(_currentTrackName) ? "Unknown" : _currentTrackName;
                        Artist = string.IsNullOrEmpty(_currentArtistName) ? "Unknown" : _currentArtistName;
                        AlbumArt = e.AlbumArt;

                        if (_currentLyrics == null || _currentLyrics.Count == 0)
                        {
                            if (cachedResult.Error == Services.LyricsResultError.NotFound)
                            {
                                CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LyricsNotFound);
                            }
                            else if (cachedResult.Error == Services.LyricsResultError.NetworkError)
                            {
                                CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LyricsNetworkError);
                            }
                            else
                            {
                                CurrentLyric = "";
                            }
                        }
                    });

                    if (_currentLyrics != null && _currentLyrics.Count > 0)
                    {
                        UpdateCurrentLyric(_lastPosition);
                    }
                    return;
                }

                _currentLyrics = null;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Title = string.IsNullOrEmpty(_currentTrackName) ? "Unknown" : _currentTrackName;
                    Artist = string.IsNullOrEmpty(_currentArtistName) ? "Unknown" : _currentArtistName;
                    AlbumArt = e.AlbumArt;
                    CurrentLyric = SettingsService.Load().ShowLyrics ? LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LoadingLyrics) : "";
                });

                // Wait for 2 seconds (to reduce API requests during consecutive skips)
                await System.Threading.Tasks.Task.Delay(2000);

                await FetchLyricsAsync(currentRequestId, _currentTrackName, _currentArtistName, _currentAlbumName);
            };
            _mediaService.PlaybackStateChanged += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsPlaying = e.IsPlaying;
                });
            };

            _mediaService.MediaTimelineChanged += (s, e) =>
            {
                _lastPosition = e.Position;
                _lastDurationSeconds = e.EndTime.TotalSeconds;

                UpdateCurrentLyric(e.Position);

                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PositionText = e.Position.ToString(@"m\:ss");
                    DurationText = e.EndTime.ToString(@"m\:ss");
                    if (e.EndTime.TotalSeconds > 0)
                    {
                        ProgressValue = (e.Position.TotalSeconds / e.EndTime.TotalSeconds) * 100.0;
                    }
                    else
                    {
                        ProgressValue = 0;
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            };

            _mediaService.SessionsInfoChanged += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
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
            
            _ = _mediaService.InitializeAsync();

            _audioVisualizerService.SpectrumDataUpdated += (s, data) =>
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SpectrumData = data;
                }, System.Windows.Threading.DispatcherPriority.Render);
            };
            _audioVisualizerService.Start();
        }

        public void Cleanup()
        {
            _audioVisualizerService.Stop();
            _audioVisualizerService.Dispose();
        }

        public void RestartVisualizer()
        {
            _audioVisualizerService.Stop();
            _audioVisualizerService.Start();
        }

        private void UpdateCurrentLyric(TimeSpan position)
        {
            if (_currentLyrics == null || _currentLyrics.Count == 0 || !ShowLyrics)
            {
                return;
            }

            string currentText = "";
            for (int i = 0; i < _currentLyrics.Count; i++)
            {
                if (position >= _currentLyrics[i].StartTime)
                {
                    currentText = _currentLyrics[i].Text;
                }
                else
                {
                    break;
                }
            }

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (CurrentLyric != currentText)
                {
                    CurrentLyric = currentText;
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private async System.Threading.Tasks.Task FetchLyricsAsync(int currentRequestId, string tTitle, string tArtist, string tAlbum)
        {
            if (currentRequestId != _lyricsRequestId)
            {
                return;
            }

            if (!SettingsService.Load().ShowLyrics)
            {
                return;
            }

            var result = await _lyricsService.GetLyricsAsync(tTitle, tArtist, tAlbum, _lastDurationSeconds);
            
            if (currentRequestId != _lyricsRequestId)
            {
                return;
            }

            _currentLyrics = result.Lyrics;

            string cacheKey = $"{tTitle}|{tArtist}";
            _lyricsCache[cacheKey] = result;

            if (_currentLyrics == null || _currentLyrics.Count == 0)
            {
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (result.Error == LyricsResultError.NotFound)
                    {
                        CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LyricsNotFound);
                    }
                    else if (result.Error == LyricsResultError.NetworkError)
                    {
                        CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LyricsNetworkError);
                    }
                    else
                    {
                        CurrentLyric = "";
                    }
                });
            }
            else
            {
                UpdateCurrentLyric(_lastPosition);
            }
        }
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
