using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class DigitalClockWidgetView : UserControl
{
    public DigitalClockWidgetView()
    {
        InitializeComponent();
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        DataTemplate? template = style switch
        {
            HomeWidgetStyle.ClockMinimal => Resources["ClockMinimalTemplate"] as DataTemplate,
            HomeWidgetStyle.ClockExpressive => Resources["ClockExpressiveTemplate"] as DataTemplate,
            HomeWidgetStyle.ClockBoldGradient => Resources["ClockBoldGradientTemplate"] as DataTemplate,
            _ => Resources["ClockMinimalTemplate"] as DataTemplate
        };

        if (template != null)
        {
            ClockContentControl.ContentTemplate = template;
        }
    }
}
