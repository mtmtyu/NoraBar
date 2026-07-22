using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal sealed record HomeWorldClockSettings(string Label, string TimeZoneId);

internal sealed record HomeHudSettings(
    HomeHudDesignVariant DesignVariant,
    HomeHudTimeFormat TimeFormat,
    HomeWorldClockSettings FirstClock,
    HomeWorldClockSettings SecondClock,
    IReadOnlyList<HomeWidgetConfig>? Widgets = null)
{
    private static readonly IReadOnlyList<HomeWidgetConfig> DefaultWidgetsList = new List<HomeWidgetConfig>
    {
        new("widget_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact),
        new("widget_clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockExpressive),
        new("widget_worldclock", HomeWidgetType.WorldClock, HomeWidgetStyle.WorldClockCompact)
    }.AsReadOnly();

    public IReadOnlyList<HomeWidgetConfig> EffectiveWidgets => Widgets is { Count: > 0 } ? Widgets : DefaultWidgetsList;

    internal static HomeHudSettings Default { get; } = new(
        HomeHudDesignVariant.FusionBalanced,
        HomeHudTimeFormat.System,
        new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
        new HomeWorldClockSettings("LON", "GMT Standard Time"),
        DefaultWidgetsList);
}
