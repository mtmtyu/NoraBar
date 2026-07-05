using System.Collections.Generic;
using NoraBar.Models;

namespace NoraBar.Services
{
    public enum LocalizationKey
    {
        WindowTitle,
        AppSubtitle,
        HudSettings,
        About,
        DesignStyle,
        DesignStyleDescription,
        ProgressBar,
        ProgressBarDescription,
        Startup,
        StartupDescription,
        Language,
        LanguageDescription,
        Japanese,
        English,
        Version,
        CheckUpdates,
        CheckUpdatesDescription,
        CheckUpdatesButton,
        GitHubRepository,
        GitHubRepositoryDescription,
        OpenRepository,
        OpenSourceLicenses,
        OpenSourceLicensesDescription,
        ShowLicenses,
        LivePreview,
        Close,
        ThirdPartyLicenses,
        Update,
        Download,
        SettingsMenu,
        Exit,
        CheckingUpdates,
        UpdateAvailable,
        UpToDate,
        UpdateFailed,
        LicenseIntroTitle,
        LicensePackageVersion,
        LicenseLabel,
        LicenseDetails
    }

    public static class LocalizationService
    {
        private static readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<LocalizationKey, string>> Translations =
            new Dictionary<AppLanguage, IReadOnlyDictionary<LocalizationKey, string>>
            {
                [AppLanguage.Japanese] = new Dictionary<LocalizationKey, string>
                {
                    [LocalizationKey.WindowTitle] = "NoraBar 設定",
                    [LocalizationKey.AppSubtitle] = "デスクトップHUD",
                    [LocalizationKey.HudSettings] = "HUD設定",
                    [LocalizationKey.About] = "バージョン情報",
                    [LocalizationKey.DesignStyle] = "デザインスタイル",
                    [LocalizationKey.DesignStyleDescription] = "HUDのレイアウトデザイン（Minimal または Productivity）を切り替えます。",
                    [LocalizationKey.ProgressBar] = "プログレスバー表示",
                    [LocalizationKey.ProgressBarDescription] = "再生中にHUDの下部に進行状況バーを表示します。",
                    [LocalizationKey.Startup] = "システム起動時に実行",
                    [LocalizationKey.StartupDescription] = "Windowsの起動時にNoraBarを自動的に開始します。",
                    [LocalizationKey.Language] = "表示言語",
                    [LocalizationKey.LanguageDescription] = "アプリ内の表示言語を切り替えます。",
                    [LocalizationKey.Japanese] = "日本語",
                    [LocalizationKey.English] = "English",
                    [LocalizationKey.Version] = "バージョン ",
                    [LocalizationKey.CheckUpdates] = "アップデートの確認",
                    [LocalizationKey.CheckUpdatesDescription] = "GitHub Releasesから最新バージョンがあるかチェックします。",
                    [LocalizationKey.CheckUpdatesButton] = "アップデートを確認",
                    [LocalizationKey.GitHubRepository] = "GitHub リポジトリ",
                    [LocalizationKey.GitHubRepositoryDescription] = "ソースコードを公開しているリポジトリページを開きます。",
                    [LocalizationKey.OpenRepository] = "リポジトリを開く",
                    [LocalizationKey.OpenSourceLicenses] = "オープンソース ライセンス",
                    [LocalizationKey.OpenSourceLicensesDescription] = "使用しているサードパーティ製ライブラリのライセンスを表示します。",
                    [LocalizationKey.ShowLicenses] = "ライセンスを表示",
                    [LocalizationKey.LivePreview] = "リアルタイムプレビュー",
                    [LocalizationKey.Close] = "閉じる",
                    [LocalizationKey.ThirdPartyLicenses] = "サードパーティ ソフトウェアのライセンス",
                    [LocalizationKey.Update] = "アップデート",
                    [LocalizationKey.Download] = "ダウンロード",
                    [LocalizationKey.SettingsMenu] = "設定...",
                    [LocalizationKey.Exit] = "終了",
                    [LocalizationKey.CheckingUpdates] = "アップデートを確認中...",
                    [LocalizationKey.UpdateAvailable] = "新しいバージョンが見つかりました: {0}",
                    [LocalizationKey.UpToDate] = "お使いのバージョンは最新です。",
                    [LocalizationKey.UpdateFailed] = "アップデートの確認に失敗しました。\nエラー: {0}",
                    [LocalizationKey.LicenseIntroTitle] = "【使用しているサードパーティ ソフトウェア】",
                    [LocalizationKey.LicensePackageVersion] = "■ CSCore (バージョン 1.2.1.2)",
                    [LocalizationKey.LicenseLabel] = "   ライセンス: Microsoft Public License (MS-PL)",
                    [LocalizationKey.LicenseDetails] = "   (詳細なライセンス条文は以下を参照してください)"
                },
                [AppLanguage.English] = new Dictionary<LocalizationKey, string>
                {
                    [LocalizationKey.WindowTitle] = "NoraBar Settings",
                    [LocalizationKey.AppSubtitle] = "Desktop HUD",
                    [LocalizationKey.HudSettings] = "HUD Settings",
                    [LocalizationKey.About] = "About",
                    [LocalizationKey.DesignStyle] = "Design Style",
                    [LocalizationKey.DesignStyleDescription] = "Switches the HUD layout between Minimal and Productivity.",
                    [LocalizationKey.ProgressBar] = "Progress Bar",
                    [LocalizationKey.ProgressBarDescription] = "Shows the playback progress bar at the bottom of the HUD.",
                    [LocalizationKey.Startup] = "Run at System Startup",
                    [LocalizationKey.StartupDescription] = "Starts NoraBar automatically when Windows starts.",
                    [LocalizationKey.Language] = "Display Language",
                    [LocalizationKey.LanguageDescription] = "Changes the language used in the app.",
                    [LocalizationKey.Japanese] = "日本語",
                    [LocalizationKey.English] = "English",
                    [LocalizationKey.Version] = "Version ",
                    [LocalizationKey.CheckUpdates] = "Check for Updates",
                    [LocalizationKey.CheckUpdatesDescription] = "Checks GitHub Releases for the latest version.",
                    [LocalizationKey.CheckUpdatesButton] = "Check Updates",
                    [LocalizationKey.GitHubRepository] = "GitHub Repository",
                    [LocalizationKey.GitHubRepositoryDescription] = "Opens the repository page where the source code is published.",
                    [LocalizationKey.OpenRepository] = "Open Repository",
                    [LocalizationKey.OpenSourceLicenses] = "Open Source Licenses",
                    [LocalizationKey.OpenSourceLicensesDescription] = "Shows licenses for the third-party libraries used by this app.",
                    [LocalizationKey.ShowLicenses] = "Show Licenses",
                    [LocalizationKey.LivePreview] = "Live Preview",
                    [LocalizationKey.Close] = "Close",
                    [LocalizationKey.ThirdPartyLicenses] = "Third-party Software Licenses",
                    [LocalizationKey.Update] = "Update",
                    [LocalizationKey.Download] = "Download",
                    [LocalizationKey.SettingsMenu] = "Settings...",
                    [LocalizationKey.Exit] = "Exit",
                    [LocalizationKey.CheckingUpdates] = "Checking for updates...",
                    [LocalizationKey.UpdateAvailable] = "A new version is available: {0}",
                    [LocalizationKey.UpToDate] = "You are using the latest version.",
                    [LocalizationKey.UpdateFailed] = "Failed to check for updates.\nError: {0}",
                    [LocalizationKey.LicenseIntroTitle] = "Third-party software used by NoraBar",
                    [LocalizationKey.LicensePackageVersion] = "CSCore (version 1.2.1.2)",
                    [LocalizationKey.LicenseLabel] = "   License: Microsoft Public License (MS-PL)",
                    [LocalizationKey.LicenseDetails] = "   See the license text below for details."
                }
            };

        public static string GetText(AppLanguage language, LocalizationKey key)
        {
            if (Translations.TryGetValue(language, out var languageTranslations) &&
                languageTranslations.TryGetValue(key, out var text))
            {
                return text;
            }

            return Translations[AppLanguage.Japanese][key];
        }
    }
}
