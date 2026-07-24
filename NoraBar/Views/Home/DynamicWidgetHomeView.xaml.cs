using System.ComponentModel;
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
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }

        RebuildWidgets();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HomeHudViewModel.ActiveWidgets)
            or nameof(HomeHudViewModel.MaxWidgetWidth)
            or nameof(HomeHudViewModel.MaxWidgetHeight))
        {
            RebuildWidgets();
        }
    }

    public void RebuildWidgets()
    {
        WidgetsContainer.Children.Clear();

        if (DataContext is not HomeHudViewModel vm || vm.ActiveWidgets is null)
        {
            return;
        }

        WidgetsScrollViewer.MaxHeight = Math.Max(50.0, vm.MaxWidgetHeight - 16.0);
        WidgetsContainer.MaxWidth = Math.Max(100.0, vm.MaxWidgetWidth - 24.0);

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

            case HomeWidgetType.MediaControls:
                MediaControlsWidgetView mediaView = new MediaControlsWidgetView { DataContext = vm };
                mediaView.SetStyle(widget.Style);
                return mediaView;

            default:
                return null;
        }
    }
}
