namespace NoraBar.Hud;

/// <summary>
/// HUDモジュールを一覧表示するためのメタデータを表します。
/// </summary>
/// <param name="DisplayName">利用者に表示するモジュール名。</param>
/// <param name="DisplayOrder">一覧内での表示順を決める値。</param>
public sealed record HudModuleMetadata(string DisplayName, int DisplayOrder);
