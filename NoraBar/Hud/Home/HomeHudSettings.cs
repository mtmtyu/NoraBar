using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal sealed record HomeWorldClockEntry(string Label, string TimeZoneId);

internal sealed record HomeHudSettings(
    HomeHudDesignVariant DesignVariant,
    HomeHudTimeFormat TimeFormat,
    IReadOnlyList<HomeWorldClockEntry>? WorldClocks,
    IReadOnlyList<HomeWidgetConfig>? Widgets = null,
    double MaxWidgetWidth = 800,
    double MaxWidgetHeight = 300)
{
    private static readonly IReadOnlyList<HomeWidgetConfig> DefaultWidgetsList = new List<HomeWidgetConfig>
    {
        new("widget_clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
        new("widget_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)
    }.AsReadOnly();

    public static IReadOnlyList<HomeWorldClockEntry> DefaultWorldClocksList { get; } = new List<HomeWorldClockEntry>
    {
        new("LOCAL", "Local"),
        new("NYC", "Eastern Standard Time"),
        new("LON", "GMT Standard Time")
    }.AsReadOnly();

    public IReadOnlyList<HomeWidgetConfig> EffectiveWidgets => Widgets is { Count: > 0 } ? Widgets : DefaultWidgetsList;

    public IReadOnlyList<HomeWorldClockEntry> EffectiveWorldClocks => WorldClocks is { Count: > 0 } ? WorldClocks : DefaultWorldClocksList;

    internal static HomeHudSettings Default { get; } = new(
        HomeHudDesignVariant.FusionBalanced,
        HomeHudTimeFormat.System,
        DefaultWorldClocksList,
        DefaultWidgetsList,
        800,
        300);
}
