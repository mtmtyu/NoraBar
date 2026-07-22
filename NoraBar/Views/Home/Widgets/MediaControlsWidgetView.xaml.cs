using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home.Widgets;

public partial class MediaControlsWidgetView : UserControl
{
    public MediaControlsWidgetView()
    {
        InitializeComponent();
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        DataTemplate? template = style switch
        {
            HomeWidgetStyle.MediaExpanded => Resources["MediaExpandedTemplate"] as DataTemplate,
            _ => Resources["MediaCompactTemplate"] as DataTemplate
        };

        if (template != null)
        {
            MediaContentControl.ContentTemplate = template;
        }
    }
}
