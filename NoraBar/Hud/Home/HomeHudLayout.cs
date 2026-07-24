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

        double constrainedWidth = HomeWidgetLayoutMetrics.NormalizeMaxWidth(maxWidgetWidth);
        double constrainedHeight = HomeWidgetLayoutMetrics.NormalizeMaxHeight(maxWidgetHeight);

        if (activeWidgets is null || activeWidgets.Count == 0)
        {
            return new HudSize(
                Math.Min(constrainedWidth, baseSize.Width),
                Math.Min(constrainedHeight, baseSize.Height));
        }

        double contentWidth = Math.Max(
            1.0,
            constrainedWidth - HomeWidgetLayoutMetrics.RootHorizontalPadding);

        var itemSizes = new List<HomeWidgetLayoutSize>(activeWidgets.Count);
        foreach (HomeWidgetConfig widget in activeWidgets)
        {
            itemSizes.Add(HomeWidgetLayoutMetrics.GetSize(widget.Style));
        }

        HomeWidgetLayoutPlan plan = HomeWidgetLayoutMetrics.CreatePlan(
            itemSizes,
            contentWidth);

        double calculatedHeight =
            plan.ContentHeight + HomeWidgetLayoutMetrics.RootVerticalPadding;
        double finalHeight = Math.Min(
            constrainedHeight,
            Math.Max(baseSize.Height, calculatedHeight));

        // The configured maximum width is also the widget viewport width.
        // Using one width for both planning and rendering prevents later additions
        // from changing earlier row assignments.
        return new HudSize(constrainedWidth, finalHeight);
    }
}
