using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherSmartScorerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rank_UsesRecencyFrequencyDurationAndDecay()
    {
        LauncherUsageEntry recent = Usage("recent", 2, Now.AddHours(-1), TimeSpan.FromMinutes(10));
        LauncherUsageEntry frequentButOld = Usage("old", 100, Now.AddDays(-180), TimeSpan.FromHours(4));

        LauncherSmartSuggestion[] ranked = LauncherSmartScorer.Rank(
            [recent, frequentButOld], [], null, Now, 2);

        Assert.Equal("recent", ranked[0].ApplicationId);
        Assert.True(ranked[0].Score > ranked[1].Score);
        Assert.NotEmpty(ranked[0].Reasons);
    }

    [Fact]
    public void Rank_UsesTimeOfDayAndForegroundContextAffinity()
    {
        LauncherUsageEntry contextual = Usage("contextual", 1, Now.AddDays(-1), TimeSpan.Zero) with
        {
            HourBuckets = new Dictionary<int, int> { [9] = 20 },
            ContextTransitions = new Dictionary<string, int>(StringComparer.Ordinal) { ["editor"] = 12 }
        };
        LauncherUsageEntry neutral = Usage("neutral", 1, Now.AddDays(-1), TimeSpan.Zero);

        LauncherSmartSuggestion[] ranked = LauncherSmartScorer.Rank(
            [neutral, contextual], [], "editor", Now, 2);

        Assert.Equal("contextual", ranked[0].ApplicationId);
        Assert.Contains(ranked[0].Reasons, reason => reason.Contains("context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rank_ExcludesVisibleAndMissingApplications()
    {
        LauncherUsageEntry visible = Usage("visible", 10, Now, TimeSpan.Zero);
        LauncherUsageEntry missing = Usage("missing", 9, Now, TimeSpan.Zero) with { IsLaunchable = false };
        LauncherUsageEntry available = Usage("available", 1, Now, TimeSpan.Zero);

        LauncherSmartSuggestion[] ranked = LauncherSmartScorer.Rank(
            [visible, missing, available], ["visible"], null, Now, 5);

        Assert.Equal("available", Assert.Single(ranked).ApplicationId);
    }

    [Fact]
    public void BoundHistory_KeepsMostRecentEntries()
    {
        LauncherUsageEntry[] entries = Enumerable.Range(0, 10)
            .Select(index => Usage(index.ToString(), 1, Now.AddMinutes(index), TimeSpan.Zero))
            .ToArray();

        LauncherUsageEntry[] bounded = LauncherSmartScorer.BoundHistory(entries, 3);

        Assert.Equal(["9", "8", "7"], bounded.Select(entry => entry.ApplicationId));
    }

    private static LauncherUsageEntry Usage(
        string id,
        int count,
        DateTimeOffset lastUsed,
        TimeSpan duration) =>
        new(id, count, lastUsed, duration, true, new Dictionary<int, int>(),
            new Dictionary<string, int>(StringComparer.Ordinal));
}
