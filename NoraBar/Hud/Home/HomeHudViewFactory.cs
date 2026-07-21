using System.Windows;
using NoraBar.Models;
using NoraBar.Views.Home.Design20;
using NoraBar.Views.Home.Design21;
using NoraBar.Views.Home.Design22B;
using NoraBar.Views.Home.Design22C;

namespace NoraBar.Hud.Home;

internal static class HomeHudViewFactory
{
    internal static FrameworkElement Create(HomeHudDesignVariant variant) => variant switch
    {
        HomeHudDesignVariant.ActivityModules => new ActivityModulesView(),
        HomeHudDesignVariant.ClassicSystemOverlay => new ClassicSystemOverlayView(),
        HomeHudDesignVariant.FusionBalanced => new FusionBalancedView(),
        HomeHudDesignVariant.FusionExpressive => new FusionExpressiveView(),
        _ => new FusionBalancedView()
    };
}
