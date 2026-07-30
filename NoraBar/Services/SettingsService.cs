using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoraBar.Hud;
using NoraBar.Models;

namespace NoraBar.Services
{
    public class UserSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string DefaultHudId { get; set; } = BuiltInHudIds.Music;
        public List<string> EnabledHudModuleIds { get; set; } =
            [BuiltInHudIds.Music, BuiltInHudIds.Home];
        public Dictionary<string, JsonElement> Modules { get; set; } = new(StringComparer.Ordinal);
        public HudNavigationPlacement HudNavigationPlacement { get; set; } =
            HudNavigationPlacement.RightRail;
        public bool HomeHudIntroductionCompleted { get; set; } = true;

        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalProperties { get; set; } = new(StringComparer.Ordinal);

        public DesignVariant Variant { get; set; } = DesignVariant.MinimalFloatingPill;
        public bool ShowProgressBar { get; set; } = true;
        public AppLanguage Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? AppLanguage.Japanese : AppLanguage.English;
        public bool ShowLyrics { get; set; } = false;
        public TextScrollMode TextScrollMode { get; set; } = TextScrollMode.Disabled;
        public bool HasCustomPosition { get; set; } = false;
        public double WindowLeft { get; set; } = 0;
        public double WindowTop { get; set; } = 0;
        public bool CheckUpdateOnStartup { get; set; } = true;
        public bool DisableExpandOnFullscreen { get; set; } = true;
    }

    public static class SettingsService
    {
        private const string SettingsDirectoryName = "NoraBar";
        private const string SettingsFileName = "settings.json";

        private static readonly string SettingsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            SettingsDirectoryName);
            
        internal static string? OverrideSettingsDirectoryPath { get; set; }

        private static string FilePath => OverrideSettingsDirectoryPath != null
            ? Path.Combine(OverrideSettingsDirectoryPath, SettingsFileName)
            : Path.Combine(SettingsDirectoryPath, SettingsFileName);

        private static string LegacyFilePath => OverrideSettingsDirectoryPath != null
            ? Path.Combine(OverrideSettingsDirectoryPath, "legacy_" + SettingsFileName)
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

        public static UserSettings Load()
        {
            try
            {
                bool isFirstRun = !File.Exists(FilePath) && !File.Exists(LegacyFilePath);

                MigrateLegacySettingsIfNeeded();

                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return UserSettingsJson.DeserializeOrDefault(json);
                }
                
                if (isFirstRun)
                {
                    StartupService.SetStartup(true);
                }
            }
            catch (Exception)
            {
                // Return default settings if loading fails
            }
            return new UserSettings();
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                MigrateLegacySettingsIfNeeded();
                string currentSettingsDir = OverrideSettingsDirectoryPath ?? SettingsDirectoryPath;
                Directory.CreateDirectory(currentSettingsDir);

                string json = UserSettingsJson.Serialize(settings);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception)
            {
                // Fail silently or handle accordingly
            }
        }

        private static void MigrateLegacySettingsIfNeeded()
        {
            if (File.Exists(FilePath) || !File.Exists(LegacyFilePath))
            {
                return;
            }

            string currentSettingsDir = OverrideSettingsDirectoryPath ?? SettingsDirectoryPath;
            Directory.CreateDirectory(currentSettingsDir);
            File.Move(LegacyFilePath, FilePath);
        }
    }
}
