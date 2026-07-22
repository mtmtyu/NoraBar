using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Views.Home.Widgets;

namespace NoraBar.Views.Home;

public partial class DynamicWidgetHomeView : UserControl
{
    public DynamicWidgetHomeView()
    {
        InitializeComponent();
        DataContextChanged += DynamicWidgetHomeView_DataContextChanged;
    }

    private void DynamicWidgetHomeView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        RebuildWidgets();
    }

    public void RebuildWidgets()
    {
        WidgetsContainer.Children.Clear();

        if (DataContext is not HomeHudViewModel vm || vm.ActiveWidgets is null)
        {
            return;
        }

        bool first = true;
        foreach (HomeWidgetConfig widget in vm.ActiveWidgets)
        {
            if (!first)
            {
                Border separator = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.White,
                    Opacity = 0.12,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    Margin = new Thickness(10, 4, 10, 4)
                };
                WidgetsContainer.Children.Add(separator);
            }
            first = false;

            UIElement? element = CreateWidgetElement(widget, vm);
            if (element != null)
            {
                WidgetsContainer.Children.Add(element);
            }
        }
    }

    private static UIElement? CreateWidgetElement(HomeWidgetConfig widget, HomeHudViewModel vm)
    {
        switch (widget.Type)
        {
            case HomeWidgetType.DigitalClock:
                DigitalClockWidgetView clockView = new DigitalClockWidgetView { DataContext = vm };
                clockView.SetStyle(widget.Style);
                return clockView;

            case HomeWidgetType.WorldClock:
                WorldClockWidgetView worldClockView = new WorldClockWidgetView { DataContext = vm };
                worldClockView.SetStyle(widget.Style);
                return worldClockView;

            case HomeWidgetType.MediaControls:
                MediaControlsWidgetView mediaView = new MediaControlsWidgetView { DataContext = vm };
                mediaView.SetStyle(widget.Style);
                return mediaView;

            case HomeWidgetType.SystemStatus:
                SystemStatusWidgetView systemView = new SystemStatusWidgetView { DataContext = vm };
                systemView.SetStyle(widget.Style);
                return systemView;

            default:
                return null;
        }
    }
}
