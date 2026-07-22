using NoraBar.Hud;

namespace NoraBar;

internal static class HudInteractiveSizePolicy
{
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
