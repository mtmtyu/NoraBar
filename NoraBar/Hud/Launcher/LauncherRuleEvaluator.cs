namespace NoraBar.Hud.Launcher;

internal static class LauncherRuleEvaluator
{
    internal static string? Evaluate(IEnumerable<LauncherRule> rules, DateTimeOffset now, string? foregroundApplicationId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Select((rule, order) => (rule, order))
            .Where(candidate => candidate.rule.IsEnabled && Matches(candidate.rule.Conditions, now, foregroundApplicationId))
            .OrderByDescending(candidate => candidate.rule.Priority)
            .ThenBy(candidate => candidate.order)
            .Select(candidate => candidate.rule.TargetPageId)
            .FirstOrDefault();
    }

    private static bool Matches(LauncherRuleConditions conditions, DateTimeOffset now, string? foregroundApplicationId)
    {
        if (conditions.Weekdays is { Count: > 0 } && !conditions.Weekdays.Contains(now.DayOfWeek)) return false;
        if (conditions.StartMinuteOfDay is int start && conditions.EndMinuteOfDay is int end)
        {
            int minute = now.Hour * 60 + now.Minute;
            bool matches = start <= end ? minute >= start && minute < end : minute >= start || minute < end;
            if (!matches) return false;
        }

        return string.IsNullOrWhiteSpace(conditions.ForegroundApplicationId)
            || string.Equals(conditions.ForegroundApplicationId, foregroundApplicationId, StringComparison.OrdinalIgnoreCase);
    }
}
