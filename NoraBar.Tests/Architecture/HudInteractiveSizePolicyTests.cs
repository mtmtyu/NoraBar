using System.Windows;
using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class HudInteractiveSizePolicyTests
{
    [Fact]
    public void ResolveTargets_PreservesInteractiveAreaWithoutStretchingContent()
    {
        var preferredContentSize = new HudSize(450, 80);
        var desiredContainerSize = new HudSize(498, 80);
        var currentContainerSize = new HudSize(848, 120);

        HudInteractiveSizeTargets result = HudInteractiveSizePolicy.ResolveTargets(
            preferredContentSize,
            desiredContainerSize,
            currentContainerSize,
            isPointerOver: true,
            usesRightRailNavigation: true);

        Assert.Equal(currentContainerSize, result.ContainerSize);
        Assert.Equal(preferredContentSize, result.ContentSize);
        Assert.Equal(HorizontalAlignment.Right, result.ContentHorizontalAlignment);
    }

    [Fact]
    public void ResolveTargets_UsesCenterAlignmentWithoutRightRailNavigation()
    {
        var preferredContentSize = new HudSize(450, 80);

        HudInteractiveSizeTargets result = HudInteractiveSizePolicy.ResolveTargets(
            preferredContentSize,
            new HudSize(450, 80),
            new HudSize(450, 80),
            isPointerOver: false,
            usesRightRailNavigation: false);

        Assert.Equal(HorizontalAlignment.Center, result.ContentHorizontalAlignment);
    }

    [Fact]
    public void ApplyContentLayout_AppliesPreferredSizeAndAlignmentToHost()
    {
        StaTestRunner.Run(() =>
        {
            var contentHost = new FrameworkElement();
            var targets = new HudInteractiveSizeTargets(
                new HudSize(848, 120),
                new HudSize(450, 80),
                HorizontalAlignment.Right);

            HudInteractiveSizePolicy.ApplyContentLayout(contentHost, targets);

            Assert.Equal(450, contentHost.Width);
            Assert.Equal(80, contentHost.Height);
            Assert.Equal(HorizontalAlignment.Right, contentHost.HorizontalAlignment);
        });
    }

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
