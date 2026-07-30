using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using NoraBar.Views.Helpers;
using Xunit;

namespace NoraBar.Tests.Views;

public sealed class AnimatedReorderHelperTests
{
    [Fact]
    public void IsInteractiveControl_DetectsNestedSelectorsWithoutBlockingOrdinarySurfaces()
    {
        StaTestRunner.Run(() =>
        {
            var itemsControl = new ListBox();
            var helper = new AnimatedReorderHelper(itemsControl, (_, _) => { });
            MethodInfo method = typeof(AnimatedReorderHelper).GetMethod(
                "IsInteractiveControl",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Interactive-control detector was not found.");

            Assert.True(Invoke(new ComboBox()));
            Assert.True(Invoke(new ListBox()));
            Assert.False(Invoke(new Border()));
            Assert.False(Invoke(itemsControl));

            bool Invoke(DependencyObject source) =>
                Assert.IsType<bool>(method.Invoke(helper, new object?[] { source }));
        });
    }

    [Fact]
    public void AnimateSwap_RestoresOriginalTransformsAfterCompletion()
    {
        StaTestRunner.Run(() =>
        {
            var itemsControl = new ListBox
            {
                ItemsSource = new[] { "first", "second" }
            };
            var hostWindow = new Window
            {
                Width = 300,
                Height = 200,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = itemsControl
            };

            try
            {
                hostWindow.Show();
                hostWindow.UpdateLayout();

                var first = Assert.IsType<ListBoxItem>(itemsControl.ItemContainerGenerator.ContainerFromIndex(0));
                var second = Assert.IsType<ListBoxItem>(itemsControl.ItemContainerGenerator.ContainerFromIndex(1));
                var firstTransform = new TransformGroup
                {
                    Children =
                    {
                        new TranslateTransform(4, 7),
                        new ScaleTransform(0.9, 0.8)
                    }
                };
                var secondTransform = new RotateTransform(12);
                first.RenderTransform = firstTransform;
                second.RenderTransform = secondTransform;

                var helper = new AnimatedReorderHelper(itemsControl, (_, _) => { });

                helper.AnimateSwap(0, 1);
                RunDispatcherFor(TimeSpan.FromMilliseconds(250));

                Assert.Same(firstTransform, first.RenderTransform);
                Assert.Same(secondTransform, second.RenderTransform);
                Assert.Equal(7, Assert.IsType<TranslateTransform>(firstTransform.Children[0]).Y);
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
