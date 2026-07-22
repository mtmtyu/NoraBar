using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class WorldClockWidgetView : UserControl
{
    public WorldClockWidgetView()
    {
        InitializeComponent();
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        DataTemplate? template = style switch
        {
            HomeWidgetStyle.WorldClockDualCard => Resources["WorldClockDualCardTemplate"] as DataTemplate,
            _ => Resources["WorldClockCompactTemplate"] as DataTemplate
        };

        if (template != null)
        {
            WorldClockContentControl.ContentTemplate = template;
        }
    }
}
