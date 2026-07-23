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
        Assert.Equal(2, result.EffectiveWidgets.Count);
    }

    [Fact]
    public void WriteAndRead_PreservesWidgetListAndStyles()
    {
        UserSettings settings = new UserSettings();
        List<HomeWidgetConfig> customWidgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)
        };

        HomeHudSettings customSettings = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.TwelveHour,
            new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
            new HomeWorldClockSettings("LON", "GMT Standard Time"),
            customWidgets);

        HomeHudSettingsJson.Write(settings, customSettings);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Equal(2, reloaded.EffectiveWidgets.Count);
        Assert.Equal("w1", reloaded.EffectiveWidgets[0].Id);
        Assert.Equal(HomeWidgetType.DigitalClock, reloaded.EffectiveWidgets[0].Type);
        Assert.Equal(HomeWidgetStyle.ClockMinimal, reloaded.EffectiveWidgets[0].Style);

        Assert.Equal("w2", reloaded.EffectiveWidgets[1].Id);
        Assert.Equal(HomeWidgetType.MediaControls, reloaded.EffectiveWidgets[1].Type);
        Assert.Equal(HomeWidgetStyle.MediaCompact, reloaded.EffectiveWidgets[1].Style);
    }

    [Fact]
    public void WriteAndRead_PreservesMediaArtworkHoverStyle()
    {
        UserSettings settings = new UserSettings();
        List<HomeWidgetConfig> customWidgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHover)
        };

        HomeHudSettings customSettings = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.TwelveHour,
            new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
            new HomeWorldClockSettings("LON", "GMT Standard Time"),
            customWidgets);

        HomeHudSettingsJson.Write(settings, customSettings);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Single(reloaded.EffectiveWidgets);
        Assert.Equal("w1", reloaded.EffectiveWidgets[0].Id);
        Assert.Equal(HomeWidgetType.MediaControls, reloaded.EffectiveWidgets[0].Type);
        Assert.Equal(HomeWidgetStyle.MediaArtworkHover, reloaded.EffectiveWidgets[0].Style);
    }

    [Fact]
    public void WriteAndRead_PreservesMediaArtworkHoverSizeStyles()
    {
        UserSettings settings = new UserSettings();
        List<HomeWidgetConfig> customWidgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverSmall),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverMedium),
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge)
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
        Assert.Equal(HomeWidgetStyle.MediaArtworkHoverSmall, reloaded.EffectiveWidgets[0].Style);
        Assert.Equal(HomeWidgetStyle.MediaArtworkHoverMedium, reloaded.EffectiveWidgets[1].Style);
        Assert.Equal(HomeWidgetStyle.MediaArtworkHoverLarge, reloaded.EffectiveWidgets[2].Style);
    }

    [Fact]
    public void WriteAndRead_PreservesMediaBlurLyricsStyle()
    {
        UserSettings settings = new UserSettings();
        List<HomeWidgetConfig> customWidgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HomeHudSettings customSettings = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.TwelveHour,
            new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
            new HomeWorldClockSettings("LON", "GMT Standard Time"),
            customWidgets);

        HomeHudSettingsJson.Write(settings, customSettings);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Single(reloaded.EffectiveWidgets);
        Assert.Equal(HomeWidgetStyle.MediaBlurLyrics, reloaded.EffectiveWidgets[0].Style);
    }
}
