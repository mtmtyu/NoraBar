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
            var originalOrigin1 = new Point(0.2, 0.3);
            var originalOrigin2 = new Point(0.7, 0.8);
            item1.RenderTransform = originalTransform1;
            item2.RenderTransform = originalTransform2;
            item1.RenderTransformOrigin = originalOrigin1;
            item2.RenderTransformOrigin = originalOrigin2;
            panel.Children.Add(item1);
            panel.Children.Add(item2);
            var commits = new List<(int From, int To)>();
            var helper = new WrapPanelAnimatedReorderHelper(
                panel,
                (from, to) => commits.Add((from, to)));

            helper.StartDrag(item1, new Point(10, 10), index: 0);
            
            // Transform has been replaced temporarily
            Assert.NotSame(originalTransform1, item1.RenderTransform);
            Assert.NotSame(originalTransform2, item2.RenderTransform);

            helper.CancelDrag();

            Assert.Same(originalTransform1, item1.RenderTransform);
            Assert.Same(originalTransform2, item2.RenderTransform);
            Assert.Equal(originalOrigin1, item1.RenderTransformOrigin);
            Assert.Equal(originalOrigin2, item2.RenderTransformOrigin);
            Assert.Empty(commits);
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
            var originalOrigin1 = new Point(0.15, 0.25);
            var originalOrigin2 = new Point(0.65, 0.75);
            item1.RenderTransform = originalTransform1;
            item2.RenderTransform = originalTransform2;
            item1.RenderTransformOrigin = originalOrigin1;
            item2.RenderTransformOrigin = originalOrigin2;
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
            Assert.Equal(originalOrigin1, item1.RenderTransformOrigin);
            Assert.Equal(originalOrigin2, item2.RenderTransformOrigin);
        });
    }

    [Fact]
    public void EndDrag_AfterCommittedMove_RestoresExactTransformsAndOrigins()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var draggedTransform = new TransformGroup
            {
                Children =
                {
                    new RotateTransform(12),
                    new ScaleTransform(0.9, 1.1)
                }
            };
            draggedTransform.Freeze();
            var sharedTransform = new TranslateTransform(4, 7);
            sharedTransform.Freeze();
            var dragged = CreateItem(draggedTransform, new Point(0.2, 0.3));
            var second = CreateItem(sharedTransform, new Point(0.4, 0.5));
            var third = CreateItem(sharedTransform, new Point(0.6, 0.7));
            panel.Children.Add(dragged);
            panel.Children.Add(second);
            panel.Children.Add(third);
            var commits = new List<(int From, int To)>();
            var helper = new WrapPanelAnimatedReorderHelper(
                panel,
                (from, to) => commits.Add((from, to)));
            Window hostWindow = CreateHostWindow(panel);

            try
            {
                hostWindow.Show();
                hostWindow.UpdateLayout();

                helper.StartDrag(dragged, new Point(10, 20), index: 0);
                helper.UpdateDrag(new Point(130, 20));
                helper.EndDrag();

                Assert.Equal([(0, 1)], commits);
                Assert.Same(draggedTransform, dragged.RenderTransform);
                Assert.Same(sharedTransform, second.RenderTransform);
                Assert.Same(sharedTransform, third.RenderTransform);
                Assert.Equal(new Point(0.2, 0.3), dragged.RenderTransformOrigin);
                Assert.Equal(new Point(0.4, 0.5), second.RenderTransformOrigin);
                Assert.Equal(new Point(0.6, 0.7), third.RenderTransformOrigin);
                Assert.True(draggedTransform.IsFrozen);
                Assert.True(sharedTransform.IsFrozen);
            }
            finally
            {
                hostWindow.Close();
            }
        });
    }

    [Fact]
    public void EndDrag_RepeatedCommittedMoves_DoNotAccumulateTemporaryTransforms()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new HomeWidgetPanel();
            var firstTransform = new RotateTransform(8);
            var secondTransform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(1.1, 0.9),
                    new TranslateTransform(3, 5)
                }
            };
            var first = CreateItem(firstTransform, new Point(0.1, 0.2));
            var second = CreateItem(secondTransform, new Point(0.8, 0.9));
            panel.Children.Add(first);
            panel.Children.Add(second);
            var commits = new List<(int From, int To)>();
            var helper = new WrapPanelAnimatedReorderHelper(
                panel,
                (from, to) => commits.Add((from, to)));
            Window hostWindow = CreateHostWindow(panel);

            try
            {
                hostWindow.Show();
                hostWindow.UpdateLayout();

                helper.StartDrag(first, new Point(10, 20), index: 0);
                helper.UpdateDrag(new Point(130, 20));
                helper.EndDrag();
                Assert.Same(firstTransform, first.RenderTransform);
                Assert.Same(secondTransform, second.RenderTransform);

                helper.StartDrag(second, new Point(130, 20), index: 1);
                helper.UpdateDrag(new Point(1, 20));
                helper.EndDrag();

                Assert.Equal([(0, 1), (1, 0)], commits);
                Assert.Same(firstTransform, first.RenderTransform);
                Assert.Same(secondTransform, second.RenderTransform);
                Assert.Equal(new Point(0.1, 0.2), first.RenderTransformOrigin);
                Assert.Equal(new Point(0.8, 0.9), second.RenderTransformOrigin);
                Assert.Equal(2, secondTransform.Children.Count);
            }
            finally
            {
                hostWindow.Close();
            }
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

    private static FrameworkElement CreateItem(
        Transform transform,
        Point transformOrigin) => new()
    {
        Width = 120,
        Height = 40,
        RenderTransform = transform,
        RenderTransformOrigin = transformOrigin
    };

    private static Window CreateHostWindow(HomeWidgetPanel panel) => new()
    {
        Width = 600,
        Height = 100,
        Left = -10000,
        Top = -10000,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        Content = panel
    };

}
