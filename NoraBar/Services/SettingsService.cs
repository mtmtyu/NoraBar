using System;
using System.IO;
using System.Text.Json;
using NoraBar.Models;

namespace NoraBar.Services
{
    public class UserSettings
    {
        public DesignVariant Variant { get; set; } = DesignVariant.MinimalFloatingPill;
        public bool ShowProgressBar { get; set; } = true;
        public AppLanguage Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? AppLanguage.Japanese : AppLanguage.English;
        public bool ShowLyrics { get; set; } = false;
        public bool HasCustomPosition { get; set; } = false;
        public double WindowLeft { get; set; } = 0;
        public double WindowTop { get; set; } = 0;
    }

    public static class SettingsService
    {
        private const string SettingsDirectoryName = "NoraBar";
        private const string SettingsFileName = "settings.json";

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
        private static readonly string SettingsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            SettingsDirectoryName);
        private static readonly string FilePath = Path.Combine(SettingsDirectoryPath, SettingsFileName);
        private static readonly string LegacyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

        public static UserSettings Load()
        {
            try
            {
                bool isFirstRun = !File.Exists(FilePath) && !File.Exists(LegacyFilePath);

                MigrateLegacySettingsIfNeeded();

                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
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
                Directory.CreateDirectory(SettingsDirectoryPath);

                string json = JsonSerializer.Serialize(settings, SerializerOptions);
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

            Directory.CreateDirectory(SettingsDirectoryPath);
            File.Move(LegacyFilePath, FilePath);
        }
    }
}
