using System.Text.Json;
using System.Text.Json.Nodes;
using NoraBar.Services;

namespace NoraBar.Hud.Launcher;

internal static class LauncherHudSettingsJson
{
    private static readonly JsonSerializerOptions Options = new();

    internal static LauncherHudSettings Read(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Modules is null
            || !settings.Modules.TryGetValue(BuiltInHudIds.Launcher, out JsonElement payload)
            || payload.ValueKind != JsonValueKind.Object)
        {
            return LauncherHudSettings.Default;
        }

        try
        {
            LauncherHudSettings? parsed = payload.Deserialize<LauncherHudSettings>(Options);
            return parsed is null ? LauncherHudSettings.Default : Normalize(parsed);
        }
        catch (JsonException)
        {
            return LauncherHudSettings.Default;
        }
        catch (NotSupportedException)
        {
            return LauncherHudSettings.Default;
        }
    }

    internal static void Write(UserSettings settings, LauncherHudSettings launcherSettings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(launcherSettings);
        settings.Modules ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        JsonObject root = ReadExistingObject(settings);
        int payloadVersion = Math.Max(
            LauncherHudSettings.CurrentPayloadVersion,
            Math.Max(launcherSettings.PayloadVersion, ReadPayloadVersion(root)));
        JsonObject known = JsonSerializer.SerializeToNode(
            Normalize(launcherSettings) with { PayloadVersion = payloadVersion },
            Options)!.AsObject();
        foreach ((string propertyName, JsonNode? value) in known)
        {
            root[propertyName] = value?.DeepClone();
        }

        settings.Modules[BuiltInHudIds.Launcher] = JsonSerializer.SerializeToElement(root, Options);
    }

    private static LauncherHudSettings Normalize(LauncherHudSettings settings)
    {
        LauncherPage[] pages = (settings.Pages ?? [])
            .Where(page => page is not null && !string.IsNullOrWhiteSpace(page.Id))
            .Select(NormalizePage)
            .GroupBy(page => page.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (pages.Length == 0)
        {
            pages = [.. LauncherHudSettings.Default.Pages];
        }

        HashSet<string> pageIds = pages.Select(page => page.Id).ToHashSet(StringComparer.Ordinal);
        LauncherRule[] rules = (settings.Rules ?? [])
            .Where(rule => rule is not null
                && !string.IsNullOrWhiteSpace(rule.Id)
                && pageIds.Contains(rule.TargetPageId))
            .GroupBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return settings with
        {
            PayloadVersion = settings.PayloadVersion <= 0
                ? LauncherHudSettings.CurrentPayloadVersion
                : settings.PayloadVersion,
            Pages = pages,
            Rules = rules
        };
    }

    private static LauncherPage NormalizePage(LauncherPage page)
    {
        LauncherGroup[] groups = (page.Groups ?? [])
            .Where(group => group is not null && !string.IsNullOrWhiteSpace(group.Id))
            .Select(group => group with
            {
                DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName,
                Items = (group.Items ?? [])
                    .Where(IsValidItem)
                    .GroupBy(item => item.Id, StringComparer.Ordinal)
                    .Select(items => items.First())
                    .ToArray()
            })
            .GroupBy(group => group.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (groups.Length == 0)
        {
            groups = [new LauncherGroup($"group-{page.Id}", "Applications", [])];
        }

        return page with
        {
            DisplayName = string.IsNullOrWhiteSpace(page.DisplayName) ? page.Id : page.DisplayName,
            Groups = groups
        };
    }

    private static bool IsValidItem(LauncherItem item)
    {
        if (item is null
            || string.IsNullOrWhiteSpace(item.Id)
            || string.IsNullOrWhiteSpace(item.DisplayName)
            || string.IsNullOrWhiteSpace(item.Target)
            || !Enum.IsDefined(item.Kind))
        {
            return false;
        }

        return item.Kind != LauncherItemKind.Url
            || Uri.TryCreate(item.Target, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static int ReadPayloadVersion(JsonObject root)
    {
        try
        {
            return root[nameof(LauncherHudSettings.PayloadVersion)]?.GetValue<int>() ?? 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static JsonObject ReadExistingObject(UserSettings settings)
    {
        if (!settings.Modules.TryGetValue(BuiltInHudIds.Launcher, out JsonElement payload)
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
}
