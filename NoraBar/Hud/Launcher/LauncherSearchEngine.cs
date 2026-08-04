namespace NoraBar.Hud.Launcher;

internal enum LauncherSearchSource { Registered, Installed }

internal sealed record LauncherSearchResult(LauncherItem Item, LauncherSearchSource Source, double Score);

internal static class LauncherSearchEngine
{
    internal static LauncherSearchResult[] Search(
        string query,
        IEnumerable<LauncherItem> registeredItems,
        IEnumerable<LauncherItem> installedApplications,
        int maximumResults = 50)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(registeredItems);
        ArgumentNullException.ThrowIfNull(installedApplications);
        if (string.IsNullOrWhiteSpace(query) || maximumResults <= 0)
        {
            return [];
        }

        string normalized = query.Trim();
        LauncherItem[] registered = registeredItems.ToArray();
        HashSet<string> registeredIdentities = registered
            .Select(GetLaunchIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return registered
            .Select(item => CreateResult(item, LauncherSearchSource.Registered, normalized))
            .Concat(installedApplications
                .Where(item => !registeredIdentities.Contains(GetLaunchIdentity(item)))
                .Select(item => CreateResult(item, LauncherSearchSource.Installed, normalized)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(maximumResults)
            .ToArray();
    }

    private static string GetLaunchIdentity(LauncherItem item) =>
        $"{item.Kind}\0{item.Target}\0{item.Arguments}";

    private static LauncherSearchResult CreateResult(LauncherItem item, LauncherSearchSource source, string query)
    {
        double score = Score(item.DisplayName, query);
        return new LauncherSearchResult(item, source, score > 0 && source == LauncherSearchSource.Registered ? score + 5 : score);
    }

    private static double Score(string value, string query)
    {
        int substringIndex = value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (substringIndex == 0) return 1000 - value.Length;
        if (substringIndex > 0) return 700 - substringIndex - value.Length * 0.1;

        int queryIndex = 0;
        int firstMatch = -1;
        int lastMatch = -1;
        for (int valueIndex = 0; valueIndex < value.Length && queryIndex < query.Length; valueIndex++)
        {
            if (char.ToUpperInvariant(value[valueIndex]) != char.ToUpperInvariant(query[queryIndex])) continue;
            firstMatch = firstMatch < 0 ? valueIndex : firstMatch;
            lastMatch = valueIndex;
            queryIndex++;
        }

        return queryIndex == query.Length ? 400 - (lastMatch - firstMatch) - value.Length * 0.1 : 0;
    }
}
