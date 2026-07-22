using System.Windows;
using NoraBar.Models;
using NoraBar.Views.Home;

namespace NoraBar.Hud.Home;

internal static class HomeHudViewFactory
{
    internal static FrameworkElement Create(HomeHudDesignVariant variant)
    {
        return new DynamicWidgetHomeView();
    }
}
