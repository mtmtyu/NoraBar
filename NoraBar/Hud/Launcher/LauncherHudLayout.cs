namespace NoraBar.Hud.Launcher;

internal static class LauncherHudLayout
{
    private static readonly HudSize PeekSize = new(520, 92);
    private static readonly HudSize ExpandedSize = new(760, 520);

    internal static HudSize Calculate(HudPresentationState state) => state switch
    {
        HudPresentationState.Collapsed => new HudSize(200, 2),
        HudPresentationState.Peek => PeekSize,
        HudPresentationState.Expanded or HudPresentationState.Pinned => ExpandedSize,
        _ => ExpandedSize
    };
}
