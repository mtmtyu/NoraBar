using System;
using System.Collections.Generic;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal static class HomeHudLayout
{
    internal static HudSize Calculate(HomeHudDesignVariant variant, IReadOnlyList<HomeWidgetConfig>? activeWidgets = null)
    {
        HudSize baseSize = variant switch
        {
            HomeHudDesignVariant.ActivityModules => new HudSize(800, 84),
            HomeHudDesignVariant.ClassicSystemOverlay => new HudSize(720, 120),
            HomeHudDesignVariant.FusionBalanced => new HudSize(700, 88),
            HomeHudDesignVariant.FusionExpressive => new HudSize(740, 108),
            _ => new HudSize(700, 88)
        };

        if (activeWidgets is null || activeWidgets.Count == 0)
        {
            return baseSize;
        }

        double totalWidth = 28;
        double maxHeight = 0;

        for (int i = 0; i < activeWidgets.Count; i++)
        {
            if (i > 0)
            {
                totalWidth += 21;
            }

            HomeWidgetConfig widget = activeWidgets[i];
            (double w, double h) = GetWidgetDimensions(widget.Style);
            totalWidth += w;
            if (h > maxHeight)
            {
                maxHeight = h;
            }
        }

        double finalHeight = Math.Max(baseSize.Height, maxHeight + 20);
        double finalWidth = Math.Max(baseSize.Width, totalWidth);

        return new HudSize(finalWidth, finalHeight);
    }

    private static (double Width, double Height) GetWidgetDimensions(HomeWidgetStyle style) => style switch
    {
        HomeWidgetStyle.ClockMinimal => (120, 40),
        HomeWidgetStyle.MediaCompact => (200, 40),
        HomeWidgetStyle.MediaArtworkHoverSmall => (160, 90),
        HomeWidgetStyle.MediaArtworkHover => (190, 105),
        HomeWidgetStyle.MediaArtworkHoverMedium => (190, 105),
        HomeWidgetStyle.MediaArtworkHoverLarge => (240, 135),
        HomeWidgetStyle.MediaBlurLyrics => (280, 130),
        _ => (150, 50)
    };
}
