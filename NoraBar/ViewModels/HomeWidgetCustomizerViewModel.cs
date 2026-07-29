using System.Collections.ObjectModel;
using System.Windows.Input;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using NoraBar.Services;

namespace NoraBar.ViewModels;

public sealed class HomeWidgetCustomizerItemViewModel : ViewModelBase
{
    private readonly AppLanguage _language;
    internal AppLanguage Language => _language;
    public string Id { get; }
    public HomeWidgetType Type { get; }

    private HomeWidgetStyle _style;
    public HomeWidgetStyle Style
    {
        get => _style;
        set
        {
            if (SetProperty(ref _style, value))
            {
                OnPropertyChanged(nameof(AvailableStyles));
            }
        }
    }

    public string Title => (Type, Style) switch
    {
        (HomeWidgetType.DigitalClock, _) => T(LocalizationKey.WidgetDigitalClock),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact) => T(LocalizationKey.WidgetMediaCompact),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverSmall) => T(LocalizationKey.WidgetMediaArtworkSmall),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverMedium) => T(LocalizationKey.WidgetMediaArtworkMedium),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge) => T(LocalizationKey.WidgetMediaArtworkLarge),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics) => T(LocalizationKey.WidgetMediaBlurLyrics),
        (HomeWidgetType.MediaControls, _) => T(LocalizationKey.WidgetMediaControls),
        _ => Type.ToString()
    };

    public string MoveUpText => T(LocalizationKey.MoveUp);
    public string MoveDownText => T(LocalizationKey.MoveDown);
    public string RemoveText => T(LocalizationKey.RemoveWidget);
    public string IconText => Type switch
    {
        HomeWidgetType.DigitalClock => "\uE814",
        HomeWidgetType.MediaControls => "\uE93C",
        _ => "\uE700"
    };

    public IReadOnlyList<HomeWidgetStyle> AvailableStyles => Type switch
    {
        HomeWidgetType.DigitalClock => [HomeWidgetStyle.ClockMinimal],
        HomeWidgetType.MediaControls => [
            HomeWidgetStyle.MediaCompact,
            HomeWidgetStyle.MediaArtworkHoverSmall,
            HomeWidgetStyle.MediaArtworkHoverMedium,
            HomeWidgetStyle.MediaArtworkHoverLarge,
            HomeWidgetStyle.MediaBlurLyrics
        ],
        _ => [Style]
    };

    public HomeWidgetCustomizerItemViewModel(
        string id,
        HomeWidgetType type,
        HomeWidgetStyle style,
        AppLanguage? language = null)
    {
        Id = id;
        Type = type;
        Style = style;
        _language = language ?? SettingsService.Load().Language;
    }

    private string T(LocalizationKey key) => LocalizationService.GetText(_language, key);

    public HomeWidgetConfig ToConfig() => new HomeWidgetConfig(Id, Type, Style);
}

public sealed class HomeWidgetCustomizerViewModel : ViewModelBase
{
    private readonly AppLanguage _language;
    public ObservableCollection<HomeWidgetCustomizerItemViewModel> ActiveWidgets { get; }
    public ObservableCollection<HomeWidgetCustomizerItemViewModel> CatalogWidgets { get; }

    public string TitleText => T(LocalizationKey.WidgetCustomizerTitle);
    public string HeaderText => T(LocalizationKey.WidgetCustomizerHeader);
    public string HeaderDescriptionText => T(LocalizationKey.WidgetCustomizerHeaderDescription);
    public string LivePreviewText => T(LocalizationKey.WidgetLivePreview);
    public string MaxWidgetWidthLabelText => T(LocalizationKey.MaxWidgetWidthLabel);
    public string MaxWidgetHeightLabelText => T(LocalizationKey.MaxWidgetHeightLabel);
    public string ActiveWidgetsHeaderText => T(LocalizationKey.ActiveWidgetsHeader);
    public string AddWidgetsHeaderText => T(LocalizationKey.AddWidgetsHeader);
    public string AddWidgetButtonText => T(LocalizationKey.AddWidgetButton);
    public string CancelButtonText => T(LocalizationKey.Cancel);
    public string SaveAndApplyButtonText => T(LocalizationKey.SaveAndApply);

