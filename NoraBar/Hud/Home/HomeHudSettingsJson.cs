using System.Text.Json;
using System.Text.Json.Nodes;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using NoraBar.Services;

namespace NoraBar.Hud.Home;

internal static class HomeHudSettingsJson
{
    private const string DesignVariantProperty = "DesignVariant";
    private const string TimeFormatProperty = "TimeFormat";
    private const string WorldClocksProperty = "WorldClocks";
    private const string FirstClockProperty = "FirstClock";
    private const string SecondClockProperty = "SecondClock";
    private const string WidgetsProperty = "Widgets";
    private const string MaxWidgetWidthProperty = "MaxWidgetWidth";
    private const string MaxWidgetHeightProperty = "MaxWidgetHeight";
    private const string LabelProperty = "Label";
    private const string TimeZoneIdProperty = "TimeZoneId";
    private const string IdProperty = "Id";
    private const string TypeProperty = "Type";
    private const string StyleProperty = "Style";

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
            ReadWorldClocks(payload, defaults.EffectiveWorldClocks),
            ReadWidgets(payload),
            ReadDouble(payload, MaxWidgetWidthProperty, defaults.MaxWidgetWidth),
            ReadDouble(payload, MaxWidgetHeightProperty, defaults.MaxWidgetHeight));
    }

    internal static void Write(UserSettings settings, HomeHudSettings homeSettings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(homeSettings);

        settings.Modules ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        JsonObject root = ReadExistingObject(settings);
        root[DesignVariantProperty] = (int)homeSettings.DesignVariant;
        root[TimeFormatProperty] = (int)homeSettings.TimeFormat;
        root[WorldClocksProperty] = CreateWorldClocksNode(homeSettings.EffectiveWorldClocks);
        root.Remove(FirstClockProperty);
        root.Remove(SecondClockProperty);
        root[WidgetsProperty] = CreateWidgetsNode(homeSettings.EffectiveWidgets);
        root[MaxWidgetWidthProperty] = homeSettings.MaxWidgetWidth;
        root[MaxWidgetHeightProperty] = homeSettings.MaxWidgetHeight;
        settings.Modules[BuiltInHudIds.Home] = JsonSerializer.SerializeToElement(root);
    }

    private static JsonArray CreateWorldClocksNode(IReadOnlyList<HomeWorldClockEntry> clocks)
    {
        JsonArray array = new JsonArray();
        foreach (HomeWorldClockEntry clock in clocks)
        {
            array.Add(new JsonObject
            {
                [LabelProperty] = clock.Label,
                [TimeZoneIdProperty] = clock.TimeZoneId
            });
        }
        return array;
    }

    private static JsonArray CreateWidgetsNode(IReadOnlyList<HomeWidgetConfig> widgets)
    {
        JsonArray array = new JsonArray();
        foreach (HomeWidgetConfig widget in widgets)
        {
            array.Add(new JsonObject
            {
                [IdProperty] = widget.Id,
                [TypeProperty] = (int)widget.Type,
                [StyleProperty] = (int)widget.Style
            });
        }
        return array;
    }

    private static IReadOnlyList<HomeWorldClockEntry>? ReadWorldClocks(
        JsonElement payload,
        IReadOnlyList<HomeWorldClockEntry> fallback)
    {
        if (payload.TryGetProperty(WorldClocksProperty, out JsonElement clocksElement)
            && clocksElement.ValueKind == JsonValueKind.Array)
        {
            List<HomeWorldClockEntry> list = new List<HomeWorldClockEntry>();
            foreach (JsonElement item in clocksElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string label = ReadNonEmptyString(item, LabelProperty) ?? "CLOCK";
                string timeZoneId = ReadNonEmptyString(item, TimeZoneIdProperty) ?? "Local";
                list.Add(new HomeWorldClockEntry(label, timeZoneId));
                if (list.Count == HomeHudSettings.MaximumWorldClockCount)
                {
                    break;
                }
            }

            if (list.Count > 0)
            {
                return list.AsReadOnly();
            }
        }

        // Legacy fallback from FirstClock / SecondClock
        List<HomeWorldClockEntry> legacyList = new List<HomeWorldClockEntry>();
        HomeWorldClockEntry? first = ReadLegacyClock(payload, FirstClockProperty);
        if (first is not null)
        {
            legacyList.Add(first);
        }

        HomeWorldClockEntry? second = ReadLegacyClock(payload, SecondClockProperty);
        if (second is not null)
        {
            legacyList.Add(second);
        }

        return legacyList.Count > 0 ? legacyList.AsReadOnly() : fallback;
    }

    private static HomeWorldClockEntry? ReadLegacyClock(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out JsonElement clock)
            || clock.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? label = ReadNonEmptyString(clock, LabelProperty);
        string? timeZoneId = ReadNonEmptyString(clock, TimeZoneIdProperty);
        if (label is null && timeZoneId is null)
        {
            return null;
        }

        return new HomeWorldClockEntry(label ?? "CLOCK", timeZoneId ?? "Local");
    }

    private static IReadOnlyList<HomeWidgetConfig>? ReadWidgets(JsonElement payload)
    {
        if (!payload.TryGetProperty(WidgetsProperty, out JsonElement widgetsElement)
            || widgetsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<HomeWidgetConfig> list = new List<HomeWidgetConfig>();
        foreach (JsonElement item in widgetsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? id = ReadNonEmptyString(item, IdProperty);
            if (id is null)
            {
                continue;
            }

            HomeWidgetType type = ReadEnum(item, TypeProperty, HomeWidgetType.DigitalClock);
            HomeWidgetStyle style = ReadEnum(item, StyleProperty, HomeWidgetStyle.ClockMinimal);
            list.Add(new HomeWidgetConfig(id, type, style));
        }

        return list.Count > 0 ? list.AsReadOnly() : null;
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

    private static double ReadDouble(JsonElement payload, string propertyName, double fallback)
    {
        return payload.TryGetProperty(propertyName, out JsonElement value)
            && value.TryGetDouble(out double doubleValue)
            && doubleValue > 0
                ? doubleValue
                : fallback;
    }
}
