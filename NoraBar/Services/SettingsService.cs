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
            [BuiltInHudIds.Music, BuiltInHudIds.Home, BuiltInHudIds.Launcher];
        public Dictionary<string, JsonElement> Modules { get; set; } = new(StringComparer.Ordinal);
        public HudNavigationPlacement HudNavigationPlacement { get; set; } =
            HudNavigationPlacement.RightRail;
        public bool HomeHudIntroductionCompleted { get; set; } = true;
        public bool LauncherHudIntroductionCompleted { get; set; } = true;

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

    internal interface ISettingsStore
    {
        UserSettings Load();
        void Save(UserSettings settings);
    }

    internal sealed class FileSettingsStore : ISettingsStore
    {
        private const string SettingsFileName = "settings.json";
        private readonly object _operationLock = new();
        private readonly string _settingsDirectoryPath;
        private readonly string _legacyFilePath;
        private readonly bool _enableFirstRunStartup;

        internal FileSettingsStore(
            string settingsDirectoryPath,
            string legacyFilePath,
            bool enableFirstRunStartup)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(legacyFilePath);
            _settingsDirectoryPath = Path.GetFullPath(settingsDirectoryPath);
            _legacyFilePath = Path.GetFullPath(legacyFilePath);
            _enableFirstRunStartup = enableFirstRunStartup;
        }

        internal string SettingsFilePath =>
            Path.Combine(_settingsDirectoryPath, SettingsFileName);

        public UserSettings Load()
        {
            lock (_operationLock)
            {
                try
                {
                    bool isFirstRun =
                        !File.Exists(SettingsFilePath)
                        && !File.Exists(_legacyFilePath);

                    MigrateLegacySettingsIfNeeded();

                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        return UserSettingsJson.DeserializeOrDefault(json);
                    }

                    if (isFirstRun && _enableFirstRunStartup)
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
        }

        public void Save(UserSettings settings)
        {
            lock (_operationLock)
            {
                try
                {
                    MigrateLegacySettingsIfNeeded();
                    Directory.CreateDirectory(_settingsDirectoryPath);

                    string json = UserSettingsJson.Serialize(settings);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch (Exception)
                {
                    // Fail silently or handle accordingly
                }
            }
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            if (File.Exists(SettingsFilePath) || !File.Exists(_legacyFilePath))
            {
                return;
            }

            Directory.CreateDirectory(_settingsDirectoryPath);
            File.Move(_legacyFilePath, SettingsFilePath);
        }
    }

    public static class SettingsService
    {
        private const string SettingsDirectoryName = "NoraBar";
        private static readonly ISettingsStore DefaultStore = new FileSettingsStore(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                SettingsDirectoryName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"),
            enableFirstRunStartup: true);

        internal static ISettingsStore Store => DefaultStore;

        public static UserSettings Load() => DefaultStore.Load();

        public static void Save(UserSettings settings)
        {
            DefaultStore.Save(settings);
        }
    }
}
