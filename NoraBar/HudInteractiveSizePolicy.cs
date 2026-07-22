using System.Windows;
using NoraBar.Hud;

namespace NoraBar;

internal readonly record struct HudInteractiveSizeTargets(
    HudSize ContainerSize,
    HudSize ContentSize,
    HorizontalAlignment ContentHorizontalAlignment);

internal static class HudInteractiveSizePolicy
{
    internal static HudInteractiveSizeTargets ResolveTargets(
        HudSize preferredContentSize,
        HudSize desiredContainerSize,
        HudSize currentContainerSize,
        bool isPointerOver,
        bool usesRightRailNavigation)
    {
        HudSize containerSize = ResolveTarget(
            desiredContainerSize,
            currentContainerSize,
            isPointerOver);
        HorizontalAlignment contentHorizontalAlignment = usesRightRailNavigation
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Center;
        return new HudInteractiveSizeTargets(
            containerSize,
            preferredContentSize,
            contentHorizontalAlignment);
    }

    internal static void ApplyContentLayout(
        FrameworkElement contentHost,
        HudInteractiveSizeTargets targets)
    {
        ArgumentNullException.ThrowIfNull(contentHost);

        contentHost.Width = targets.ContentSize.Width;
        contentHost.Height = targets.ContentSize.Height;
        contentHost.HorizontalAlignment = targets.ContentHorizontalAlignment;
    }

    internal static HudSize ResolveTarget(
        HudSize desiredSize,
        HudSize currentSize,
        bool isPointerOver)
    {
        if (!isPointerOver)
        {
            return desiredSize;
        }

        return new HudSize(
            Math.Max(desiredSize.Width, currentSize.Width),
            Math.Max(desiredSize.Height, currentSize.Height));
    }
}
