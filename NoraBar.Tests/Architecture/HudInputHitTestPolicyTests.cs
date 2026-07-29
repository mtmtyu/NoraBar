using System.Windows;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class HudInputHitTestPolicyTests
{
    [Fact]
    public void IsInteractive_ReturnsTrueInsideHud()
    {
        bool result = HudInputHitTestPolicy.IsInteractive(
            new Point(120, 40),
            new Rect(100, 20, 200, 100),
            null);

        Assert.True(result);
    }

    [Fact]
    public void IsInteractive_ReturnsTrueInsideVisibleWidgetPalette()
    {
        bool result = HudInputHitTestPolicy.IsInteractive(
            new Point(140, 180),
            new Rect(100, 20, 200, 100),
            new Rect(100, 150, 200, 80));

        Assert.True(result);
    }

    [Fact]
    public void IsInteractive_ReturnsFalseInTransparentWindowRegion()
    {
        bool result = HudInputHitTestPolicy.IsInteractive(
            new Point(20, 300),
            new Rect(100, 20, 200, 100),
            new Rect(100, 150, 200, 80));

        Assert.False(result);
    }
}