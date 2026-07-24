using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal sealed record HomeWorldClockSettings(string Label, string TimeZoneId);

internal sealed record HomeHudSettings(
    HomeHudDesignVariant DesignVariant,
    HomeHudTimeFormat TimeFormat,
    HomeWorldClockSettings FirstClock,
    HomeWorldClockSettings SecondClock,
    IReadOnlyList<HomeWidgetConfig>? Widgets = null,
    double MaxWidgetWidth = 800,
    double MaxWidgetHeight = 300)
{
    private static readonly IReadOnlyList<HomeWidgetConfig> DefaultWidgetsList = new List<HomeWidgetConfig>
    {
        new("widget_clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
        new("widget_media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)
    }.AsReadOnly();

    public IReadOnlyList<HomeWidgetConfig> EffectiveWidgets => Widgets is { Count: > 0 } ? Widgets : DefaultWidgetsList;

    internal static HomeHudSettings Default { get; } = new(
        HomeHudDesignVariant.FusionBalanced,
        HomeHudTimeFormat.System,
        new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
        new HomeWorldClockSettings("LON", "GMT Standard Time"),
        DefaultWidgetsList,
        800,
        300);
}
