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
            using JsonDocument document = JsonDocument.Parse(json);
            bool introductionMarkerExists = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(
                    nameof(UserSettings.HomeHudIntroductionCompleted),
                    out _);
            UserSettings settings =
                JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions)
                ?? new UserSettings();
            if (!introductionMarkerExists)
            {
                settings.HomeHudIntroductionCompleted = false;
            }

            return NormalizeStructure(settings);
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
            EnabledHudModuleIds = NormalizeEnabledHudIds(
                settings.EnabledHudModuleIds,
                settings.HomeHudIntroductionCompleted),
            Modules = settings.Modules is null
                ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                : settings.Modules.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Clone(),
                    StringComparer.Ordinal),
            HudNavigationPlacement = settings.HudNavigationPlacement,
            HomeHudIntroductionCompleted = true,
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

    private static List<string> NormalizeEnabledHudIds(
        IEnumerable<string>? configuredIds,
        bool introductionCompleted)
    {
        List<string> result = configuredIds is null
            ? [BuiltInHudIds.Music, BuiltInHudIds.Home]
            : [.. configuredIds];
        if (introductionCompleted
            || result.Contains(BuiltInHudIds.Home, StringComparer.Ordinal))
        {
            return result;
        }

        int musicIndex = result.FindIndex(
            id => string.Equals(id, BuiltInHudIds.Music, StringComparison.Ordinal));
        result.Insert(musicIndex >= 0 ? musicIndex + 1 : result.Count, BuiltInHudIds.Home);
        return result;
    }
}
