namespace NoraBar.Hud.Launcher;

internal sealed record LauncherUsageEntry(
    string ApplicationId,
    int LaunchCount,
    DateTimeOffset LastUsed,
    TimeSpan ForegroundDuration,
    bool IsLaunchable,
    IReadOnlyDictionary<int, int> HourBuckets,
    IReadOnlyDictionary<string, int> ContextTransitions);

internal sealed record LauncherSmartSuggestion(string ApplicationId, double Score, IReadOnlyList<string> Reasons);

internal static class LauncherSmartScorer
{
    private const double RecencyHalfLifeDays = 7;

    internal static LauncherSmartSuggestion[] Rank(
        IEnumerable<LauncherUsageEntry> entries,
        IEnumerable<string> excludedApplicationIds,
        string? foregroundApplicationId,
        DateTimeOffset now,
        int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(excludedApplicationIds);
        if (maximumResults <= 0) return [];
        HashSet<string> excluded = excludedApplicationIds.ToHashSet(StringComparer.Ordinal);
        return entries.Where(entry => entry.IsLaunchable && !excluded.Contains(entry.ApplicationId))
            .Select(entry => Score(entry, foregroundApplicationId, now))
            .OrderByDescending(suggestion => suggestion.Score)
            .ThenBy(suggestion => suggestion.ApplicationId, StringComparer.Ordinal)
            .Take(maximumResults).ToArray();
    }

    internal static LauncherUsageEntry[] BoundHistory(IEnumerable<LauncherUsageEntry> entries, int maximumEntries) =>
        maximumEntries <= 0 ? [] : entries.OrderByDescending(entry => entry.LastUsed).Take(maximumEntries).ToArray();

    private static LauncherSmartSuggestion Score(LauncherUsageEntry entry, string? foregroundApplicationId, DateTimeOffset now)
    {
        var reasons = new List<string>();
        double ageDays = Math.Max(0, (now - entry.LastUsed).TotalDays);
        double decay = Math.Pow(0.5, ageDays / RecencyHalfLifeDays);
        double recency = 50 * decay;
        double activityDecay = Math.Max(0.05, decay);
        double frequency = 12 * Math.Log2(1 + Math.Max(0, entry.LaunchCount)) * activityDecay;
        double duration = 8 * Math.Log2(1 + Math.Max(0, entry.ForegroundDuration.TotalMinutes)) * activityDecay;
        if (recency >= 10) reasons.Add("recent use");
        if (frequency >= 12) reasons.Add("frequent use");

        int totalHourSamples = entry.HourBuckets.Values.Sum();
        entry.HourBuckets.TryGetValue(now.Hour, out int currentHourSamples);
        double timeAffinity = totalHourSamples == 0 ? 0 : 20d * currentHourSamples / totalHourSamples;
        if (timeAffinity >= 5) reasons.Add("time-of-day affinity");

        double contextAffinity = 0;
        if (!string.IsNullOrWhiteSpace(foregroundApplicationId)
            && entry.ContextTransitions.TryGetValue(foregroundApplicationId, out int transitions))
        {
            contextAffinity = 25d * transitions / Math.Max(1, entry.ContextTransitions.Values.Sum());
            if (contextAffinity >= 5) reasons.Add("foreground context affinity");
        }

        return new LauncherSmartSuggestion(entry.ApplicationId, recency + frequency + duration + timeAffinity + contextAffinity, reasons.AsReadOnly());
    }
}
