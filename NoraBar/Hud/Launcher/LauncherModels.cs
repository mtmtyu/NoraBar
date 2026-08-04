namespace NoraBar.Hud.Launcher;

internal enum LauncherItemKind
{
    Win32Application,
    PackagedApplication,
    File,
    Folder,
    Url
}

internal enum LauncherExistingInstanceBehavior
{
    FocusExisting,
    AlwaysLaunchNew
}

internal sealed record LauncherItem(
    string Id,
    string DisplayName,
    LauncherItemKind Kind,
    string Target,
    string? Arguments = null,
    string? WorkingDirectory = null,
    bool RunAsAdministrator = false,
    LauncherExistingInstanceBehavior ExistingInstanceBehavior = LauncherExistingInstanceBehavior.FocusExisting,
    string? BrowserTarget = null,
    string? CustomIconPath = null);

internal sealed record LauncherGroup(string Id, string DisplayName, IReadOnlyList<LauncherItem> Items);

internal sealed record LauncherPage(string Id, string DisplayName, IReadOnlyList<LauncherGroup> Groups);

internal sealed record LauncherRuleConditions(
    IReadOnlyList<DayOfWeek>? Weekdays,
    int? StartMinuteOfDay,
    int? EndMinuteOfDay,
    string? ForegroundApplicationId);

internal sealed record LauncherRule(
    string Id,
    bool IsEnabled,
    int Priority,
    string TargetPageId,
    LauncherRuleConditions Conditions);

internal sealed record LauncherShortcut(string Modifiers, string Key);

internal sealed record LauncherHudSettings(
    int PayloadVersion,
    IReadOnlyList<LauncherPage> Pages,
    bool SmartEnabled,
    IReadOnlyList<LauncherRule> Rules,
    LauncherShortcut? OpenShortcut,
    LauncherShortcut? SearchShortcut)
{
    internal const int CurrentPayloadVersion = 1;

    internal static LauncherHudSettings Default { get; } = new(
        CurrentPayloadVersion,
        [new LauncherPage("page-main", "Main", [new LauncherGroup("group-main", "Applications", [])])],
        SmartEnabled: true,
        Rules: [],
        OpenShortcut: null,
        SearchShortcut: null);
}
