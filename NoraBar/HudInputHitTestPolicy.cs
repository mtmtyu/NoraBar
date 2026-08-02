using System.Windows;

namespace NoraBar;

internal static class HudInputHitTestPolicy
{
    internal static bool IsInteractive(
        Point windowPoint,
        Rect hudBounds,
        Rect? widgetPaletteBounds)
    {
        return hudBounds.Contains(windowPoint)
            || widgetPaletteBounds?.Contains(windowPoint) == true;
    }
}