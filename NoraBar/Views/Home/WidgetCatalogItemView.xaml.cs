using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;
using NoraBar.Views.Home.Widgets;

namespace NoraBar.Views.Home;

public partial class WidgetCatalogItemView : UserControl
{
    public WidgetCatalogItemView()
    {
        InitializeComponent();
        DataContextChanged += WidgetCatalogItemView_DataContextChanged;
    }

    private void WidgetCatalogItemView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is HomeWidgetCustomizerItemViewModel item)
        {
            BuildPreviewWidget(item);
        }
    }

    private void BuildPreviewWidget(HomeWidgetCustomizerItemViewModel item)
    {
        MainViewModel dummyVm = new MainViewModel();

        UIElement? previewView = null;
        switch (item.Type)
        {
            case HomeWidgetType.DigitalClock:
                DigitalClockWidgetView clockView = new DigitalClockWidgetView { DataContext = dummyVm };
                clockView.SetStyle(item.Style);
                previewView = clockView;
                break;

            case HomeWidgetType.MediaControls:
                MediaControlsWidgetView mediaView = new MediaControlsWidgetView { DataContext = dummyVm };
                mediaView.SetStyle(item.Style);
                previewView = mediaView;
                break;
        }

        PreviewContentHost.Content = previewView;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeWidgetCustomizerItemViewModel item)
        {
            FrameworkElement? p = this;
            while (p != null && p is not WidgetPaletteOverlayView)
            {
                p = VisualTreeHelper.GetParent(p) as FrameworkElement;
            }

            if (p is WidgetPaletteOverlayView palette && palette.DataContext is MainViewModel mainVm)
            {
                string newId = $"widget_{item.Type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
                List<HomeWidgetConfig> current = mainVm.ActiveHomeWidgets.ToList();
                current.Add(new HomeWidgetConfig(newId, item.Type, item.Style));
                mainVm.ActiveHomeWidgets = current.AsReadOnly();
            }
        }
    }
}
