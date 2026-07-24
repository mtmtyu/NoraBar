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
    public void Calculate_WithActiveWidgets_UsesConfiguredViewportWidth()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HudSize size = HomeHudLayout.Calculate(
            HomeHudDesignVariant.FusionBalanced,
            widgets);

        Assert.Equal(800, size.Width);
        Assert.Equal(150, size.Height);
    }

    [Fact]
    public void Calculate_WithManyWidgets_UsesTheSameWrappingWidthAsTheView()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("w1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w2", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics),
            new("w3", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaBlurLyrics)
        };

        HudSize size = HomeHudLayout.Calculate(
            HomeHudDesignVariant.FusionBalanced,
            widgets,
            maxWidgetWidth: 600,
            maxWidgetHeight: 500);

        Assert.Equal(600, size.Width);
        Assert.Equal(418, size.Height);
    }

    [Fact]
    public void Calculate_AppendingLargeWidget_PreservesEarlierRowAssignments()
    {
        var widgets = new List<HomeWidgetConfig>
        {
            new("clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal),
            new("compact", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact),
            new("large1", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaArtworkHoverLarge)
        };

        HudSize withOneLarge = HomeHudLayout.Calculate(
            HomeHudDesignVariant.FusionBalanced,
            widgets,
            maxWidgetWidth: 500,
            maxWidgetHeight: 800);

        widgets.Add(new HomeWidgetConfig(
            "large2",
            HomeWidgetType.MediaControls,
            HomeWidgetStyle.MediaArtworkHoverLarge));

        HudSize withTwoLarge = HomeHudLayout.Calculate(
            HomeHudDesignVariant.FusionBalanced,
            widgets,
            maxWidgetWidth: 500,
            maxWidgetHeight: 800);

        Assert.Equal(new HudSize(500, 195), withOneLarge);
        Assert.Equal(new HudSize(500, 334), withTwoLarge);
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

        HudSize size = HomeHudLayout.Calculate(
            HomeHudDesignVariant.FusionBalanced,
            widgets,
            maxWidgetWidth: 650,
            maxWidgetHeight: 250);

        Assert.Equal(650, size.Width);
        Assert.Equal(250, size.Height);
    }

    [Theory]
    [InlineData(HomeWidgetStyle.ClockMinimal, 120, 40)]
    [InlineData(HomeWidgetStyle.MediaCompact, 236, 40)]
    [InlineData(HomeWidgetStyle.MediaArtworkHoverSmall, 158, 89)]
    [InlineData(HomeWidgetStyle.MediaArtworkHoverMedium, 198, 109)]
    [InlineData(HomeWidgetStyle.MediaArtworkHoverLarge, 248, 139)]
    [InlineData(HomeWidgetStyle.MediaBlurLyrics, 288, 134)]
    public void WidgetMetrics_MatchRenderedOuterSize(
        HomeWidgetStyle style,
        double expectedWidth,
        double expectedHeight)
    {
        HomeWidgetLayoutSize size = HomeWidgetLayoutMetrics.GetSize(style);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
    }
}
