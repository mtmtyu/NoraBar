namespace NoraBar.Hud;

/// <summary>
/// HUDルーターの一時点における整合した状態を表します。
/// </summary>
/// <param name="CurrentHudId">現在選択されているHUDのID。選択前または終了後は<c>null</c>です。</param>
/// <param name="CurrentModule">現在選択されているHUDモジュール。選択前または終了後は<c>null</c>です。</param>
/// <param name="PresentationState">現在の表示状態。</param>
/// <param name="IsInitialized">ルーターの初期化が完了しているかどうか。</param>
/// <param name="IsShuttingDown">ルーターが終了処理中かどうか。</param>
public readonly record struct HudRouterSnapshot(
    string? CurrentHudId,
    IHudModule? CurrentModule,
    HudPresentationState PresentationState,
    bool IsInitialized,
    bool IsShuttingDown);
