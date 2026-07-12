namespace NoraBar.Hud;

/// <summary>
/// HUDの現在の表示方法を表します。
/// </summary>
public enum HudPresentationState
{
    /// <summary>
    /// HUDの内容を閉じた状態です。
    /// </summary>
    Collapsed,

    /// <summary>
    /// HUDの内容を一時的に表示した状態です。
    /// </summary>
    Peek,

    /// <summary>
    /// HUDの内容を展開した状態です。
    /// </summary>
    Expanded,

    /// <summary>
    /// HUDの内容を展開したまま固定した状態です。
    /// </summary>
    Pinned
}
