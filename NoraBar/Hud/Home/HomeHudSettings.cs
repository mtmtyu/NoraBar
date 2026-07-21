using NoraBar.Models;

namespace NoraBar.Hud.Home;

internal sealed record HomeWorldClockSettings(string Label, string TimeZoneId);

internal sealed record HomeHudSettings(
    HomeHudDesignVariant DesignVariant,
    HomeHudTimeFormat TimeFormat,
    HomeWorldClockSettings FirstClock,
    HomeWorldClockSettings SecondClock)
{
    internal static HomeHudSettings Default { get; } = new(
        HomeHudDesignVariant.FusionBalanced,
        HomeHudTimeFormat.System,
        new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
        new HomeWorldClockSettings("LON", "GMT Standard Time"));
}
