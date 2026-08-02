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
using NoraBar.ViewModels;

namespace NoraBar.Views.Island.DesignC_LyricsFocus
{
    public partial class DesignCMusicView : UserControl, IDisposable
    {
        private const int MaxLyricContainerAttempts = 5;

        private readonly Func<bool> _isLoaded;
        private IMusicChangeSource? _musicSource;
        private DispatcherOperation? _pendingLyricScroll;
        private CancellationTokenSource? _lyricScrollCancellation;
        private bool _isDisposed;

        public DesignCMusicView()
            : this(null)
        {
        }

        internal DesignCMusicView(
            Func<bool>? isLoaded,
            bool initializeComponent = true)
        {
            _isLoaded = isLoaded ?? (() => IsLoaded);
            if (initializeComponent)
            {
                InitializeComponent();
            }

            DataContextChanged += DesignCMusicView_DataContextChanged;
            Loaded += DesignCMusicView_Loaded;
            Unloaded += DesignCMusicView_Unloaded;
        }

        internal bool HasPendingLyricScroll =>
            _pendingLyricScroll is not null
            || _lyricScrollCancellation is not null;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            DataContextChanged -= DesignCMusicView_DataContextChanged;
            Loaded -= DesignCMusicView_Loaded;
            Unloaded -= DesignCMusicView_Unloaded;
            DetachMusicSource();
            CancelPendingLyricScroll();
            _musicSource = null;
            DataContext = null;
        }

        private void UserControl_MouseWheel(
            object sender,
            System.Windows.Input.MouseWheelEventArgs e)
        {
            if (DataContext is MainViewModel mainViewModel
                && mainViewModel.Music.HasMultipleSessions)
            {
                if (e.Delta > 0)
                {
                    mainViewModel.Music.SwitchToPreviousSessionCommand.Execute(null);
                }
                else if (e.Delta < 0)
                {
                    mainViewModel.Music.SwitchToNextSessionCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void DesignCMusicView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            _musicSource = GetMusicSource();
            AttachMusicSource();
            ScheduleLyricScroll();
        }

        private void DesignCMusicView_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachMusicSource();
            CancelPendingLyricScroll();
        }

        private void DesignCMusicView_DataContextChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            DetachMusicSource();
            CancelPendingLyricScroll();
            _musicSource = GetMusicSource();
            if (!_isDisposed && _isLoaded())
            {
                AttachMusicSource();
                ScheduleLyricScroll();
            }
        }

        private IMusicChangeSource? GetMusicSource()
        {
            if (DataContext is MainViewModel mainViewModel)
            {
                return mainViewModel.Music;
            }

            return DataContext as IMusicChangeSource;
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

        private void MusicSource_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IMusicChangeSource.CurrentLyricIndex))
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
                Trace.TraceError($"Lyrics Focus scrolling failed: {exception}");
            }

            CompleteLyricScroll(cancellationToken);
        }

        private bool TryScrollToCurrentLyric(CancellationToken cancellationToken)
        {
            if (!CanScrollLyrics() || cancellationToken.IsCancellationRequested)
            {
                return true;
            }

            int index = _musicSource!.CurrentLyricIndex;
            if (index < 0 || index >= LyricsListBox.Items.Count)
            {
                return true;
            }

            object targetItem = LyricsListBox.Items[index];
            ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(LyricsListBox);
            if (scrollViewer is null)
            {
                LyricsListBox.ScrollIntoView(targetItem);
                return false;
            }

            var container = LyricsListBox.ItemContainerGenerator.ContainerFromIndex(index)
                as FrameworkElement;
            if (container is null)
            {
                LyricsListBox.ScrollIntoView(targetItem);
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
            && _musicSource is not null;

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

        private static T? FindVisualChild<T>(DependencyObject element)
            where T : DependencyObject
        {
            for (int index = 0;
                 index < VisualTreeHelper.GetChildrenCount(element);
                 index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, index);
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
}
