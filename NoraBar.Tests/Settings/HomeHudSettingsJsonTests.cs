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
    public void Read_UsesBalancedDesignAndDefaultWorldClocks()
    {
        var settings = new UserSettings();

        HomeHudSettings result = HomeHudSettingsJson.Read(settings);

        Assert.Equal(HomeHudDesignVariant.FusionBalanced, result.DesignVariant);
        Assert.Equal(HomeHudTimeFormat.System, result.TimeFormat);
        Assert.Equal(3, result.EffectiveWorldClocks.Count);
        Assert.Equal("LOCAL", result.EffectiveWorldClocks[0].Label);
        Assert.Equal("Local", result.EffectiveWorldClocks[0].TimeZoneId);
        Assert.Equal("NYC", result.EffectiveWorldClocks[1].Label);
        Assert.Equal("Eastern Standard Time", result.EffectiveWorldClocks[1].TimeZoneId);
        Assert.Equal("LON", result.EffectiveWorldClocks[2].Label);
        Assert.Equal("GMT Standard Time", result.EffectiveWorldClocks[2].TimeZoneId);
    }

    [Fact]
    public void Read_MigratesLegacyFirstAndSecondClock()
    {
        var settings = new UserSettings
        {
            Modules = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [BuiltInHudIds.Home] = JsonSerializer.SerializeToElement(new
                {
                    FirstClock = new { Label = "SEA", TimeZoneId = "Pacific Standard Time" },
                    SecondClock = new { Label = "TYO", TimeZoneId = "Tokyo Standard Time" }
                })
            }
        };

        HomeHudSettings result = HomeHudSettingsJson.Read(settings);

        Assert.Equal(2, result.EffectiveWorldClocks.Count);
        Assert.Equal("SEA", result.EffectiveWorldClocks[0].Label);
        Assert.Equal("Pacific Standard Time", result.EffectiveWorldClocks[0].TimeZoneId);
        Assert.Equal("TYO", result.EffectiveWorldClocks[1].Label);
        Assert.Equal("Tokyo Standard Time", result.EffectiveWorldClocks[1].TimeZoneId);
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
            new[]
            {
                new HomeWorldClockEntry("SEA", "Pacific Standard Time"),
                new HomeWorldClockEntry("TYO", "Tokyo Standard Time")
            });

        HomeHudSettingsJson.Write(settings, home);

        JsonElement payload = settings.Modules[BuiltInHudIds.Home];
        Assert.Equal((int)HomeHudDesignVariant.FusionExpressive, payload.GetProperty("DesignVariant").GetInt32());
        Assert.True(payload.GetProperty("FutureProperty").GetProperty("enabled").GetBoolean());
        JsonElement worldClocks = payload.GetProperty("WorldClocks");
        Assert.Equal(2, worldClocks.GetArrayLength());
        Assert.Equal("TYO", worldClocks[1].GetProperty("Label").GetString());
    }

    [Fact]
    public void WriteAndRead_PreservesMaxWidgetWidthAndHeight()
    {
        var settings = new UserSettings();
        var home = new HomeHudSettings(
            HomeHudDesignVariant.FusionBalanced,
            HomeHudTimeFormat.System,
            new[]
            {
                new HomeWorldClockEntry("NYC", "Eastern Standard Time"),
                new HomeWorldClockEntry("LON", "GMT Standard Time")
            },
            null,
            950,
            450);

        HomeHudSettingsJson.Write(settings, home);
        HomeHudSettings reloaded = HomeHudSettingsJson.Read(settings);

        Assert.Equal(950, reloaded.MaxWidgetWidth);
        Assert.Equal(450, reloaded.MaxWidgetHeight);
        Assert.Equal(2, reloaded.EffectiveWorldClocks.Count);
    }
}
