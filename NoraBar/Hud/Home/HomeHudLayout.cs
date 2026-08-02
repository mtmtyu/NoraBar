using System.Collections.Generic;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Hud.Home;

internal static class HomeHudLayout
{
    private static readonly HudSize DefaultSize = new(700, 88);

    internal static HudSize Calculate(
        IReadOnlyList<HomeWidgetConfig>? activeWidgets = null,
        double maxWidgetWidth = 800,
        double maxWidgetHeight = 300)
    {
        double constrainedWidth = HomeWidgetLayoutMetrics.NormalizeMaxWidth(maxWidgetWidth);
        double constrainedHeight = HomeWidgetLayoutMetrics.NormalizeMaxHeight(maxWidgetHeight);

        if (activeWidgets is null || activeWidgets.Count == 0)
        {
            return new HudSize(
                Math.Min(constrainedWidth, DefaultSize.Width),
                Math.Min(constrainedHeight, DefaultSize.Height));
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
            Math.Max(DefaultSize.Height, calculatedHeight));

        // The configured maximum width is also the widget viewport width.
        // Using one width for both planning and rendering prevents later additions
        // from changing earlier row assignments.
        return new HudSize(constrainedWidth, finalHeight);
    }
}
