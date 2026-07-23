using System.Collections.Generic;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HomeHudLayoutTests
{
    [Theory]
    [InlineData(HomeHudDesignVariant.ActivityModules, 800, 84)]
    [InlineData(HomeHudDesignVariant.ClassicSystemOverlay, 720, 120)]
    [InlineData(HomeHudDesignVariant.FusionBalanced, 700, 88)]
    [InlineData(HomeHudDesignVariant.FusionExpressive, 740, 108)]
    public void Calculate_ReturnsStableDesignSize(
        HomeHudDesignVariant variant,
        double width,
        double height)
    {
        Assert.Equal(new HudSize(width, height), HomeHudLayout.Calculate(variant));
    }

    [Fact]
    public void Calculate_WithActiveWidgets_ExpandsSizeForLargeWidgets()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HudSize size = HomeHudLayout.Calculate(HomeHudDesignVariant.FusionBalanced, widgets);

        // MediaBlurLyrics height 130 -> 130 + 20 = 150 > 88
        Assert.Equal(150, size.Height);
        // Base width 700 maintained since calculated width (449) is less than 700
        Assert.Equal(700, size.Width);
    }

    [Fact]
    public void Calculate_WithManyWidgets_ExpandsWidthWhenExceedingBase()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HudSize size = HomeHudLayout.Calculate(HomeHudDesignVariant.FusionBalanced, widgets);

        // Width = 28 + 280 + 21 + 280 + 21 + 280 = 910 > 700
        Assert.Equal(910, size.Width);
        Assert.Equal(150, size.Height);
    }
}
