using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudViewModel : ViewModelBase, IHomeHudPresentationSource
{
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(1);

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _clockTimer;
    private readonly Func<DateTimeOffset> _getNow;
    private bool _isInitialized;
    private bool _isDisposed;

    internal HomeHudViewModel(MainViewModel viewModel)
        : this(
            viewModel,
            new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = ClockInterval
            },
            static () => DateTimeOffset.Now)
    {
    }

    internal HomeHudViewModel(
        MainViewModel viewModel,
        DispatcherTimer clockTimer,
        Func<DateTimeOffset> getNow)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(clockTimer);
        ArgumentNullException.ThrowIfNull(getNow);
        _viewModel = viewModel;
        _clockTimer = clockTimer;
        _getNow = getNow;
    }

    public HomeHudDesignVariant DesignVariant => _viewModel.HomeHudDesignVariant;

    public object ViewDataContext => this;

    public MusicViewModel Music => _viewModel.Music;

    public bool HasMedia => Music.HasActiveSession;

    public bool HasNoMedia => !HasMedia;

    public string MediaTitle => HasMedia
        ? Music.Title
        : _viewModel.SelectedLanguage == AppLanguage.Japanese
            ? "再生中のメディアはありません"
            : "No media playing";

    public string MediaArtist => HasMedia ? Music.Artist : string.Empty;

    public string FirstWorldClockLabel => _viewModel.FirstWorldClockLabel.ToUpperInvariant();

    public string SecondWorldClockLabel => _viewModel.SecondWorldClockLabel.ToUpperInvariant();

    private string _localTimeText = string.Empty;
    public string LocalTimeText
    {
        get => _localTimeText;
        private set => SetProperty(ref _localTimeText, value);
    }

    private string _localDateText = string.Empty;
    public string LocalDateText
    {
        get => _localDateText;
        private set => SetProperty(ref _localDateText, value);
    }

    private string _firstWorldClockTimeText = string.Empty;
    public string FirstWorldClockTimeText
    {
        get => _firstWorldClockTimeText;
        private set => SetProperty(ref _firstWorldClockTimeText, value);
    }

    private string _secondWorldClockTimeText = string.Empty;
    public string SecondWorldClockTimeText
    {
        get => _secondWorldClockTimeText;
        private set => SetProperty(ref _secondWorldClockTimeText, value);
    }

    public event EventHandler? PresentationInvalidated;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isInitialized)
        {
            return;
        }

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Music.PropertyChanged += Music_PropertyChanged;
        _clockTimer.Tick += ClockTimer_Tick;
        _isInitialized = true;
        RefreshClock();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        RefreshClock();
        _clockTimer.Start();
    }

    public void Stop() => _clockTimer.Stop();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _clockTimer.Stop();
        if (_isInitialized)
        {
            _clockTimer.Tick -= ClockTimer_Tick;
            Music.PropertyChanged -= Music_PropertyChanged;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _isInitialized = false;
        }

        _isDisposed = true;
    }

    private void ClockTimer_Tick(object? sender, EventArgs e) => RefreshClock();

    private void RefreshClock()
    {
        DateTimeOffset now = _getNow();
        HomeHudTimeFormat format = _viewModel.HomeHudTimeFormat;
        CultureInfo systemCulture = CultureInfo.CurrentCulture;
        LocalTimeText = HomeHudClockFormatter.FormatTime(now, format, systemCulture);
        LocalDateText = HomeHudClockFormatter.FormatDate(now, _viewModel.SelectedLanguage);
        FirstWorldClockTimeText = HomeHudClockFormatter.FormatTime(
            ConvertTime(now, _viewModel.FirstWorldClockTimeZoneId),
            format,
            systemCulture);
        SecondWorldClockTimeText = HomeHudClockFormatter.FormatTime(
            ConvertTime(now, _viewModel.SecondWorldClockTimeZoneId),
            format,
            systemCulture);
    }

    private static DateTimeOffset ConvertTime(DateTimeOffset value, string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Utc);
        }
    }

    private IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig>? _overrideWidgets;
    public IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig>? OverrideWidgets
    {
        get => _overrideWidgets;
        set
        {
            if (SetProperty(ref _overrideWidgets, value))
            {
                OnPropertyChanged(nameof(ActiveWidgets));
                PresentationInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> ActiveWidgets => OverrideWidgets ?? _viewModel.ActiveHomeWidgets;

    private double? _overrideMaxWidgetWidth;
    public double? OverrideMaxWidgetWidth
    {
        get => _overrideMaxWidgetWidth;
        set
        {
            if (SetProperty(ref _overrideMaxWidgetWidth, value))
            {
                OnPropertyChanged(nameof(MaxWidgetWidth));
                PresentationInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private double? _overrideMaxWidgetHeight;
    public double? OverrideMaxWidgetHeight
    {
        get => _overrideMaxWidgetHeight;
        set
        {
            if (SetProperty(ref _overrideMaxWidgetHeight, value))
            {
                OnPropertyChanged(nameof(MaxWidgetHeight));
                PresentationInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double MaxWidgetWidth => OverrideMaxWidgetWidth ?? _viewModel.MaxWidgetWidth;
    public double MaxWidgetHeight => OverrideMaxWidgetHeight ?? _viewModel.MaxWidgetHeight;

    public bool IsWidgetEditMode => _viewModel.IsWidgetEditMode;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.HomeHudDesignVariant):
            case nameof(MainViewModel.ActiveHomeWidgets):
            case nameof(MainViewModel.MaxWidgetWidth):
            case nameof(MainViewModel.MaxWidgetHeight):
            case nameof(MainViewModel.IsWidgetEditMode):
                PresentationInvalidated?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(ActiveWidgets));
                OnPropertyChanged(nameof(MaxWidgetWidth));
                OnPropertyChanged(nameof(MaxWidgetHeight));
                OnPropertyChanged(nameof(IsWidgetEditMode));
                break;
            case nameof(MainViewModel.HomeHudTimeFormat):
            case nameof(MainViewModel.FirstWorldClockTimeZoneId):
            case nameof(MainViewModel.SecondWorldClockTimeZoneId):
            case nameof(MainViewModel.SelectedLanguage):
                RefreshClock();
                OnPropertyChanged(nameof(MediaTitle));
                break;
            case nameof(MainViewModel.FirstWorldClockLabel):
                OnPropertyChanged(nameof(FirstWorldClockLabel));
                break;
            case nameof(MainViewModel.SecondWorldClockLabel):
                OnPropertyChanged(nameof(SecondWorldClockLabel));
                break;
        }
    }

    private void Music_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(MusicViewModel.HasActiveSession)
                or nameof(MusicViewModel.Title)
                or nameof(MusicViewModel.Artist))
        {
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(HasNoMedia));
            OnPropertyChanged(nameof(MediaTitle));
            OnPropertyChanged(nameof(MediaArtist));
        }
    }
}
