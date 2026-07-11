using System;
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
        private readonly VisualizerRestartCoordinator _visualizerRestartCoordinator = new();
        private System.Collections.Generic.List<LyricLine>? _currentLyrics;
        private double _lastDurationSeconds = 0;
        private TimeSpan _lastPosition = TimeSpan.Zero;
        private string _currentTrackName = "";
        private string _currentArtistName = "";
        private string _currentAlbumName = "";
        private int _mediaUpdateVersion;

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
                        int currentRequestId = System.Threading.Interlocked.Increment(ref _lyricsRequestId);
                        string trackName = _currentTrackName;
                        string artistName = _currentArtistName;
                        string albumName = _currentAlbumName;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            CurrentLyric = LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LoadingLyrics);
                        });
                        _ = FetchLyricsAsync(currentRequestId, trackName, artistName, albumName);
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
        public bool IsVisualizerRunning { get; private set; }

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

            _mediaService.MediaInfoChanged += async (s, e) =>
            {
                if (!TryAdvanceMediaUpdateVersion(e.UpdateVersion))
                {
                    return;
                }

                int currentRequestId = System.Threading.Interlocked.Increment(ref _lyricsRequestId);

                string trackName = e.Title ?? "";
                string artistName = e.Artist ?? "";
                string albumName = e.AlbumTitle ?? "";

                _currentTrackName = trackName;
                _currentArtistName = artistName;
                _currentAlbumName = albumName;

                _currentLyrics = null;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (e.UpdateVersion != System.Threading.Volatile.Read(ref _mediaUpdateVersion))
                    {
                        return;
                    }

                    Title = string.IsNullOrEmpty(trackName) ? "Unknown" : trackName;
                    Artist = string.IsNullOrEmpty(artistName) ? "Unknown" : artistName;
                    AlbumArt = null;
                    CurrentLyric = SettingsService.Load().ShowLyrics ? LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.LoadingLyrics) : "";
                });

                // Wait for 2 seconds (to reduce API requests during consecutive skips)
                await System.Threading.Tasks.Task.Delay(2000);

                await FetchLyricsAsync(currentRequestId, trackName, artistName, albumName);
            };
            _mediaService.AlbumArtChanged += (s, e) =>
            {
                if (e.UpdateVersion != System.Threading.Volatile.Read(ref _mediaUpdateVersion))
                {
                    return;
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (e.UpdateVersion != System.Threading.Volatile.Read(ref _mediaUpdateVersion))
                    {
                        return;
                    }

                    AlbumArt = e.AlbumArt;
                });
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

            _ = _mediaService.InitializeAsync();

            _audioVisualizerService.SpectrumDataUpdated += (s, data) =>
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SpectrumData = data;
                }, System.Windows.Threading.DispatcherPriority.Render);
            };
            IsVisualizerRunning = _audioVisualizerService.Start();
        }

        private bool TryAdvanceMediaUpdateVersion(int updateVersion)
        {
            while (true)
            {
                int currentVersion = System.Threading.Volatile.Read(ref _mediaUpdateVersion);
                if (updateVersion <= currentVersion)
                {
                    return false;
                }

                if (System.Threading.Interlocked.CompareExchange(
                        ref _mediaUpdateVersion,
                        updateVersion,
                        currentVersion) == currentVersion)
                {
                    return true;
                }
            }
        }

        public void Cleanup()
        {
            _mediaService.Dispose();
            _audioVisualizerService.Dispose();
            ClearSpectrumData();
        }

        public async System.Threading.Tasks.Task<bool> RestartVisualizerAsync()
        {
            IsVisualizerRunning = await _visualizerRestartCoordinator.RestartAsync(
                () => System.Threading.Tasks.Task.Run(() =>
                {
                    _audioVisualizerService.Stop();
                    return _audioVisualizerService.Start();
                }));
            if (!IsVisualizerRunning)
            {
                ClearSpectrumData();
            }

            return IsVisualizerRunning;
        }

        private void ClearSpectrumData()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SpectrumData = new float[SpectrumData.Length];
            });
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
}
