using System.Text.Json;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Settings;

public sealed class HomeHudSettingsJsonTests
{
    [Fact]
    public void Read_UsesBalancedDesignAndNewYorkLondonDefaults()
    {
        var settings = new UserSettings();

        HomeHudSettings result = HomeHudSettingsJson.Read(settings);

        Assert.Equal(HomeHudDesignVariant.FusionBalanced, result.DesignVariant);
        Assert.Equal(HomeHudTimeFormat.System, result.TimeFormat);
        Assert.Equal("NYC", result.FirstClock.Label);
        Assert.Equal("Eastern Standard Time", result.FirstClock.TimeZoneId);
        Assert.Equal("LON", result.SecondClock.Label);
        Assert.Equal("GMT Standard Time", result.SecondClock.TimeZoneId);
    }

    [Fact]
    public void Write_PreservesUnknownHomePayloadProperties()
    {
        var settings = new UserSettings
        {
            Modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [BuiltInHudIds.Home] = JsonSerializer.SerializeToElement(new
                {
                    DesignVariant = 0,
                    FutureProperty = new { enabled = true }
                })
            }
        };
        var home = new HomeHudSettings(
            HomeHudDesignVariant.FusionExpressive,
            HomeHudTimeFormat.TwentyFourHour,
            new HomeWorldClockSettings("SEA", "Pacific Standard Time"),
            new HomeWorldClockSettings("TYO", "Tokyo Standard Time"));

        HomeHudSettingsJson.Write(settings, home);

        JsonElement payload = settings.Modules[BuiltInHudIds.Home];
        Assert.Equal((int)HomeHudDesignVariant.FusionExpressive, payload.GetProperty("DesignVariant").GetInt32());
        Assert.True(payload.GetProperty("FutureProperty").GetProperty("enabled").GetBoolean());
        Assert.Equal("TYO", payload.GetProperty("SecondClock").GetProperty("Label").GetString());
    }

    [Fact]
    public void WriteAndRead_PreservesMaxWidgetWidthAndHeight()
    {
        var settings = new UserSettings();
        var home = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.System,
            new HomeWorldClockSettings("NYC", "Eastern Standard Time"),
            new HomeWorldClockSettings("LON", "GMT Standard Time"),
            null,
            950,
            450);

        HomeHudSettingsJson.Write(settings, home);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Equal(950, reloaded.MaxWidgetWidth);
        Assert.Equal(450, reloaded.MaxWidgetHeight);
    }
}
