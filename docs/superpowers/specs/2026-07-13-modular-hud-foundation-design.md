# NoraBar Phase 0 モジュラーHUD基盤 設計

## 目的

現在の音楽専用HUDを、組み込みHUDと将来の外部プラグインを共通契約で扱える基盤へ段階移行する。Phase 0では既存の音楽HUDだけを組み込みモジュールとして登録し、表示、操作、設定、アニメーション、音楽サービスの起動時期を維持する。

## 採用方針

既存の `MainViewModel` を音楽Viewの互換表示コンテキストとして維持する。音楽Viewの既存バインディングを全面変更せず、HUD固有のView選択、推奨サイズ、表示再評価、ライフサイクルを `MusicHudModule` へ移す。

この段階移行により、`MainWindow` から音楽固有の型と条件分岐を除去しつつ、既存XAMLと音楽サービスの回帰リスクを抑える。

## 全体構成

```text
App（Composition Root）
  ├─ MainViewModel
  ├─ MusicHudModule
  ├─ HudRegistry
  ├─ HudRouter
  └─ MainWindow
       ├─ HudRouterの状態を監視
       ├─ 現在モジュールのPresentationInvalidatedを監視
       ├─ 現在モジュールからViewと推奨サイズを取得
       └─ 位置、DPI、全画面、入力、アニメーション、トレイを担当
```

`App.xaml` の `StartupUri` は削除する。`App.OnStartup` が依存オブジェクトを明示的に生成し、constructor injectionで接続する。新しいDIコンテナや本番用パッケージは追加しない。

## HUD識別子と表示状態

HUDの種類は安定した文字列IDで表す。組み込みIDは `BuiltInHudIds.Music` のような定数に集約し、値は `music` とする。ID比較には `StringComparer.Ordinal` を使用する。

表示状態はHUD IDから独立した `HudPresentationState` で表す。

```csharp
public enum HudPresentationState
{
    Collapsed,
    Peek,
    Expanded,
    Pinned
}
```

- `Collapsed`: 高さ2px程度の待機表示
- `Peek`: 将来の一時的な小型表示
- `Expanded`: 通常操作可能な表示
- `Pinned`: マウス離脱で折りたたまれない表示

Phase 0の通常操作では主に `Collapsed` と `Expanded` を使う。表示状態の変更はモジュールの選択状態を変えない。

## HUDモジュール契約

`IHudModule` は次を提供する。

- 安定したIDと表示用メタデータ
- 表示コンテキストに応じたView
- 表示コンテキストに応じた推奨サイズ
- 初期化、アクティブ化、非アクティブ化、非同期破棄
- Viewまたはレイアウトの再評価を要求する `PresentationInvalidated`

Viewとサイズの問い合わせには、表示状態などを含む明示的なコンテキスト型を渡す。折りたたみサイズとアニメーション時間はモジュール固有値にせず、シェル側の一か所に集約する。

公開する将来拡張用契約には簡潔なXMLドキュメントを付ける。WPF型を含む契約はPhase 0では本体プロジェクト内に置き、不要な抽象化プロジェクトは増やさない。

## HudRegistry

`HudRegistry` はモジュールの手動登録、ID検索、登録順列挙、破棄を担当する。

- null、空白、規約に反するIDを拒否する
- `StringComparer.Ordinal` で重複IDを早期検出する
- 登録順を保持する
- 登録済みモジュールを一度ずつ破棄する
- アセンブリスキャン、外部DLLロード、フォルダー監視は行わない

## HudRouter

`HudRouter` は有効HUD一覧、現在HUD、表示状態、モジュールの選択ライフサイクルを一元管理する。初期化、切り替え、無効化、終了は `SemaphoreSlim` などで直列化し、重複した非同期遷移を防ぐ。

ルーター変更通知には、少なくとも現在HUDまたは表示状態の変更をMainWindowが識別できる情報を含める。MainWindowは通知のたびに次を再取得する。

```text
CurrentModule
CurrentHudId
PresentationState
GetView(...)
GetPreferredSize(...)
```

不明または無効なHUDへの遷移は、有効な既定HUDへフォールバックする。現在HUDを無効化する場合は、無効化前に遷移先を解決する。有効HUDが0件になる状態は許可せず、Phase 0では `music` を自動的に有効化する。

## 起動時ライフサイクル

