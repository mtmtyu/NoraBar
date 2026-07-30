using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NoraBar.Hud.Home.Widgets;
using NoraBar.Views.Helpers;
using NoraBar.Views.Home;
using Xunit;

namespace NoraBar.Tests.Views;

public sealed class WrapPanelAnimatedReorderHelperTests
{
    [Fact]
    public void UpdateDrag_MovingNarrowItemBeforeWideItem_PreviewsReorderedLayout()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var hostWindow = new Window
            {
                Width = 1000,
                Height = 100,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = panel
            };
            var wideItem = new FrameworkElement
            {
                Width = 300,
                Height = 80
            };
            var narrowItem = new FrameworkElement
            {
                Width = 120,
                Height = 40
            };
            panel.Children.Add(wideItem);
            panel.Children.Add(narrowItem);
            try
            {
                hostWindow.Show();
                hostWindow.UpdateLayout();

                var helper = new WrapPanelAnimatedReorderHelper(panel, (_, _) => { });
                Point narrowItemPosition = narrowItem.TranslatePoint(new Point(), panel);
                helper.StartDrag(
                    narrowItem,
                    new Point(
                        narrowItemPosition.X + (narrowItem.ActualWidth / 2.0),
                        narrowItemPosition.Y + (narrowItem.ActualHeight / 2.0)),
                    index: 1);

                helper.UpdateDrag(new Point(0, 20));
                RunDispatcherFor(TimeSpan.FromMilliseconds(200));

                var wideItemTranslation = Assert.IsType<TranslateTransform>(
                    wideItem.RenderTransform);
                double expectedOffset =
                    narrowItem.ActualWidth + HomeWidgetLayoutMetrics.InterWidgetSpacing;
                Assert.Equal(expectedOffset, wideItemTranslation.X, precision: 3);

                helper.CancelDrag();
            }
            finally
            {
                hostWindow.Close();
            }
        });
    }

    private static void RunDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            duration,
            DispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }
}
