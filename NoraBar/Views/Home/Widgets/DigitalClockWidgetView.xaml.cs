using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class DigitalClockWidgetView : UserControl
{
    public DigitalClockWidgetView()
    {
        InitializeComponent();
        ClockContentControl.ContentTemplate = Resources["ClockMinimalTemplate"] as DataTemplate;
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        ClockContentControl.ContentTemplate = Resources["ClockMinimalTemplate"] as DataTemplate;
    }
}
