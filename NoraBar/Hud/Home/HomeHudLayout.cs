using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal static class HomeHudLayout
{
    internal static HudSize Calculate(HomeHudDesignVariant variant) => variant switch
    {
        HomeHudDesignVariant.ActivityModules => new HudSize(800, 84),
        HomeHudDesignVariant.ClassicSystemOverlay => new HudSize(720, 120),
        HomeHudDesignVariant.FusionBalanced => new HudSize(700, 88),
        HomeHudDesignVariant.FusionExpressive => new HudSize(740, 108),
        _ => new HudSize(700, 88)
    };
}
