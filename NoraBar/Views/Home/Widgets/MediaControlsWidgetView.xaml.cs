using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class MediaControlsWidgetView : UserControl
{
    public MediaControlsWidgetView()
    {
        InitializeComponent();
        MediaContentControl.ContentTemplate = Resources["MediaCompactTemplate"] as DataTemplate;
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        MediaContentControl.ContentTemplate = Resources["MediaCompactTemplate"] as DataTemplate;
    }
}
