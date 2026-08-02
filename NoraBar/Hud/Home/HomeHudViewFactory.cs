using System.Windows;
using NoraBar.Views.Home;

namespace NoraBar.Hud.Home;

internal static class HomeHudViewFactory
{
    internal static FrameworkElement Create()
    {
        return new DynamicWidgetHomeView();
    }
}
