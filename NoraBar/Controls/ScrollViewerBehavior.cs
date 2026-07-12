using System.Windows;
using System.Windows.Controls;

namespace NoraBar.Controls
{
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty AnimatedOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedOffset",
                typeof(double),
                typeof(ScrollViewerBehavior),
                new FrameworkPropertyMetadata(0.0, OnAnimatedOffsetChanged));

        public static double GetAnimatedOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(AnimatedOffsetProperty);
        }

        public static void SetAnimatedOffset(DependencyObject obj, double value)
        {
            obj.SetValue(AnimatedOffsetProperty, value);
        }

        private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }
}
