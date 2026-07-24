using System;
using System.Collections.Generic;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal static class HomeHudLayout
{
    internal static HudSize Calculate(
        HomeHudDesignVariant variant,
        IReadOnlyList<HomeWidgetConfig>? activeWidgets = null,
        double maxWidgetWidth = 800,
        double maxWidgetHeight = 300)
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

        const double horizontalPadding = 24.0;
        const double verticalPadding = 16.0;
        const double separatorWidth = 21.0;
        const double lineSpacing = 8.0;

        double contentMaxWidthLimit = Math.Max(150.0, maxWidgetWidth - horizontalPadding);

        double overallContentWidth = 0;
        double overallContentHeight = 0;

        double currentLineWidth = 0;
        double currentLineHeight = 0;

        for (int i = 0; i < activeWidgets.Count; i++)
        {
            HomeWidgetConfig widget = activeWidgets[i];
            (double w, double h) = GetWidgetDimensions(widget.Style);

            double spacingNeeded = currentLineWidth > 0 ? separatorWidth : 0;

            if (currentLineWidth > 0 && (currentLineWidth + spacingNeeded + w) > contentMaxWidthLimit)
            {
                // Wrap to next line
                overallContentWidth = Math.Max(overallContentWidth, currentLineWidth);
                overallContentHeight += currentLineHeight + lineSpacing;

                currentLineWidth = w;
                currentLineHeight = h;
            }
            else
            {
                currentLineWidth += spacingNeeded + w;
                currentLineHeight = Math.Max(currentLineHeight, h);
            }
        }

        if (currentLineWidth > 0)
        {
            overallContentWidth = Math.Max(overallContentWidth, currentLineWidth);
            overallContentHeight += currentLineHeight;
        }

        double calculatedWidth = overallContentWidth + horizontalPadding;
        double calculatedHeight = overallContentHeight + verticalPadding;

        double finalWidth = Math.Min(maxWidgetWidth, Math.Max(baseSize.Width, calculatedWidth));
        double finalHeight = Math.Min(maxWidgetHeight, Math.Max(baseSize.Height, calculatedHeight));

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
