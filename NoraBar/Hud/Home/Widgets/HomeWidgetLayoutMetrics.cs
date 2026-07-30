using System;
using System.Collections.Generic;

namespace NoraBar.Hud.Home.Widgets;

internal readonly record struct HomeWidgetLayoutSize(double Width, double Height);

internal readonly record struct HomeWidgetLayoutPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    double RowTop,
    double RowHeight,
    bool HasLeadingSeparator);

internal sealed class HomeWidgetLayoutPlan
{
    internal HomeWidgetLayoutPlan(
        IReadOnlyList<HomeWidgetLayoutPlacement> placements,
        double contentWidth,
        double contentHeight)
    {
        Placements = placements;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
    }

    internal IReadOnlyList<HomeWidgetLayoutPlacement> Placements { get; }

    internal double ContentWidth { get; }

    internal double ContentHeight { get; }
}

internal static class HomeWidgetLayoutMetrics
{
    internal const double RootHorizontalPadding = 24.0;
    internal const double RootVerticalPadding = 16.0;
    internal const double InterWidgetSpacing = 21.0;

    private const double DefaultMaxWidth = 800.0;
    private const double DefaultMaxHeight = 300.0;

    internal static double NormalizeMaxWidth(double value) =>
        NormalizeConstraint(value, DefaultMaxWidth, RootHorizontalPadding + 1.0);

    internal static double NormalizeMaxHeight(double value) =>
        NormalizeConstraint(value, DefaultMaxHeight, RootVerticalPadding + 1.0);

    internal static HomeWidgetLayoutSize GetSize(HomeWidgetStyle style) => style switch
    {
        HomeWidgetStyle.ClockMinimal => new HomeWidgetLayoutSize(160, 40),
        HomeWidgetStyle.MediaCompact => new HomeWidgetLayoutSize(236, 40),
        HomeWidgetStyle.MediaArtworkHoverSmall => new HomeWidgetLayoutSize(158, 89),
        HomeWidgetStyle.MediaArtworkHover => new HomeWidgetLayoutSize(198, 109),
        HomeWidgetStyle.MediaArtworkHoverMedium => new HomeWidgetLayoutSize(198, 109),
        HomeWidgetStyle.MediaArtworkHoverLarge => new HomeWidgetLayoutSize(248, 139),
        HomeWidgetStyle.MediaBlurLyrics => new HomeWidgetLayoutSize(288, 134),
        _ => new HomeWidgetLayoutSize(150, 50)
    };

    internal static HomeWidgetLayoutPlan CreatePlan(
        IReadOnlyList<HomeWidgetLayoutSize> itemSizes,
        double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(itemSizes);

        double widthLimit = double.IsFinite(availableWidth)
            ? Math.Max(1.0, availableWidth)
            : double.PositiveInfinity;

        var placements = new List<HomeWidgetLayoutPlacement>(itemSizes.Count);
        var pendingRow = new List<(HomeWidgetLayoutSize Size, double X)>();

        double rowWidth = 0;
        double rowHeight = 0;
        double contentWidth = 0;
        double currentY = 0;

        void FlushRow()
        {
            if (pendingRow.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pendingRow.Count; i++)
            {
                (HomeWidgetLayoutSize size, double x) = pendingRow[i];
                double y = currentY + Math.Max(0, (rowHeight - size.Height) / 2.0);
                placements.Add(new HomeWidgetLayoutPlacement(
                    x,
                    y,
                    size.Width,
                    size.Height,
                    currentY,
                    rowHeight,
                    i > 0));
            }

            contentWidth = Math.Max(contentWidth, rowWidth);
            currentY += rowHeight;
            pendingRow.Clear();
            rowWidth = 0;
            rowHeight = 0;
        }

        foreach (HomeWidgetLayoutSize rawSize in itemSizes)
        {
            var size = new HomeWidgetLayoutSize(
                NormalizeDimension(rawSize.Width),
                NormalizeDimension(rawSize.Height));

            double spacing = pendingRow.Count > 0 ? InterWidgetSpacing : 0;
            if (pendingRow.Count > 0 && rowWidth + spacing + size.Width > widthLimit)
            {
                FlushRow();
                spacing = 0;
            }

            double x = rowWidth + spacing;
            pendingRow.Add((size, x));
            rowWidth = x + size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        FlushRow();
        return new HomeWidgetLayoutPlan(placements.AsReadOnly(), contentWidth, currentY);
    }

    private static double NormalizeConstraint(double value, double fallback, double minimum)
    {
        if (!double.IsFinite(value))
        {
            return fallback;
        }

        return Math.Max(minimum, value);
    }

    private static double NormalizeDimension(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Max(0, value);
    }
}
