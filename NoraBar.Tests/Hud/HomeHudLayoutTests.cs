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
    public void Calculate_WithManyWidgets_WrapsToMultipleLinesWhenExceedingMaxWidth()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics), // w:280, h:130
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics), // w:280, h:130
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)  // w:280, h:130
        };

        // maxWidgetWidth = 600, maxWidgetHeight = 500
        HudSize size = HomeHudLayout.Calculate(HomeHudDesignVariant.FusionBalanced, widgets, maxWidgetWidth: 600, maxWidgetHeight: 500);

        // Line 1: w1 (280) + 21 + w2 (280) = 581 <= 600 - 24 = 576 -> exceeds 576, so w2 wraps to Line 2!
        // Line 1: 280 (h: 130), Line 2: 280 (h: 130), Line 3: 280 (h: 130)
        // Calculated height: 130 + 8 + 130 + 8 + 130 + 16 = 422
        Assert.True(size.Height >= 280);
        Assert.True(size.Width <= 700); // base width 700
    }

    [Fact]
    public void Calculate_ClampsToMaxWidgetWidthAndHeight()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w4", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HudSize size = HomeHudLayout.Calculate(HomeHudDesignVariant.FusionBalanced, widgets, maxWidgetWidth: 650, maxWidgetHeight: 250);

        Assert.Equal(650, size.Width);
        Assert.Equal(250, size.Height);
    }
}
