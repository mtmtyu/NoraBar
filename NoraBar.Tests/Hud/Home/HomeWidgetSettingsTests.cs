using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Hud.Home;

public sealed class HomeWidgetSettingsTests
{
    [Fact]
    public void Read_WhenNoPayloadExists_ReturnsDefaultWithWidgets()
    {
        UserSettings settings = new UserSettings();
        HomeHudSettings result = HomeHudSettingsJson.Read(settings);

        Assert.NotNull(result);
        Assert.NotEmpty(result.EffectiveWidgets);
        Assert.Equal(3, result.EffectiveWidgets.Count);
    }

    [Fact]
    public void WriteAndRead_PreservesWidgetListAndStyles()
    {
        UserSettings settings = new UserSettings();
        List<HomeWidgetConfig> customWidgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockBoldGradient),
            new("w2", HomeWidgetType.SystemStatus, HomeWidgetStyle.SystemGauge),
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaExpanded)
        };

        HomeHudSettings customSettings = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.TwelveHour,
            new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
            new HomeWorldClockSettings("LON", "GMT Standard Time"),
            customWidgets);

        HomeHudSettingsJson.Write(settings, customSettings);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Equal(3, reloaded.EffectiveWidgets.Count);
        Assert.Equal("w1", reloaded.EffectiveWidgets[0].Id);
        Assert.Equal(HomeWidgetType.DigitalClock, reloaded.EffectiveWidgets[0].Type);
        Assert.Equal(HomeWidgetStyle.ClockBoldGradient, reloaded.EffectiveWidgets[0].Style);

        Assert.Equal("w2", reloaded.EffectiveWidgets[1].Id);
        Assert.Equal(HomeWidgetType.SystemStatus, reloaded.EffectiveWidgets[1].Type);
        Assert.Equal(HomeWidgetStyle.SystemGauge, reloaded.EffectiveWidgets[1].Style);

        Assert.Equal("w3", reloaded.EffectiveWidgets[2].Id);
        Assert.Equal(HomeWidgetType.MediaControls, reloaded.EffectiveWidgets[2].Type);
        Assert.Equal(HomeWidgetStyle.MediaExpanded, reloaded.EffectiveWidgets[2].Style);
    }
}
