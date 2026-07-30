using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudViewModel : ViewModelBase, IHomeHudPresentationSource, IHomeWidgetPresentationSource
{
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(1);

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _clockTimer;
    private readonly Func<DateTimeOffset> _getNow;
    private readonly List<HomeWorldClockItemViewModel> _worldClockItems = new();
    private readonly ReadOnlyCollection<HomeWorldClockItemViewModel> _worldClockItemsReadOnly;
    private readonly List<WorldClockEntryViewModel> _worldClockSources = new();
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
        _worldClockItemsReadOnly = _worldClockItems.AsReadOnly();
    }

    public HomeHudDesignVariant DesignVariant => _viewModel.HomeHudDesignVariant;

    public object ViewDataContext => this;

    public Dispatcher OwningDispatcher => _clockTimer.Dispatcher;

    public MusicViewModel Music => _viewModel.Music;

    public bool HasMedia => Music.HasActiveSession;

    public bool HasNoMedia => !HasMedia;

    public string MediaTitle => HasMedia
        ? Music.Title
        : LocalizationService.GetText(
            _viewModel.SelectedLanguage,
            LocalizationKey.NoMediaPlaying);

    public string MediaArtist => HasMedia ? Music.Artist : string.Empty;

    public string PreviousMediaText => LocalizationService.GetText(
        _viewModel.SelectedLanguage,
        LocalizationKey.MediaPrevious);

    public string PlayPauseMediaText => LocalizationService.GetText(
        _viewModel.SelectedLanguage,
        Music.IsPlaying ? LocalizationKey.MediaPause : LocalizationKey.MediaPlay);

    public string NextMediaText => LocalizationService.GetText(
        _viewModel.SelectedLanguage,
        LocalizationKey.MediaNext);

    public string FirstWorldClockLabel => WorldClockItems.Count > 0 ? WorldClockItems[0].Label : string.Empty;

    public string SecondWorldClockLabel => WorldClockItems.Count > 1 ? WorldClockItems[1].Label : string.Empty;

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

    public string FirstWorldClockTimeText => WorldClockItems.Count > 0 ? WorldClockItems[0].TimeText : string.Empty;

    public string SecondWorldClockTimeText => WorldClockItems.Count > 1 ? WorldClockItems[1].TimeText : string.Empty;

    public IReadOnlyList<HomeWorldClockItemViewModel> WorldClockItems => _worldClockItemsReadOnly;

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

        var entries = _viewModel.WorldClockEntries;
        bool collectionChanged = entries.Count != _worldClockSources.Count;
        if (!collectionChanged)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i], _worldClockSources[i]))
                {
                    collectionChanged = true;
                    break;
                }
            }
        }

        // Synchronize _worldClockItems length with entries
        while (_worldClockItems.Count < entries.Count)
        {
            _worldClockItems.Add(new HomeWorldClockItemViewModel(string.Empty, string.Empty));
        }
        while (_worldClockItems.Count > entries.Count)
        {
            _worldClockItems.RemoveAt(_worldClockItems.Count - 1);
        }
        if (collectionChanged)
        {
            _worldClockSources.Clear();
            _worldClockSources.AddRange(entries);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var item = _worldClockItems[i];
            item.Label = entry.Label.ToUpperInvariant();
            item.TimeZoneId = entry.TimeZoneId;
            DateTimeOffset convertedNow = ConvertTime(now, entry.TimeZoneId);
            item.TimeText = HomeHudClockFormatter.FormatTime(convertedNow, format, systemCulture);
            item.DateText = HomeHudClockFormatter.FormatDate(convertedNow, _viewModel.SelectedLanguage);
        }

        OnPropertyChanged(nameof(FirstWorldClockLabel));
        OnPropertyChanged(nameof(SecondWorldClockLabel));
        OnPropertyChanged(nameof(FirstWorldClockTimeText));
        OnPropertyChanged(nameof(SecondWorldClockTimeText));
        if (collectionChanged)
        {
            OnPropertyChanged(nameof(WorldClockItems));
        }
    }

    private static DateTimeOffset ConvertTime(DateTimeOffset value, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.Local);
        }

        try
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
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

    public void UpdateActiveWidgets(IReadOnlyList<NoraBar.Hud.Home.Widgets.HomeWidgetConfig> widgets)
    {
        if (OverrideWidgets != null)
        {
            OverrideWidgets = widgets;
        }
        else
        {
            _viewModel.ActiveHomeWidgets = widgets;
        }
    }

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
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            RefreshClock();
            OnPropertyChanged(nameof(ActiveWidgets));
            OnPropertyChanged(nameof(MaxWidgetWidth));
            OnPropertyChanged(nameof(MaxWidgetHeight));
            OnPropertyChanged(nameof(IsWidgetEditMode));
            OnPropertyChanged(nameof(MediaTitle));
            OnPropertyChanged(nameof(PreviousMediaText));
            OnPropertyChanged(nameof(PlayPauseMediaText));
            OnPropertyChanged(nameof(NextMediaText));
            OnPropertyChanged(nameof(FirstWorldClockLabel));
            OnPropertyChanged(nameof(SecondWorldClockLabel));
            PresentationInvalidated?.Invoke(this, EventArgs.Empty);
            return;
        }

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
            case nameof(MainViewModel.FirstWorldClockLabel):
            case nameof(MainViewModel.SecondWorldClockLabel):
            case nameof(MainViewModel.SelectedLanguage):
                RefreshClock();
                OnPropertyChanged(nameof(MediaTitle));
                OnPropertyChanged(nameof(PreviousMediaText));
                OnPropertyChanged(nameof(PlayPauseMediaText));
                OnPropertyChanged(nameof(NextMediaText));
                break;
        }
    }

    private void Music_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(MusicViewModel.HasActiveSession)
                or nameof(MusicViewModel.Title)
                or nameof(MusicViewModel.Artist)
                or nameof(MusicViewModel.IsPlaying))
        {
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(HasNoMedia));
            OnPropertyChanged(nameof(MediaTitle));
            OnPropertyChanged(nameof(MediaArtist));
            OnPropertyChanged(nameof(PlayPauseMediaText));
        }
    }
}
