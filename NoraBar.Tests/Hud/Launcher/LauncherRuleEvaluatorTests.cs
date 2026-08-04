using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherRuleEvaluatorTests
{
    private static readonly DateTimeOffset MondayMorning =
        new(2026, 8, 3, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_RequiresAllConfiguredConditions()
    {
        LauncherRule rule = Rule("work", 10, [DayOfWeek.Monday], 9 * 60, 12 * 60, "code.exe");

        Assert.Equal("work", LauncherRuleEvaluator.Evaluate([rule], MondayMorning, "CODE.EXE"));
        Assert.Null(LauncherRuleEvaluator.Evaluate([rule], MondayMorning, "browser.exe"));
        Assert.Null(LauncherRuleEvaluator.Evaluate([rule], MondayMorning.AddDays(1), "code.exe"));
        Assert.Null(LauncherRuleEvaluator.Evaluate([rule], MondayMorning.AddHours(4), "code.exe"));
    }

    [Fact]
    public void Evaluate_UsesPriorityThenConfiguredOrder()
    {
        LauncherRule low = Rule("low", 1, null, null, null, null);
        LauncherRule firstHigh = Rule("first", 5, null, null, null, null);
        LauncherRule secondHigh = Rule("second", 5, null, null, null, null);

        Assert.Equal("first", LauncherRuleEvaluator.Evaluate([low, firstHigh, secondHigh], MondayMorning, null));
    }

    [Fact]
    public void Evaluate_SupportsTimeRangesCrossingMidnight()
    {
        LauncherRule rule = Rule("night", 1, null, 22 * 60, 6 * 60, null);

        Assert.Equal("night", LauncherRuleEvaluator.Evaluate([rule], MondayMorning.Date.AddHours(23), null));
        Assert.Equal("night", LauncherRuleEvaluator.Evaluate([rule], MondayMorning.Date.AddDays(1).AddHours(5), null));
        Assert.Null(LauncherRuleEvaluator.Evaluate([rule], MondayMorning.Date.AddHours(12), null));
    }

    private static LauncherRule Rule(
        string pageId,
        int priority,
        IReadOnlyList<DayOfWeek>? weekdays,
        int? startMinute,
        int? endMinute,
        string? foreground) =>
        new(Guid.NewGuid().ToString("N"), true, priority, pageId,
            new LauncherRuleConditions(weekdays, startMinute, endMinute, foreground));
}
