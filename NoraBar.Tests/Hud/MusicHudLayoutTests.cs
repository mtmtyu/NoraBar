using NoraBar.Hud;
using NoraBar.Hud.Music;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class MusicHudLayoutTests
{
    [Theory]
    [InlineData(false, false, false, 450, 80)]
    [InlineData(true, false, false, 450, 106)]
    [InlineData(false, true, false, 450, 104)]
    [InlineData(false, false, true, 450, 92)]
    [InlineData(true, true, true, 450, 142)]
    public void Calculate_MinimalMatchesExistingLayout(
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions,
        double expectedWidth,
        double expectedHeight)
    {
        HudSize result = MusicHudLayout.Calculate(
            DesignVariant.MinimalFloatingPill,
            showProgressBar,
            showLyrics,
            hasMultipleSessions);

        Assert.Equal(new HudSize(expectedWidth, expectedHeight), result);
    }

    [Theory]
    [InlineData(false, false, false, 560, 90)]
    [InlineData(true, false, false, 560, 120)]
    [InlineData(false, true, false, 560, 114)]
    [InlineData(false, false, true, 560, 106)]
    [InlineData(true, true, true, 560, 160)]
    public void Calculate_ProductivityMatchesExistingLayout(
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions,
        double expectedWidth,
        double expectedHeight)
    {
        HudSize result = MusicHudLayout.Calculate(
            DesignVariant.ProductivityCommandIsland,
            showProgressBar,
            showLyrics,
            hasMultipleSessions);

        Assert.Equal(new HudSize(expectedWidth, expectedHeight), result);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    public void Calculate_LyricsFocusAlwaysUsesFixedSize(
        bool showProgressBar,
        bool showLyrics,
        bool hasMultipleSessions)
    {
        HudSize result = MusicHudLayout.Calculate(
            DesignVariant.LyricsFocusedSidebar,
            showProgressBar,
            showLyrics,
            hasMultipleSessions);

        Assert.Equal(new HudSize(650, 180), result);
    }

    [Fact]
    public void Calculate_UnknownVariantThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicHudLayout.Calculate(
            (DesignVariant)(-1),
            showProgressBar: false,
            showLyrics: false,
            hasMultipleSessions: false));
    }
}
