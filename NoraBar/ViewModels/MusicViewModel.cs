using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NoraBar.Services;

namespace NoraBar.ViewModels
{
    public class MusicViewModel : ViewModelBase
    {
        private readonly MediaControlService _mediaService;
        private readonly AudioVisualizerService _audioVisualizerService;

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

        public ICommand PlayPauseCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }

        public MusicViewModel()
        {
            _mediaService = new MediaControlService();
            _audioVisualizerService = new AudioVisualizerService();

            PlayPauseCommand = new RelayCommand(async _ => await _mediaService.PlayPauseAsync());
            NextCommand = new RelayCommand(async _ => await _mediaService.NextAsync());
            PreviousCommand = new RelayCommand(async _ => await _mediaService.PreviousAsync());

            _mediaService.MediaInfoChanged += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Title = string.IsNullOrEmpty(e.Title) ? "Unknown" : e.Title;
                    Artist = string.IsNullOrEmpty(e.Artist) ? "Unknown" : e.Artist;
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
            _audioVisualizerService.Start();
        }

        public void Cleanup()
        {
            _audioVisualizerService.Stop();
            _audioVisualizerService.Dispose();
        }
    }
}
