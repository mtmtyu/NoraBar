using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class DigitalClockWidgetView : UserControl
{
    private const string ClockMinimalTemplateKey = "ClockMinimalTemplate";

    public DigitalClockWidgetView()
    {
        InitializeComponent();
        SetStyle(HomeWidgetStyle.ClockMinimal);
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        string templateKey = style switch
        {
            HomeWidgetStyle.ClockMinimal => ClockMinimalTemplateKey,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported digital clock style.")
        };
        ClockContentControl.ContentTemplate = (DataTemplate)Resources[templateKey];
    }
}
