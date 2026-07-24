using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Views.Home;

public sealed class HomeWidgetPanel : Panel
{
    private static readonly Pen SeparatorPen = CreateSeparatorPen();
    private readonly List<(double X, double Top, double Bottom)> _separators = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var sizes = new List<HomeWidgetLayoutSize>(InternalChildren.Count);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            sizes.Add(new HomeWidgetLayoutSize(
                child.DesiredSize.Width,
                child.DesiredSize.Height));
        }

        HomeWidgetLayoutPlan plan = HomeWidgetLayoutMetrics.CreatePlan(
            sizes,
            availableSize.Width);

        return new Size(plan.ContentWidth, plan.ContentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sizes = new List<HomeWidgetLayoutSize>(InternalChildren.Count);
        foreach (UIElement child in InternalChildren)
        {
            sizes.Add(new HomeWidgetLayoutSize(
                child.DesiredSize.Width,
                child.DesiredSize.Height));
        }

        HomeWidgetLayoutPlan plan = HomeWidgetLayoutMetrics.CreatePlan(
            sizes,
            finalSize.Width);

        _separators.Clear();

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            UIElement child = InternalChildren[i];
            HomeWidgetLayoutPlacement placement = plan.Placements[i];

            child.Arrange(new Rect(
                placement.X,
                placement.Y,
                placement.Width,
                placement.Height));

            if (placement.HasLeadingSeparator)
            {
                double separatorX = placement.X - (HomeWidgetLayoutMetrics.InterWidgetSpacing / 2.0);
                double top = placement.RowTop + 4.0;
                double bottom = placement.RowTop + Math.Max(4.0, placement.RowHeight - 4.0);
                _separators.Add((separatorX, top, bottom));
            }
        }

        return finalSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        foreach ((double x, double top, double bottom) in _separators)
        {
            drawingContext.DrawLine(
                SeparatorPen,
                new Point(x, top),
                new Point(x, bottom));
        }
    }

    private static Pen CreateSeparatorPen()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
        brush.Freeze();

        var pen = new Pen(brush, 1.0);
        pen.Freeze();
        return pen;
    }
}
