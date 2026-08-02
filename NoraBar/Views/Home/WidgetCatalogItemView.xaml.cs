using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;
using NoraBar.Views.Home.Widgets;

namespace NoraBar.Views.Home;

public partial class WidgetCatalogItemView : UserControl
{
    private IDisposable? _previewView;
    private WidgetCatalogPreviewViewModel? _previewSource;

    public WidgetCatalogItemView()
    {
        InitializeComponent();
        DataContextChanged += WidgetCatalogItemView_DataContextChanged;
        Loaded += WidgetCatalogItemView_Loaded;
        Unloaded += WidgetCatalogItemView_Unloaded;
    }

    private void WidgetCatalogItemView_Loaded(object sender, RoutedEventArgs e)
    {
        if (PreviewContentHost.Content is null
            && DataContext is HomeWidgetCustomizerItemViewModel item)
        {
            BuildPreviewWidget(item);
        }
    }

    private void WidgetCatalogItemView_Unloaded(object sender, RoutedEventArgs e) =>
        ReleasePreview();

    private void WidgetCatalogItemView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        ReleasePreview();
        if (IsLoaded && e.NewValue is HomeWidgetCustomizerItemViewModel item)
        {
            BuildPreviewWidget(item);
        }
    }

    private void BuildPreviewWidget(HomeWidgetCustomizerItemViewModel item)
    {
        var source = new WidgetCatalogPreviewViewModel(item.Language);
        UIElement? previewView = item.Type switch
        {
            HomeWidgetType.DigitalClock => CreateClockPreview(item, source),
            HomeWidgetType.WorldClock => CreateWorldClockPreview(item, source),
            HomeWidgetType.MediaControls => CreateMediaPreview(item, source),
            _ => null
        };

        _previewSource = source;
        _previewView = previewView as IDisposable;
        PreviewContentHost.Content = previewView;
    }

    private static DigitalClockWidgetView CreateClockPreview(
        HomeWidgetCustomizerItemViewModel item,
        WidgetCatalogPreviewViewModel source)
    {
        var view = new DigitalClockWidgetView { DataContext = source };
        view.SetStyle(item.Style);
        return view;
    }

    private static WorldClockWidgetView CreateWorldClockPreview(
        HomeWidgetCustomizerItemViewModel item,
        WidgetCatalogPreviewViewModel source)
    {
        var view = new WorldClockWidgetView { DataContext = source };
        view.SetStyle(item.Style);
        return view;
    }

    private static MediaControlsWidgetView CreateMediaPreview(
        HomeWidgetCustomizerItemViewModel item,
        WidgetCatalogPreviewViewModel source)
    {
        var view = new MediaControlsWidgetView { DataContext = source };
        view.SetStyle(item.Style);
        return view;
    }

    private void ReleasePreview()
    {
        IDisposable? previewView = _previewView;
        WidgetCatalogPreviewViewModel? previewSource = _previewSource;
        BestEffortResourceReleaser.ReleaseAllAndReport(
            static exception => Trace.TraceError(
                $"Widget catalog preview cleanup failed: {exception}"),
            () => PreviewContentHost.Content = null,
            () => _previewView = null,
            () => _previewSource = null,
            () => previewView?.Dispose(),
            () => previewSource?.Dispose());
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeWidgetCustomizerItemViewModel item)
        {
            return;
        }

        FrameworkElement? parent = this;
        while (parent is not null && parent is not WidgetPaletteOverlayView)
        {
            parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
        }

        if (parent is WidgetPaletteOverlayView palette
            && palette.DataContext is MainViewModel mainViewModel)
        {
            string newId = $"widget_{item.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
            List<HomeWidgetConfig> current = mainViewModel.ActiveHomeWidgets.ToList();
            current.Add(new HomeWidgetConfig(newId, item.Type, item.Style));
            mainViewModel.ActiveHomeWidgets = current.AsReadOnly();
        }
    }
}

internal sealed class WidgetCatalogPreviewViewModel : IMusicChangeSource, IDisposable
{
    private static readonly ICommand NoOpCommand = new RelayCommand(_ => { });

    internal WidgetCatalogPreviewViewModel(AppLanguage language)
    {
        MediaTitle = LocalizationService.GetText(language, LocalizationKey.NoMediaPlaying);
        PreviousMediaText = LocalizationService.GetText(language, LocalizationKey.MediaPrevious);
        PlayPauseMediaText = LocalizationService.GetText(language, LocalizationKey.MediaPlay);
        NextMediaText = LocalizationService.GetText(language, LocalizationKey.MediaNext);
        DateTimeOffset now = DateTimeOffset.Now;
        LocalTimeText = now.ToString("HH:mm");
        LocalDateText = now.ToString("ddd, MMM d");
    }

    public WidgetCatalogPreviewViewModel Music => this;
    public bool HasMedia => false;
    public string MediaTitle { get; }
    public string MediaArtist => string.Empty;
    public string PreviousMediaText { get; }
    public string PlayPauseMediaText { get; }
    public string NextMediaText { get; }
    public string LocalTimeText { get; }
    public string LocalDateText { get; }
    public string FirstWorldClockLabel => string.Empty;
    public string SecondWorldClockLabel => string.Empty;
    public string FirstWorldClockTimeText => string.Empty;
    public string SecondWorldClockTimeText => string.Empty;
    public IReadOnlyList<HomeWorldClockItemViewModel> WorldClockItems { get; } =
    [
        new HomeWorldClockItemViewModel("NYC", "Eastern Standard Time") { TimeText = "14:05", DateText = "Thu, Jul 30" },
        new HomeWorldClockItemViewModel("LON", "GMT Standard Time") { TimeText = "19:05", DateText = "Thu, Jul 30" }
    ];
    public BitmapImage? AlbumArt => null;
    public bool IsPlaying => false;
    public string CurrentLyric => string.Empty;
    public ObservableCollection<LyricLineViewModel> LyricsList { get; } = [];
    public int CurrentLyricIndex => -1;
    public ICommand PlayPauseCommand => NoOpCommand;
    public ICommand PreviousCommand => NoOpCommand;
    public ICommand NextCommand => NoOpCommand;
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public void Dispose() { }
}
