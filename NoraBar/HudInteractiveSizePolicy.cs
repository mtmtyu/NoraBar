using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud;

namespace NoraBar;

internal readonly record struct HudInteractiveSizeTargets(
    HudSize ContainerSize,
    HudSize ContentSize,
    bool StretchesContentWidth,
    bool StretchesContentHeight);

internal static class HudInteractiveSizePolicy
{
    internal static HudInteractiveSizeTargets ResolveTargets(
        HudSize preferredContentSize,
        HudSize desiredContainerSize,
        HudSize currentContainerSize,
        bool isPointerOver)
    {
        HudSize containerSize = ResolveTarget(
            desiredContainerSize,
            currentContainerSize,
            isPointerOver);
        return new HudInteractiveSizeTargets(
            containerSize,
            preferredContentSize,
            desiredContainerSize.Width > currentContainerSize.Width,
            desiredContainerSize.Height > currentContainerSize.Height);
    }

    internal static void ApplyContentLayout(
        ContentControl contentHost,
        HudInteractiveSizeTargets targets)
    {
        ArgumentNullException.ThrowIfNull(contentHost);

        contentHost.Width = targets.StretchesContentWidth
            ? double.NaN
            : targets.ContentSize.Width;
        contentHost.Height = targets.StretchesContentHeight
            ? double.NaN
            : targets.ContentSize.Height;
        contentHost.HorizontalAlignment = targets.StretchesContentWidth
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Center;
        contentHost.VerticalAlignment = targets.StretchesContentHeight
            ? VerticalAlignment.Stretch
            : VerticalAlignment.Top;
        contentHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        contentHost.VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    internal static HudSize ResolveTarget(
        HudSize desiredSize,
        HudSize currentSize,
        bool isPointerOver)
    {
        return desiredSize;
    }
}
