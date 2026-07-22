using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class HudInteractiveSizePolicyTests
{
    [Fact]
    public void ResolveTarget_PreservesCurrentSizeWhenPointerIsOverAndDesiredSizeShrinks()
    {
        var desiredSize = new HudSize(498, 80);
        var currentSize = new HudSize(848, 120);

        HudSize result = HudInteractiveSizePolicy.ResolveTarget(
            desiredSize,
            currentSize,
            isPointerOver: true);

        Assert.Equal(currentSize, result);
    }

    [Fact]
    public void ResolveTarget_AllowsGrowthWhilePointerIsOver()
    {
        var desiredSize = new HudSize(848, 120);
        var currentSize = new HudSize(498, 80);

        HudSize result = HudInteractiveSizePolicy.ResolveTarget(
            desiredSize,
            currentSize,
            isPointerOver: true);

        Assert.Equal(desiredSize, result);
    }

    [Fact]
    public void ResolveTarget_UsesDesiredSizeWhenPointerIsOutside()
    {
        var desiredSize = new HudSize(498, 80);
        var currentSize = new HudSize(848, 120);

        HudSize result = HudInteractiveSizePolicy.ResolveTarget(
            desiredSize,
            currentSize,
            isPointerOver: false);

        Assert.Equal(desiredSize, result);
    }
}
