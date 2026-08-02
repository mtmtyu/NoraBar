using NoraBar.Hud.Home.Widgets;
using NoraBar.Views.Home.Widgets;
using Xunit;

namespace NoraBar.Tests.Views;

public sealed class DigitalClockWidgetViewTests
{
    [Fact]
    public void SetStyle_RejectsUnsupportedStyles()
    {
        StaTestRunner.Run(() =>
        {
            var view = new DigitalClockWidgetView();

            view.SetStyle(HomeWidgetStyle.ClockMinimal);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => view.SetStyle(HomeWidgetStyle.MediaCompact));
        });
    }
}
