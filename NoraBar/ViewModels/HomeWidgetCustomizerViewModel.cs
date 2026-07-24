using System.Collections.ObjectModel;
using System.Windows.Input;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Services;

namespace NoraBar.ViewModels;

public sealed class HomeWidgetCustomizerItemViewModel : ViewModelBase
{
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
        (HomeWidgetType.DigitalClock, _) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetDigitalClock),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaCompact),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverSmall) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaArtworkSmall),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverMedium) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaArtworkMedium),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaArtworkLarge),
        (HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaBlurLyrics),
        (HomeWidgetType.MediaControls, _) => LocalizationService.GetText(SettingsService.Load().Language, LocalizationKey.WidgetMediaControls),
        _ => Type.ToString()
    };

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

    public HomeWidgetCustomizerItemViewModel(string id, HomeWidgetType type, HomeWidgetStyle style)
    {
        Id = id;
        Type = type;
        Style = style;
    }

    public HomeWidgetConfig ToConfig() => new HomeWidgetConfig(Id, Type, Style);
}

public sealed class HomeWidgetCustomizerViewModel : ViewModelBase
{
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

    private static string T(LocalizationKey key) =>
        LocalizationService.GetText(SettingsService.Load().Language, key);

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
        double maxWidgetHeight = 300)
    {
        _maxWidgetWidth = maxWidgetWidth;
        _maxWidgetHeight = maxWidgetHeight;

        ActiveWidgets = new ObservableCollection<HomeWidgetCustomizerItemViewModel>(
            currentWidgets.Select(w => new HomeWidgetCustomizerItemViewModel(w.Id, w.Type, w.Style)));

        foreach (HomeWidgetCustomizerItemViewModel item in ActiveWidgets)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }
        ActiveWidgets.CollectionChanged += ActiveWidgets_CollectionChanged;

        CatalogWidgets = new ObservableCollection<HomeWidgetCustomizerItemViewModel>
        {
            new("catalog_clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("catalog_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact),
            new("catalog_media_artwork_sm", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverSmall),
            new("catalog_media_artwork_md", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverMedium),
            new("catalog_media_artwork_lg", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge),
            new("catalog_media_blur_lyrics", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        AddWidgetCommand = new RelayCommand(p =>
        {
            if (p is HomeWidgetCustomizerItemViewModel item)
            {
                string newId = $"widget_{item.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
                ActiveWidgets.Add(new HomeWidgetCustomizerItemViewModel(newId, item.Type, item.Style));
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
