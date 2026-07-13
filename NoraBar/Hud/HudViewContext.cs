namespace NoraBar.Hud;

/// <summary>
/// HUDのViewと推奨サイズを取得する際の表示条件を表します。
/// </summary>
/// <param name="PresentationState">Viewを表示する際のHUD表示状態。</param>
public readonly record struct HudViewContext(HudPresentationState PresentationState);
