using System.Windows;
using NoraBar.Models;
using NoraBar.Views.Home;
using NoraBar.Views.Home.Design20;
using NoraBar.Views.Home.Design21;
using NoraBar.Views.Home.Design22B;
using NoraBar.Views.Home.Design22C;

namespace NoraBar.Hud.Home;

internal static class HomeHudViewFactory
{
    internal static FrameworkElement Create(HomeHudDesignVariant variant)
    {
        return new DynamicWidgetHomeView();
    }
}
