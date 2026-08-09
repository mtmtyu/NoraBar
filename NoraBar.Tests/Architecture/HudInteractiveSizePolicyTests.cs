using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class HudInteractiveSizePolicyTests
{
    [Fact]
    public void ResolveTargets_UsesDesiredContainerSizeWhenPointerIsOver()
    {
        var preferredContentSize = new HudSize(450, 80);
        var desiredContainerSize = new HudSize(498, 80);
        var currentContainerSize = new HudSize(848, 120);

        HudInteractiveSizeTargets result = HudInteractiveSizePolicy.ResolveTargets(
            preferredContentSize,
            desiredContainerSize,
            currentContainerSize,
            isPointerOver: true);

        Assert.Equal(desiredContainerSize, result.ContainerSize);
        Assert.Equal(preferredContentSize, result.ContentSize);
        Assert.False(result.StretchesContentWidth);
        Assert.False(result.StretchesContentHeight);
    }

    [Fact]
    public void ResolveTargets_StretchesContentDimensionsWhileContainerGrows()
    {
        var preferredContentSize = new HudSize(450, 80);
        var collapsedContainerSize = new HudSize(200, 2);

        HudInteractiveSizeTargets result = HudInteractiveSizePolicy.ResolveTargets(
            preferredContentSize,
            preferredContentSize,
            collapsedContainerSize,
            isPointerOver: true);

        Assert.True(result.StretchesContentWidth);
        Assert.True(result.StretchesContentHeight);
    }

    [Fact]
    public void ApplyContentLayout_DoesNotFixDimensionsWhileContainerGrows()
    {
        StaTestRunner.Run(() =>
        {
            var contentHost = new ContentControl();
            var targets = new HudInteractiveSizeTargets(
                new HudSize(848, 120),
                new HudSize(450, 80),
                StretchesContentWidth: true,
                StretchesContentHeight: true);

            HudInteractiveSizePolicy.ApplyContentLayout(contentHost, targets);

            var animatedContainer = new Grid();
            animatedContainer.Children.Add(contentHost);
            animatedContainer.Measure(new Size(450, 20));
            animatedContainer.Arrange(new Rect(0, 0, 450, 20));

            Assert.True(double.IsNaN(contentHost.Width));
            Assert.True(double.IsNaN(contentHost.Height));
            Assert.Equal(HorizontalAlignment.Stretch, contentHost.HorizontalAlignment);
            Assert.Equal(20, contentHost.ActualHeight);
            Assert.Equal(VerticalAlignment.Stretch, contentHost.VerticalAlignment);
            Assert.Equal(HorizontalAlignment.Stretch, contentHost.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Stretch, contentHost.VerticalContentAlignment);
        });
    }

    [Fact]
    public void ApplyContentLayout_KeepsPreferredDimensionsAndAlignsTopWhenContainerIsRetained()
    {
        StaTestRunner.Run(() =>
        {
            var contentHost = new ContentControl();
            var targets = new HudInteractiveSizeTargets(
                new HudSize(848, 120),
                new HudSize(450, 80),
                StretchesContentWidth: false,
                StretchesContentHeight: false);

            HudInteractiveSizePolicy.ApplyContentLayout(contentHost, targets);

            Assert.Equal(450, contentHost.Width);
            Assert.Equal(80, contentHost.Height);
            Assert.Equal(HorizontalAlignment.Center, contentHost.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, contentHost.VerticalAlignment);
        });
    }

    [Fact]
    public void ResolveTarget_ReturnsDesiredSizeWhenPointerIsOverAndDesiredSizeShrinks()
    {
        var desiredSize = new HudSize(498, 80);
        var currentSize = new HudSize(848, 120);

        HudSize result = HudInteractiveSizePolicy.ResolveTarget(
            desiredSize,
            currentSize,
            isPointerOver: true);

        Assert.Equal(desiredSize, result);
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
