using NoraBar.Models;

namespace NoraBar.Hud.Music;

internal static class MusicHudDesignVariantResolver
{
    internal static DesignVariant Resolve(DesignVariant variant)
    {
        return variant switch
        {
            DesignVariant.MinimalFloatingPill => variant,
            DesignVariant.ProductivityCommandIsland => variant,
            DesignVariant.LyricsFocusedSidebar => variant,
            _ => DesignVariant.MinimalFloatingPill
        };
    }
}
