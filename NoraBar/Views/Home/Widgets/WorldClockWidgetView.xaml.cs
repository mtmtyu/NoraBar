using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class WorldClockWidgetView : UserControl
{
    public WorldClockWidgetView()
    {
        InitializeComponent();
        ClockContentControl.ContentTemplate = Resources["WorldClockMinimalTemplate"] as DataTemplate;
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        string templateKey = style switch
        {
            HomeWidgetStyle.WorldClockList => "WorldClockListTemplate",
            _ => "WorldClockMinimalTemplate"
        };
        ClockContentControl.ContentTemplate = Resources[templateKey] as DataTemplate;
    }
}
