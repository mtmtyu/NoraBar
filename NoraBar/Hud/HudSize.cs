namespace NoraBar.Hud;

/// <summary>
/// HUDの推奨表示サイズを表します。
/// </summary>
/// <param name="Width">推奨する幅。</param>
/// <param name="Height">推奨する高さ。</param>
public readonly record struct HudSize(double Width, double Height);
