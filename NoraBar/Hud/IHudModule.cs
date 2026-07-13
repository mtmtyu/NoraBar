using System.Windows;

namespace NoraBar.Hud;

/// <summary>
/// HUDとして表示できる機能の初期化、選択ライフサイクル、View提供を定義します。
/// </summary>
public interface IHudModule : IAsyncDisposable
{
    /// <summary>
    /// モジュールを一意に識別するIDを取得します。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// モジュールの表示用メタデータを取得します。
    /// </summary>
    HudModuleMetadata Metadata { get; }

    /// <summary>
    /// 現在の表示状態のままViewを再評価する必要が生じたときに発生します。
    /// </summary>
    event EventHandler? PresentationInvalidated;

    /// <summary>
    /// モジュールを初めて利用するための初期化を行います。
    /// </summary>
    /// <param name="cancellationToken">初期化のキャンセルを通知するトークン。</param>
    /// <returns>初期化処理を表す非同期操作。</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// モジュールが現在のHUDとして選択された際の処理を行います。
    /// </summary>
    /// <param name="cancellationToken">選択処理のキャンセルを通知するトークン。</param>
    /// <returns>選択処理を表す非同期操作。</returns>
    ValueTask ActivateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// モジュールが現在のHUDではなくなる際の処理を行います。
    /// </summary>
    /// <param name="cancellationToken">選択解除処理のキャンセルを通知するトークン。</param>
    /// <returns>選択解除処理を表す非同期操作。</returns>
    ValueTask DeactivateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 指定された表示条件に対応するViewを取得します。
    /// </summary>
    /// <param name="context">Viewを選択するための表示条件。</param>
    /// <returns>HUDに表示するView。</returns>
    FrameworkElement GetView(HudViewContext context);

    /// <summary>
    /// 指定された表示条件に対応する推奨サイズを取得します。
    /// </summary>
    /// <param name="context">推奨サイズを決定するための表示条件。</param>
    /// <returns>HUDの推奨表示サイズ。</returns>
    HudSize GetPreferredSize(HudViewContext context);
}