```text
App.OnStartup
  → MainViewModel生成
  → MusicHudModule生成
  → HudRegistry生成・MusicHudModule登録
  → HudRouter生成
  → HudRouter.InitializeAsync
       → 既定HUDを解決
       → MusicHudModule.InitializeAsync（初回のみ）
       → MusicHudModule.ActivateAsync
       → CurrentHud確定
  → MainWindow生成
  → MainWindowがHudRouterを購読
  → MainWindowが現在モジュールのPresentationInvalidatedを購読
  → Viewと推奨サイズを初回評価
  → MainWindow表示
```

既存Mutex、起動引数、通常起動時の設定画面、更新確認を維持する。音楽サービスの開始タイミングは大幅に変更しない。

## 表示状態の変更

```text
MouseEnter
  → PresentationState = Expanded
  → Router変更通知
  → MainWindowがViewと推奨サイズを再評価

MouseLeave
  → PinnedでなければPresentationState = Collapsed
  → Router変更通知
  → MainWindowが折りたたみ表示
```

`Collapsed`、`Peek`、`Expanded`、`Pinned` 間の変更では `ActivateAsync` と `DeactivateAsync` を呼ばない。位置編集モードでは既存と同様に展開状態を維持する。全画面抑制はシェルが展開描画を抑止するが、モジュールの選択ライフサイクルは変更しない。

## HUD切り替え

```text
NavigateTo(newHudId)
  → 遷移先HUDを検証し、必要ならフォールバックを解決
  → 旧モジュールのPresentationInvalidated購読解除
  → 旧モジュール.DeactivateAsync
  → 新モジュール.InitializeAsync（初回のみ）
  → 新モジュールのPresentationInvalidatedを購読
  → 新モジュール.ActivateAsync
  → CurrentHudを確定
  → Router変更通知
  → MainWindowがViewと推奨サイズを必ず再評価
```

新モジュールの購読は `ActivateAsync` より前に行い、アクティブ化中の無効化通知を取りこぼさない。旧モジュールの購読解除は `DeactivateAsync` より前に行う。初期化、アクティブ化、非アクティブ化は可能な限り冪等にする。

ルーターはモジュールの無効化通知を集約するが、MainWindowも現在モジュールの通知元を明示的に管理し、モジュール切り替え時に古い購読を必ず解除する。

## 終了時ライフサイクル

```text
App shutdown
  → MainWindowがHudRouterの購読を解除
  → MainWindowが現在モジュールのPresentationInvalidated購読を解除
  → HudRouter.ShutdownAsync
       → 現在モジュール.DeactivateAsync
  → HudRegistry.DisposeAsync
       → 登録済みモジュールを一度ずつDisposeAsync
       → MusicHudModule.DisposeAsync内でMusicViewModel.Cleanup相当を一度だけ実行
  → MainWindow、トレイ、その他シェル資源を破棄
```

`MusicViewModel.Cleanup` の所有者は `MusicHudModule.DisposeAsync` だけとする。`MainWindow.OnClosed`、`HudRouter`、`App.OnExit` は直接呼び出さない。破棄処理は冪等にし、イベント購読解除とCleanupの重複を防ぐ。

WPFの終了イベントは同期境界を含むため、制御された非同期終了フローを先に完了させてからウィンドウを閉じる。無制御なfire-and-forget、`.Result`、`.Wait()` は使わない。

## MusicHudModule

`MusicHudModule` は次を所有する。

- Minimal、Productivity、Lyrics FocusのView選択
- 3デザインそれぞれのViewキャッシュ
- `MainViewModel` を互換DataContextとして設定する処理
- `ShowProgressBar`、`ShowLyrics`、`HasMultipleSessions` を含む推奨サイズ計算
- デザインと音楽固有表示設定の変更監視
- `PresentationInvalidated` 通知
- `MusicViewModel.Cleanup` を含む破棄

設定変更時はViewを無条件に再生成しない。現在デザインに対応するキャッシュ済みView、推奨サイズ、必要な場合のDataContextだけを再評価する。イベント購読は初期化時に一度だけ行い、破棄時に解除する。

## MainWindow

MainWindowの責務は次に限定する。

- 画面上端への配置、マルチモニター、DPI
- マウス入力と表示状態変更の要求
- 現在HUDの汎用ホスト
- 共通サイズと透明度アニメーション
- 全画面時の展開抑制
- 位置編集
- 設定画面、トレイ、終了アニメーション
- 表示言語、ウィンドウ位置、位置編集モード、全画面抑制、終了状態、トレイ表示などのシェル状態監視

HUDのView選択と推奨サイズの決定に関しては、`HudRouter` の変更通知と現在モジュールの `PresentationInvalidated` だけを監視する。現在モジュールの購読は切り替え時と終了時に必ず解除する。

