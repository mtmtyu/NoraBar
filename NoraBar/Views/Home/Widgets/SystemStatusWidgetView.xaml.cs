using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class SystemStatusWidgetView : UserControl
{
    public SystemStatusWidgetView()
    {
        InitializeComponent();
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        DataTemplate? template = style switch
        {
            HomeWidgetStyle.SystemGauge => Resources["SystemGaugeTemplate"] as DataTemplate,
            _ => Resources["SystemMinimalTemplate"] as DataTemplate
        };

        if (template != null)
        {
            SystemContentControl.ContentTemplate = template;
        }
    }
}
