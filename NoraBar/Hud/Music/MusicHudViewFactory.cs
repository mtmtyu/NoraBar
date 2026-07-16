using System.Windows;
using NoraBar.Models;
using NoraBar.Views.Island.DesignA_Minimal;
using NoraBar.Views.Island.DesignB_Productivity;
using NoraBar.Views.Island.DesignC_LyricsFocus;

namespace NoraBar.Hud.Music;

internal static class MusicHudViewFactory
{
    internal static FrameworkElement Create(DesignVariant variant)
    {
        return variant switch
        {
            DesignVariant.MinimalFloatingPill => new DesignAMusicView(),
            DesignVariant.ProductivityCommandIsland => new DesignBMusicView(),
            DesignVariant.LyricsFocusedSidebar => new DesignCMusicView(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                "未対応の音楽HUDデザインです。")
        };
    }
}