    private string T(LocalizationKey key) => LocalizationService.GetText(_language, key);

    public ICommand AddWidgetCommand { get; }
    public ICommand RemoveWidgetCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public event EventHandler? PreviewInvalidated;

    private double _maxWidgetWidth;
    public double MaxWidgetWidth
    {
        get => _maxWidgetWidth;
        set
        {
            if (SetProperty(ref _maxWidgetWidth, value))
            {
                PreviewInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private double _maxWidgetHeight;
    public double MaxWidgetHeight
    {
        get => _maxWidgetHeight;
        set
        {
            if (SetProperty(ref _maxWidgetHeight, value))
            {
                PreviewInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public HomeWidgetCustomizerViewModel(
        IEnumerable<HomeWidgetConfig> currentWidgets,
        double maxWidgetWidth = 800,
        double maxWidgetHeight = 300,
        AppLanguage? language = null)
    {
        _language = language ?? SettingsService.Load().Language;
        _maxWidgetWidth = maxWidgetWidth;
        _maxWidgetHeight = maxWidgetHeight;

        ActiveWidgets = new ObservableCollection<HomeWidgetCustomizerItemViewModel>(
            currentWidgets.Select(w => new HomeWidgetCustomizerItemViewModel(w.Id, w.Type, w.Style, _language)));

        foreach (HomeWidgetCustomizerItemViewModel item in ActiveWidgets)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }
        ActiveWidgets.CollectionChanged += ActiveWidgets_CollectionChanged;

        CatalogWidgets = new ObservableCollection<HomeWidgetCustomizerItemViewModel>
        {
            new("catalog_clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal, _language),
            new("catalog_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact, _language),
            new("catalog_media_artwork_sm", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverSmall, _language),
            new("catalog_media_artwork_md", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverMedium, _language),
            new("catalog_media_artwork_lg", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge, _language),
            new("catalog_media_blur_lyrics", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics, _language)
        };

        AddWidgetCommand = new RelayCommand(p =>
        {
            if (p is HomeWidgetCustomizerItemViewModel item)
            {
                string newId = $"widget_{item.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
                ActiveWidgets.Add(new HomeWidgetCustomizerItemViewModel(newId, item.Type, item.Style, _language));
            }
        });

        RemoveWidgetCommand = new RelayCommand(p =>
        {
            if (p is HomeWidgetCustomizerItemViewModel item)
            {
                ActiveWidgets.Remove(item);
            }
        });

        MoveUpCommand = new RelayCommand(p =>
        {
            if (p is HomeWidgetCustomizerItemViewModel item)
            {
                int index = ActiveWidgets.IndexOf(item);
                if (index > 0)
                {
                    ActiveWidgets.Move(index, index - 1);
                }
            }
        });

        MoveDownCommand = new RelayCommand(p =>
        {
            if (p is HomeWidgetCustomizerItemViewModel item)
            {
                int index = ActiveWidgets.IndexOf(item);
                if (index >= 0 && index < ActiveWidgets.Count - 1)
                {
                    ActiveWidgets.Move(index, index + 1);
                }
            }
        });
    }

    private void ActiveWidgets_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (HomeWidgetCustomizerItemViewModel item in e.NewItems.OfType<HomeWidgetCustomizerItemViewModel>())
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (HomeWidgetCustomizerItemViewModel item in e.OldItems.OfType<HomeWidgetCustomizerItemViewModel>())
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }
        PreviewInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        PreviewInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < ActiveWidgets.Count && newIndex >= 0 && newIndex < ActiveWidgets.Count && oldIndex != newIndex)
        {
            ActiveWidgets.Move(oldIndex, newIndex);
        }
    }

    public IReadOnlyList<HomeWidgetConfig> GetResultConfigs()
    {
        return ActiveWidgets.Select(item => item.ToConfig()).ToList().AsReadOnly();
    }
}