MainWindowは音楽View型、`DesignVariant`、`ShowLyrics`、`ShowProgressBar`、`HasMultipleSessions`、音楽固有サイズを参照しない。HUD IDごとの条件分岐も持たない。

## 設定スキーマと移行

既存のフラットな `UserSettings` を維持し、次を追加する段階移行を採用する。

- `SchemaVersion`
- `DefaultHudId`（既定値 `music`）
- `EnabledHudModuleIds`（既定値 `[music]`）
- モジュールIDをキーとする設定領域

モジュール設定領域は未知のJSONを保持できる型を使い、将来のモジュール設定を保存可能にする。JSONの解析・正規化を実ファイルI/Oから分離し、単体テスト可能にする。

正規化では次を保証する。

- バージョンなし旧JSONを現在スキーマとして補完できる
- 欠けた新規項目に安全な既定値を入れる
- 既存の音楽、言語、位置、起動、全画面設定を保持する
- 不明な既定HUDを `music` へ補正する
- 空の有効HUD一覧を `[music]` へ補正する
- 未知プロパティで可能な限り失敗しない
- 壊れたJSONでは既定値へフォールバックする
- `%AppData%` と旧保存場所の既存移行を維持する

## エラー処理

- 無効なIDと重複IDは登録時に明示的な例外とする
- 利用者が指定した不明HUDはクラッシュさせずフォールバックする
- ライフサイクル処理は直列化し、中途半端な状態確定を避ける
- 切り替え失敗時は、可能な限り旧モジュールまたは既定モジュールへ安全に戻す
- 新しい基盤では診断不能な例外握りつぶしを追加しない
- UI更新はDispatcher境界を明示する

## テスト方針

小規模なxUnitテストプロジェクトをソリューションへ追加し、WPF Viewそのものではなく純粋ロジックとライフサイクルを検証する。新規の依存はテスト専用パッケージに限定する。

### HudRegistry

- 登録、検索、登録順列挙
- 重複ID拒否
- null、空白、無効ID拒否
- 登録モジュールの一度だけの破棄

### HudRouter

- 既定HUDの初期化と展開
- 指定HUDへの遷移
- 折りたたみと表示状態変更
- Pinnedでのマウス離脱相当の折りたたみ拒否
- 不明HUDと無効HUDのフォールバック
- 現在HUD無効化前のフォールバック
- 有効HUD0件の `music` 自動補正
- 状態変更通知
- 表示状態変更ではActivate／Deactivateしないこと
- 購読解除、Deactivate、Initialize、購読、Activate、状態確定、通知の順序
- 重複した非同期遷移の直列化

### ライフサイクル

`FakeHudModule` で次を検証する。

- Initializeが一度だけ
- Activate／Deactivateの順序と冪等性
- Activate中のPresentationInvalidatedを受信できる購読順序
- 切り替え後に旧モジュール通知を受信しないこと
- Shutdown時のDeactivate
- Registry破棄時のDisposeが一度だけ
- Cleanup所有がMusicHudModuleだけであることをコード経路で確認

### 設定

- バージョンなし旧設定の読み込み
- 新しい既定値の補完
- 既存設定値の保持
- 不明な既定HUDと空の有効一覧の補正
- 新形式の保存と再読み込み
- モジュール設定領域の往復
- 壊れたJSONの既定値フォールバック

### サイズ計算

- 3デザインの基本サイズ
- プログレスバー、歌詞、複数セッションによるサイズ差分
- 折りたたみサイズがシェル共通値になること

## 検証

最終的に次を実行する。

```text
dotnet restore
dotnet build NoraBar.slnx -c Release
dotnet test NoraBar.slnx -c Release
dotnet build NoraBar.slnx -c Debug
```

このリポジトリには `package.json` がないため、`npm run lint`、`npm run test`、`react-doctor` は対象外として明記する。GUI確認を実行できない場合は成功扱いにせず、ビルド、テスト、コード経路確認で代替した範囲を報告する。

## ドキュメント

日本語と英語のアーキテクチャ文書を更新し、モジュール契約、Registry、Router、MainWindowの責務、ライフサイクル、設定移行、新規組み込みHUDの最小追加例、将来の外部プラグイン接続境界を記載する。

## 非目標

Phase 0では、ホームHUD、ランチャーHUD、Clock HUD、上部タブ、ウィジェットUI、ライブアクティビティ、自動切り替え、外部DLLローダー、プラグインストア、新テーマ、音楽機能の全面再実装を行わない。

将来の外部プラグイン境界は次に限定する。

```text
外部プラグイン読み込み（将来）
  → IHudModuleとして登録
  → HudRegistry
  → HudRouter
  → 汎用MainWindowホスト
```
