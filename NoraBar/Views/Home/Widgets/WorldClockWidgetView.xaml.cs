using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class WorldClockWidgetView : UserControl
{
    public WorldClockWidgetView()
    {
        InitializeComponent();
        ClockContentControl.ContentTemplate = Resources["WorldClockListTemplate"] as DataTemplate;
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        ClockContentControl.ContentTemplate = Resources["WorldClockListTemplate"] as DataTemplate;
    }
}
