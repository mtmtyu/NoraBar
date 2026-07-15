using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using NoraBar.Hud;
using NoraBar.Hud.Music;
using NoraBar.Models;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class MusicHudPreviewFactoryTests
{
    [Theory]
    [InlineData(DesignVariant.MinimalFloatingPill)]
    [InlineData(DesignVariant.ProductivityCommandIsland)]
    [InlineData(DesignVariant.LyricsFocusedSidebar)]
    public void Create_EachRequestReturnsNewViewForSelectedVariant(DesignVariant variant)
    {
        RunInSta(() =>
        {
            var dataContext = new object();
            var createdVariants = new List<DesignVariant>();
            Border CreateView(DesignVariant selectedVariant)
            {
                createdVariants.Add(selectedVariant);
                return new Border();
            }

            MusicHudPreview first = MusicHudPreviewFactory.Create(
                variant,
                showProgressBar: false,
                showLyrics: false,
                hasMultipleSessions: false,
                dataContext,
                CreateView);
            MusicHudPreview second = MusicHudPreviewFactory.Create(
                variant,
                showProgressBar: false,
                showLyrics: false,
                hasMultipleSessions: false,
                dataContext,
                CreateView);

            Assert.Equal([variant, variant], createdVariants);
            Assert.NotSame(first.View, second.View);
            Assert.Same(dataContext, first.View.DataContext);
            Assert.Same(dataContext, second.View.DataContext);
        });
    }

    [Fact]
    public void Create_ProductivityPreviewIncludesMultipleSessionHeight()
    {
        RunInSta(() =>
        {
            MusicHudPreview preview = MusicHudPreviewFactory.Create(
                DesignVariant.ProductivityCommandIsland,
                showProgressBar: true,
                showLyrics: true,
                hasMultipleSessions: true,
                new object(),
                _ => new Border());

            Assert.Equal(new HudSize(560, 160), preview.PreferredSize);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
