using System.Windows;
using System.Windows.Input;
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
    public void StartDrag_WithInvalidIndex_LeavesItemsUnchangedAndIdle()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var item = new FrameworkElement { Width = 120, Height = 40 };
            var originalTransform = new RotateTransform(10);
            item.RenderTransform = originalTransform;
            panel.Children.Add(item);
            var helper = new WrapPanelAnimatedReorderHelper(panel, (_, _) => { });

            helper.StartDrag(item, new Point(10, 10), index: 1);

            Assert.Same(originalTransform, item.RenderTransform);
            Assert.Null(item.Effect);
            Assert.False(item.IsMouseCaptured);
            Assert.Equal(0, System.Windows.Controls.Panel.GetZIndex(item));
        });
    }

    [Fact]
    public void UpdateDrag_WhenItemsChange_CancelsWithoutCommittingAndRestoresItem()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var first = new FrameworkElement { Width = 120, Height = 40 };
            var dragged = new FrameworkElement { Width = 120, Height = 40 };
            var originalTransform = new RotateTransform(10);
            dragged.RenderTransform = originalTransform;
            panel.Children.Add(first);
            panel.Children.Add(dragged);
            var commits = new List<(int From, int To)>();
            var helper = new WrapPanelAnimatedReorderHelper(
                panel,
                (from, to) => commits.Add((from, to)));

            helper.StartDrag(dragged, new Point(150, 20), index: 1);
            panel.Children.Remove(first);
            helper.UpdateDrag(new Point(10, 20));
            helper.EndDrag();

            Assert.Empty(commits);
            Assert.Same(originalTransform, dragged.RenderTransform);
            Assert.Null(dragged.Effect);
            Assert.False(dragged.IsMouseCaptured);
            Assert.Equal(0, System.Windows.Controls.Panel.GetZIndex(dragged));
        });
    }

    [Fact]
    public void StartDrag_ThenCancel_RestoresAllOriginalTransforms()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var item1 = new FrameworkElement { Width = 120, Height = 40 };
            var item2 = new FrameworkElement { Width = 120, Height = 40 };
            var originalTransform1 = new RotateTransform(10);
            var originalTransform2 = new ScaleTransform(1.5, 1.5);
            item1.RenderTransform = originalTransform1;
            item2.RenderTransform = originalTransform2;
            panel.Children.Add(item1);
            panel.Children.Add(item2);
            var helper = new WrapPanelAnimatedReorderHelper(panel, (_, _) => { });

            helper.StartDrag(item1, new Point(10, 10), index: 0);
            
            // Transform has been replaced temporarily
            Assert.NotSame(originalTransform1, item1.RenderTransform);
            Assert.NotSame(originalTransform2, item2.RenderTransform);

            helper.CancelDrag();

            Assert.Same(originalTransform1, item1.RenderTransform);
            Assert.Same(originalTransform2, item2.RenderTransform);
        });
    }

    [Fact]
    public void StartDrag_WhenMouseCaptureLost_RestoresAllOriginalTransforms()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var item1 = new FrameworkElement { Width = 120, Height = 40 };
            var item2 = new FrameworkElement { Width = 120, Height = 40 };
            var originalTransform1 = new RotateTransform(10);
            var originalTransform2 = new ScaleTransform(1.5, 1.5);
            item1.RenderTransform = originalTransform1;
            item2.RenderTransform = originalTransform2;
            panel.Children.Add(item1);
            panel.Children.Add(item2);
            var helper = new WrapPanelAnimatedReorderHelper(panel, (_, _) => { });

            helper.StartDrag(item1, new Point(10, 10), index: 0);
            item1.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0)
            {
                RoutedEvent = UIElement.LostMouseCaptureEvent
            });

            Assert.Same(originalTransform1, item1.RenderTransform);
            Assert.Same(originalTransform2, item2.RenderTransform);
        });
    }

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

                Assert.Same(Transform.Identity, wideItem.RenderTransform);
                Assert.Same(Transform.Identity, narrowItem.RenderTransform);
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
