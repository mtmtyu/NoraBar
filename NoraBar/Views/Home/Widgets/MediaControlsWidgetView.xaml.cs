using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
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
    private const int MaxLyricContainerAttempts = 5;

    private readonly Func<bool> _isLoaded;
    private IMusicChangeSource? _musicSource;
    private DispatcherOperation? _pendingLyricScroll;
    private CancellationTokenSource? _lyricScrollCancellation;
    private HomeWidgetStyle _currentStyle = HomeWidgetStyle.MediaCompact;
    private bool _isDisposed;

    public MediaControlsWidgetView()
        : this(null)
    {
    }

    internal MediaControlsWidgetView(Func<bool>? isLoaded)
    {
        _isLoaded = isLoaded ?? (() => IsLoaded);
        InitializeComponent();
        MediaContentControl.ContentTemplate = Resources["MediaCompactTemplate"] as DataTemplate;
        DataContextChanged += MediaControlsWidgetView_DataContextChanged;
        Loaded += MediaControlsWidgetView_Loaded;
        Unloaded += MediaControlsWidgetView_Unloaded;
    }

    internal bool HasPendingLyricScroll =>
        _pendingLyricScroll is not null || _lyricScrollCancellation is not null;

    public void SetStyle(HomeWidgetStyle style)
    {
        if (_isDisposed)
        {
            return;
        }

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
        else
        {
            CancelPendingLyricScroll();
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
        DetachMusicSource();
        CancelPendingLyricScroll();
        DataContext = null;
    }

    private void MediaControlsWidgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _musicSource = GetMusicSource();
        AttachMusicSource();
        if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
        {
            ScheduleLyricScroll();
        }
    }

    private void MediaControlsWidgetView_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachMusicSource();
        CancelPendingLyricScroll();
    }

    private void MediaControlsWidgetView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        DetachMusicSource();
        CancelPendingLyricScroll();
        _musicSource = GetMusicSource();
        if (!_isDisposed && _isLoaded() && _musicSource is not null)
        {
            AttachMusicSource();
            if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics)
            {
                ScheduleLyricScroll();
            }
        }
    }

    private void AttachMusicSource()
    {
        if (_musicSource is null)
        {
            return;
        }

        _musicSource.PropertyChanged -= MusicSource_PropertyChanged;
        _musicSource.PropertyChanged += MusicSource_PropertyChanged;
    }

    private void DetachMusicSource()
    {
        if (_musicSource is not null)
        {
            _musicSource.PropertyChanged -= MusicSource_PropertyChanged;
        }
    }

    private IMusicChangeSource? GetMusicSource()
    {
        if (DataContext is HomeHudViewModel homeViewModel)
        {
            return homeViewModel.Music;
        }

        if (DataContext is MainViewModel mainViewModel)
        {
            return mainViewModel.Music;
        }

        return DataContext as IMusicChangeSource;
    }

    private void MusicSource_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentStyle == HomeWidgetStyle.MediaBlurLyrics
            && e.PropertyName == nameof(IMusicChangeSource.CurrentLyricIndex))
        {
            ScheduleLyricScroll();
        }
    }

    private void ScheduleLyricScroll()
    {
        CancelPendingLyricScroll();
        if (!CanScrollLyrics())
        {
            return;
        }

        _lyricScrollCancellation = new CancellationTokenSource();
        QueueLyricScrollAttempt(_lyricScrollCancellation.Token, 1);
    }

    private void QueueLyricScrollAttempt(
        CancellationToken cancellationToken,
        int attempt)
    {
        if (!CanScrollLyrics() || cancellationToken.IsCancellationRequested)
        {
            CompleteLyricScroll(cancellationToken);
            return;
        }

        _pendingLyricScroll = Dispatcher.InvokeAsync(
            () => ExecuteLyricScrollAttempt(cancellationToken, attempt),
            DispatcherPriority.ContextIdle,
            cancellationToken);
    }

    private void ExecuteLyricScrollAttempt(
        CancellationToken cancellationToken,
        int attempt)
    {
        _pendingLyricScroll = null;
        try
        {
            if (!TryScrollToCurrentLyric(cancellationToken)
                && attempt < MaxLyricContainerAttempts)
            {
                QueueLyricScrollAttempt(cancellationToken, attempt + 1);
                return;
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Home HUD lyric scrolling failed: {exception}");
        }

        CompleteLyricScroll(cancellationToken);
    }

    private bool TryScrollToCurrentLyric(CancellationToken cancellationToken)
    {
        if (!CanScrollLyrics() || cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        ListBox? lyricsListBox = FindVisualChild<ListBox>(MediaContentControl);
        if (lyricsListBox is null || lyricsListBox.Name != "LyricsListBoxBlur")
        {
            return false;
        }

        int index = _musicSource!.CurrentLyricIndex;
        if (index < 0 || index >= lyricsListBox.Items.Count)
        {
            return true;
        }

        object targetItem = lyricsListBox.Items[index];
        var container = lyricsListBox.ItemContainerGenerator.ContainerFromIndex(index)
            as FrameworkElement;
        if (container is null)
        {
            lyricsListBox.ScrollIntoView(targetItem);
            return false;
        }

        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(lyricsListBox);
        if (scrollViewer is null)
        {
            lyricsListBox.ScrollIntoView(targetItem);
            return false;
        }

        GeneralTransform transform = container.TransformToVisual(scrollViewer);
        Point offset = transform.Transform(new Point(0, 0));
        double targetOffset = scrollViewer.VerticalOffset
            + offset.Y
            - (scrollViewer.ViewportHeight / 2)
            + (container.ActualHeight / 2);
        targetOffset = Math.Clamp(targetOffset, 0, scrollViewer.ScrollableHeight);

        if (Math.Abs(scrollViewer.VerticalOffset - targetOffset) <= 1.0)
        {
            return true;
        }

        var animation = new DoubleAnimation
        {
            From = scrollViewer.VerticalOffset,
            To = targetOffset,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scrollViewer.BeginAnimation(
            ScrollViewerBehavior.AnimatedOffsetProperty,
            animation);
        return true;
    }

    private bool CanScrollLyrics() =>
        !_isDisposed
        && _isLoaded()
        && _musicSource is not null
        && _currentStyle == HomeWidgetStyle.MediaBlurLyrics;

    private void CompleteLyricScroll(CancellationToken cancellationToken)
    {
        if (_lyricScrollCancellation is null
            || _lyricScrollCancellation.Token != cancellationToken)
        {
            return;
        }

        _lyricScrollCancellation.Dispose();
        _lyricScrollCancellation = null;
    }

    private void CancelPendingLyricScroll()
    {
        CancellationTokenSource? cancellation = _lyricScrollCancellation;
        _lyricScrollCancellation = null;
        cancellation?.Cancel();

        if (_pendingLyricScroll?.Status == DispatcherOperationStatus.Pending)
        {
            _pendingLyricScroll.Abort();
        }

        _pendingLyricScroll = null;
        cancellation?.Dispose();
    }

    private static T? FindVisualChild<T>(DependencyObject obj)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(obj); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(obj, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
