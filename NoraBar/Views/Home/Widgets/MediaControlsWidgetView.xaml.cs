using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoraBar.Controls;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;

namespace NoraBar.Views.Home.Widgets;

public partial class MediaControlsWidgetView : UserControl
{
    private MusicViewModel? _musicVm;
    private HomeWidgetStyle _currentStyle = HomeWidgetStyle.MediaCompact;

    public MediaControlsWidgetView()
    {
        InitializeComponent();
        MediaContentControl.ContentTemplate = Resources["MediaCompactTemplate"] as DataTemplate;
        DataContextChanged += MediaControlsWidgetView_DataContextChanged;
    }

    public void SetStyle(HomeWidgetStyle style)
    {
        _currentStyle = style;
        MediaContentControl.ContentTemplate = style switch
        {
            HomeWidgetStyle.MediaArtworkHoverSmall => Resources["MediaArtworkHoverSmallTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHoverMedium => Resources["MediaArtworkHoverMediumTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHoverLarge => Resources["MediaArtworkHoverLargeTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaArtworkHover => Resources["MediaArtworkHoverTemplate"] as DataTemplate,
            HomeWidgetStyle.MediaBlurLyrics => Resources["MediaBlurLyricsTemplate"] as DataTemplate,
            _ => Resources["MediaCompactTemplate"] as DataTemplate
        };

        if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
        {
            Dispatcher.InvokeAsync(ScrollToCurrentLyric, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void MediaControlsWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_musicVm != null)
        {
            _musicVm.PropertyChanged -= MusicVm_PropertyChanged;
        }

        _musicVm = GetMusicViewModel();
        if (_musicVm != null)
        {
            _musicVm.PropertyChanged += MusicVm_PropertyChanged;
            if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
            {
                Dispatcher.InvokeAsync(ScrollToCurrentLyric, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }

    private MusicViewModel? GetMusicViewModel()
    {
        if (DataContext is HomeHudViewModel homeVm)
        {
            return homeVm.Music;
        }
        if (DataContext is MainViewModel mainVm)
        {
            return mainVm.Music;
        }
        return null;
    }

    private void MusicVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics && e.PropertyName == nameof(MusicViewModel.CurrentLyricIndex))
        {
            Dispatcher.InvokeAsync(ScrollToCurrentLyric, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void ScrollToCurrentLyric()
    {
        if (_musicVm == null) return;

        ListBox? lyricsListBox = FindVisualChild<ListBox>(MediaContentControl);
        if (lyricsListBox == null || lyricsListBox.Name != "LyricsListBoxBlur") return;

        int index = _musicVm.CurrentLyricIndex;
        if (index >= 0 && index < lyricsListBox.Items.Count)
        {
            var targetItem = lyricsListBox.Items[index];
            var scrollViewer = FindVisualChild<ScrollViewer>(lyricsListBox);
            if (scrollViewer != null)
            {
                var container = lyricsListBox.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
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
                    lyricsListBox.ScrollIntoView(targetItem);
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                        ScrollToCurrentLyric();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            else
            {
                lyricsListBox.ScrollIntoView(targetItem);
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
