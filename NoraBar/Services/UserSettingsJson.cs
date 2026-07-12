using System.Text.Json;
using NoraBar.Hud;

namespace NoraBar.Services;

internal static class UserSettingsJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static UserSettings DeserializeOrDefault(string json)
    {
        try
        {
            return NormalizeStructure(
                JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions) ?? new UserSettings());
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
        catch (NotSupportedException)
        {
            return new UserSettings();
        }
    }

    public static string Serialize(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(NormalizeStructure(settings), SerializerOptions);
    }

    private static UserSettings NormalizeStructure(UserSettings settings)
    {
        return new UserSettings
        {
            SchemaVersion = settings.SchemaVersion <= 0
                ? UserSettings.CurrentSchemaVersion
                : settings.SchemaVersion,
            DefaultHudId = string.IsNullOrWhiteSpace(settings.DefaultHudId)
                ? BuiltInHudIds.Music
                : settings.DefaultHudId,
            EnabledHudModuleIds = settings.EnabledHudModuleIds is null
                ? [BuiltInHudIds.Music]
                : [.. settings.EnabledHudModuleIds],
            Modules = settings.Modules is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : settings.Modules.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal),
            AdditionalProperties = settings.AdditionalProperties is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : settings.AdditionalProperties.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal),
            Variant = settings.Variant,
            ShowProgressBar = settings.ShowProgressBar,
            Language = settings.Language,
            ShowLyrics = settings.ShowLyrics,
            TextScrollMode = settings.TextScrollMode,
            HasCustomPosition = settings.HasCustomPosition,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
            CheckUpdateOnStartup = settings.CheckUpdateOnStartup,
            DisableExpandOnFullscreen = settings.DisableExpandOnFullscreen
        };
    }
}
