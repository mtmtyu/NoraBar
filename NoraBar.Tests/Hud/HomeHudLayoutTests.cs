using NoraBar.Hud;
using NoraBar.Hud.Home;
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
}
