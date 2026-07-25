using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NoraBar.Controls;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
using NoraBar.ViewModels;

namespace NoraBar.Views.Home.Widgets;

public partial class MediaControlsWidgetView : UserControl, IDisposable
{
    private const int LyricContainerRetryDelayMilliseconds = 50;
    private const int MaxLyricContainerRetryCount = 1;

    private MusicViewModel? _musicVm;
    private DispatcherOperation? _pendingLyricScroll;
    private CancellationTokenSource? _lyricScrollCancellation;
    private HomeWidgetStyle _currentStyle = HomeWidgetStyle.MediaCompact;
    private bool _isDisposed;

    public MediaControlsWidgetView()
    {
        InitializeComponent();
        MediaContentControl.ContentTemplate = Resources["MediaCompactTemplate"] as DataTemplate;
        DataContextChanged += MediaControlsWidgetView_DataContextChanged;
        Loaded += MediaControlsWidgetView_Loaded;
        Unloaded += MediaControlsWidgetView_Unloaded;
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
            ScheduleLyricScroll();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DataContextChanged -= MediaControlsWidgetView_DataContextChanged;
        Loaded -= MediaControlsWidgetView_Loaded;
        Unloaded -= MediaControlsWidgetView_Unloaded;
        DetachMusicViewModel();
        CancelPendingLyricScroll();
        DataContext = null;
    }

    private void MediaControlsWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _musicVm = GetMusicViewModel();
        AttachMusicViewModel();
        if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
        {
            ScheduleLyricScroll();
        }
    }

    private void MediaControlsWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachMusicViewModel();
        CancelPendingLyricScroll();
    }

    private void MediaControlsWidgetView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachMusicViewModel();

        _musicVm = GetMusicViewModel();
        if (!_isDisposed && IsLoaded && _musicVm != null)
        {
            AttachMusicViewModel();
            if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
            {
                ScheduleLyricScroll();
            }
        }
    }

    private void AttachMusicViewModel()
    {
        if (_musicVm is null)
        {
            return;
        }

        _musicVm.PropertyChanged -= MusicVm_PropertyChanged;
        _musicVm.PropertyChanged += MusicVm_PropertyChanged;
    }

    private void DetachMusicViewModel()
    {
        if (_musicVm is not null)
        {
            _musicVm.PropertyChanged -= MusicVm_PropertyChanged;
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
            ScheduleLyricScroll();
        }
    }

    private void ScheduleLyricScroll()
    {
        CancelPendingLyricScroll();
        if (_isDisposed || !IsLoaded)
        {
            return;
        }

        _lyricScrollCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _lyricScrollCancellation.Token;
        _pendingLyricScroll = Dispatcher.InvokeAsync(
            () =>
            {
                _pendingLyricScroll = null;
                ScrollToCurrentLyric(cancellationToken, 0);
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ScrollToCurrentLyric(
        CancellationToken cancellationToken,
        int retryCount)
    {
        if (_musicVm == null
            || _isDisposed
            || !IsLoaded
            || cancellationToken.IsCancellationRequested)
        {
            return;
        }

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
                    if (retryCount < MaxLyricContainerRetryCount)
                    {
                        _ = RetryLyricScrollAsync(cancellationToken, retryCount + 1);
                    }
                }
            }
            else
            {
                lyricsListBox.ScrollIntoView(targetItem);
            }
        }
    }

    private async Task RetryLyricScrollAsync(
        CancellationToken cancellationToken,
        int retryCount)
    {
        try
        {
            await Task.Delay(
                LyricContainerRetryDelayMilliseconds,
                cancellationToken);
            await Dispatcher.InvokeAsync(
                () => ScrollToCurrentLyric(cancellationToken, retryCount),
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPendingLyricScroll()
    {
        if (_pendingLyricScroll?.Status == DispatcherOperationStatus.Pending)
        {
            _pendingLyricScroll.Abort();
        }

        _pendingLyricScroll = null;
        _lyricScrollCancellation?.Cancel();
        _lyricScrollCancellation?.Dispose();
        _lyricScrollCancellation = null;
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
