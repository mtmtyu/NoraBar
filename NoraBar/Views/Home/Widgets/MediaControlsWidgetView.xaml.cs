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
        MediaContentControl.ContentTemplate = style switch
        {
            HomeWidgetStyle.MediaArtworkHoverSmall => Resources["MediaArtworkHoverSmallTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHoverMedium => Resources["MediaArtworkHoverMediumTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHoverLarge => Resources["MediaArtworkHoverLargeTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHover => Resources["MediaArtworkHoverTemplate"] as DataTemplate,
            _ => Resources["MediaCompactTemplate"] as DataTemplate
        };
    }
}
