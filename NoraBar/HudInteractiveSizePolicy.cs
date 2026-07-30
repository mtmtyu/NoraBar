using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud;

namespace NoraBar;

internal readonly record struct HudInteractiveSizeTargets(
    HudSize ContainerSize,
    HudSize ContentSize,
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
            desiredContainerSize.Height > currentContainerSize.Height);
    }

    internal static void ApplyContentLayout(
        ContentControl contentHost,
        HudInteractiveSizeTargets targets)
    {
        ArgumentNullException.ThrowIfNull(contentHost);

        contentHost.Width = targets.ContentSize.Width;
        contentHost.Height = targets.StretchesContentHeight
            ? double.NaN
            : targets.ContentSize.Height;
        contentHost.HorizontalAlignment = HorizontalAlignment.Center;
        contentHost.VerticalAlignment = targets.StretchesContentHeight
            ? VerticalAlignment.Stretch
            : VerticalAlignment.Center;
        contentHost.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        contentHost.VerticalContentAlignment = VerticalAlignment.Stretch;
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
