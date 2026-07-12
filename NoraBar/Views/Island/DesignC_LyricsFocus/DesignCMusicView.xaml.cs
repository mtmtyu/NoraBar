using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoraBar.Controls;
using NoraBar.ViewModels;

namespace NoraBar.Views.Island.DesignC_LyricsFocus
{
    public partial class DesignCMusicView : UserControl
    {
        private MusicViewModel? _musicVm;

        public DesignCMusicView()
        {
            InitializeComponent();
            this.DataContextChanged += DesignCMusicView_DataContextChanged;
        }

        private void DesignCMusicView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_musicVm != null)
            {
                _musicVm.PropertyChanged -= MusicVm_PropertyChanged;
            }

            if (DataContext is MainViewModel mainVm)
            {
                _musicVm = mainVm.Music;
                if (_musicVm != null)
                {
                    _musicVm.PropertyChanged += MusicVm_PropertyChanged;
                    ScrollToCurrentLyric();
                }
            }
        }

        private void MusicVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MusicViewModel.CurrentLyricIndex))
            {
                Dispatcher.InvokeAsync(ScrollToCurrentLyric, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void ScrollToCurrentLyric()
        {
            if (_musicVm == null || LyricsListBox == null) return;

            int index = _musicVm.CurrentLyricIndex;
            if (index >= 0 && index < LyricsListBox.Items.Count)
            {
                var targetItem = LyricsListBox.Items[index];
                var scrollViewer = FindVisualChild<ScrollViewer>(LyricsListBox);
                if (scrollViewer != null)
                {
                    var container = LyricsListBox.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(scrollViewer);
                        var offset = transform.Transform(new Point(0, 0));
                        
                        double targetOffset = scrollViewer.VerticalOffset + offset.Y - (scrollViewer.ViewportHeight / 2) + (container.ActualHeight / 2);
                        
                        if (targetOffset < 0) targetOffset = 0;
                        if (targetOffset > scrollViewer.ScrollableHeight) targetOffset = scrollViewer.ScrollableHeight;

                        if (Math.Abs(scrollViewer.VerticalOffset - targetOffset) > 1.0)
                        {
                            var animation = new DoubleAnimation
                            {
                                From = scrollViewer.VerticalOffset,
                                To = targetOffset,
                                Duration = TimeSpan.FromMilliseconds(400),
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                            };
                            
                            scrollViewer.BeginAnimation(ScrollViewerBehavior.AnimatedOffsetProperty, animation);
                        }
                    }
                    else
                    {
                        // Fallback scroll to make the element exist in layout
                        LyricsListBox.ScrollIntoView(targetItem);
                        Dispatcher.InvokeAsync(async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(50);
                            ScrollToCurrentLyric();
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                else
                {
                    LyricsListBox.ScrollIntoView(targetItem);
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}
