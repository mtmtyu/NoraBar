using System.Collections.ObjectModel;
using System.Windows.Input;
using NoraBar.Hud.Home.Widgets;

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

    public string Title => Type switch
    {
        HomeWidgetType.DigitalClock => "Digital Clock",
        HomeWidgetType.WorldClock => "World Clock",
        HomeWidgetType.MediaControls => "Media Controls",
        HomeWidgetType.SystemStatus => "System Status",
        _ => Type.ToString()
    };

    public string IconText => Type switch
    {
        HomeWidgetType.DigitalClock => "\uE814",
        HomeWidgetType.WorldClock => "\uE912",
        HomeWidgetType.MediaControls => "\uE93C",
        HomeWidgetType.SystemStatus => "\uE9D9",
        _ => "\uE700"
    };

    public IReadOnlyList<HomeWidgetStyle> AvailableStyles => Type switch
    {
        HomeWidgetType.DigitalClock => [HomeWidgetStyle.ClockMinimal, HomeWidgetStyle.ClockExpressive, HomeWidgetStyle.ClockBoldGradient],
        HomeWidgetType.WorldClock => [HomeWidgetStyle.WorldClockCompact, HomeWidgetStyle.WorldClockDualCard],
        HomeWidgetType.MediaControls => [HomeWidgetStyle.MediaCompact, HomeWidgetStyle.MediaExpanded],
        HomeWidgetType.SystemStatus => [HomeWidgetStyle.SystemMinimal, HomeWidgetStyle.SystemGauge],
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

    public ICommand AddWidgetCommand { get; }
    public ICommand RemoveWidgetCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public event EventHandler? PreviewInvalidated;

    public HomeWidgetCustomizerViewModel(IEnumerable<HomeWidgetConfig> currentWidgets)
    {
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
            new("catalog_worldclock", HomeWidgetType.WorldClock, HomeWidgetStyle.WorldClockCompact),
            new("catalog_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact),
            new("catalog_system", HomeWidgetType.SystemStatus, HomeWidgetStyle.SystemMinimal)
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
