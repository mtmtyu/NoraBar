using System.Text.Json;
using System.Text.Json.Nodes;
using NoraBar.Models;
using NoraBar.Services;

namespace NoraBar.Hud.Home;

internal static class HomeHudSettingsJson
{
    private const string DesignVariantProperty = "DesignVariant";
    private const string TimeFormatProperty = "TimeFormat";
    private const string FirstClockProperty = "FirstClock";
    private const string SecondClockProperty = "SecondClock";
    private const string LabelProperty = "Label";
    private const string TimeZoneIdProperty = "TimeZoneId";

    internal static HomeHudSettings Read(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Modules is null
            || !settings.Modules.TryGetValue(BuiltInHudIds.Home, out JsonElement payload)
            || payload.ValueKind != JsonValueKind.Object)
        {
            return HomeHudSettings.Default;
        }

        HomeHudSettings defaults = HomeHudSettings.Default;
        return new HomeHudSettings(
            ReadEnum(payload, DesignVariantProperty, defaults.DesignVariant),
            ReadEnum(payload, TimeFormatProperty, defaults.TimeFormat),
            ReadClock(payload, FirstClockProperty, defaults.FirstClock),
            ReadClock(payload, SecondClockProperty, defaults.SecondClock));
    }

    internal static void Write(UserSettings settings, HomeHudSettings homeSettings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(homeSettings);

        settings.Modules ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        JsonObject root = ReadExistingObject(settings);
        root[DesignVariantProperty] = (int)homeSettings.DesignVariant;
        root[TimeFormatProperty] = (int)homeSettings.TimeFormat;
        root[FirstClockProperty] = CreateClockNode(homeSettings.FirstClock);
        root[SecondClockProperty] = CreateClockNode(homeSettings.SecondClock);
        settings.Modules[BuiltInHudIds.Home] = JsonSerializer.SerializeToElement(root);
    }

    private static JsonObject ReadExistingObject(UserSettings settings)
    {
        if (!settings.Modules.TryGetValue(BuiltInHudIds.Home, out JsonElement payload)
            || payload.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(payload.GetRawText()) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonObject CreateClockNode(HomeWorldClockSettings clock) => new()
    {
        [LabelProperty] = clock.Label,
        [TimeZoneIdProperty] = clock.TimeZoneId
    };

    private static TEnum ReadEnum<TEnum>(
        JsonElement payload,
        string propertyName,
        TEnum fallback)
        where TEnum : struct, Enum
    {
        return payload.TryGetProperty(propertyName, out JsonElement value)
            && value.TryGetInt32(out int numericValue)
            && Enum.IsDefined(typeof(TEnum), numericValue)
                ? (TEnum)Enum.ToObject(typeof(TEnum), numericValue)
                : fallback;
    }

    private static HomeWorldClockSettings ReadClock(
        JsonElement payload,
        string propertyName,
        HomeWorldClockSettings fallback)
    {
        if (!payload.TryGetProperty(propertyName, out JsonElement clock)
            || clock.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        string label = ReadNonEmptyString(clock, LabelProperty) ?? fallback.Label;
        string timeZoneId = ReadNonEmptyString(clock, TimeZoneIdProperty) ?? fallback.TimeZoneId;
        return new HomeWorldClockSettings(label, timeZoneId);
    }

    private static string? ReadNonEmptyString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
