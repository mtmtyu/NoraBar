namespace NoraBar.Hud;

/// <summary>
/// HUDの切り替えに失敗し、復旧処理を行ったことを表します。
/// </summary>
public sealed class HudNavigationException : Exception
{
    public HudNavigationException(
        string targetHudId,
        Exception navigationException,
        IReadOnlyList<Exception>? recoveryExceptions = null)
        : base($"Failed to navigate to HUD '{targetHudId}'.", navigationException)
    {
        TargetHudId = targetHudId;
        RecoveryExceptions = recoveryExceptions?.ToArray() ?? [];
    }

    public string TargetHudId { get; }

    public IReadOnlyList<Exception> RecoveryExceptions { get; }
}
