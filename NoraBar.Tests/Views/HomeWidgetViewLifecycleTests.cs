using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.ViewModels;
using NoraBar.Views.Home;
using NoraBar.Views.Home.Widgets;
using Xunit;

namespace NoraBar.Tests.Views;

public sealed class HomeWidgetViewLifecycleTests
{
    [Fact]
    public void RebuildWidgets_DisposesRemovedWidgetViews()
    {
        StaTestRunner.Run(() =>
        {
            var widgetsContainer = new Grid();
            var widget = new DisposableWidget();
            widgetsContainer.Children.Add(widget);

            DynamicWidgetHomeView.DisposeChildViews(widgetsContainer);

            Assert.Equal(1, widget.DisposeCount);
        });
    }

    [Fact]
    public void MediaControlsWidgetView_LoadSubscribesOnce()
    {
        StaTestRunner.Run(() =>
        {
            bool isLoaded = false;
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => isLoaded)
            {
                DataContext = source
            };

            Assert.Equal(0, source.SubscriptionCount);
            isLoaded = true;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(1, source.SubscriptionCount);
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_UnloadRemovesSubscription()
    {
        StaTestRunner.Run(() =>
        {
            bool isLoaded = true;
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => isLoaded)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.SetStyle(NoraBar.Hud.Home.Widgets.HomeWidgetStyle.MediaBlurLyrics);
            Assert.True(view.HasPendingLyricScroll);

            isLoaded = false;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(0, source.SubscriptionCount);
            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_DisposeRemovesSubscription()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => true)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            view.Dispose();
            view.Dispose();
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(0, source.SubscriptionCount);
        });
    }

    [Fact]
    public void MediaControlsWidgetView_StyleChangeCancelsPendingScroll()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => true)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.SetStyle(NoraBar.Hud.Home.Widgets.HomeWidgetStyle.MediaBlurLyrics);
            Assert.True(view.HasPendingLyricScroll);

            view.SetStyle(NoraBar.Hud.Home.Widgets.HomeWidgetStyle.MediaCompact);

            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_DisposeCancelsPendingScroll()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => true)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.SetStyle(NoraBar.Hud.Home.Widgets.HomeWidgetStyle.MediaBlurLyrics);
            Assert.True(view.HasPendingLyricScroll);

            view.Dispose();

            Assert.False(view.HasPendingLyricScroll);
        });
    }

    [Fact]
    public void MediaControlsWidgetView_DataContextChangeCancelsPendingScroll()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => true)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.SetStyle(NoraBar.Hud.Home.Widgets.HomeWidgetStyle.MediaBlurLyrics);
            Assert.True(view.HasPendingLyricScroll);

            view.DataContext = null;

            Assert.False(view.HasPendingLyricScroll);
            Assert.Equal(0, source.SubscriptionCount);
            view.Dispose();
        });
    }

    [Fact]
    public void HomeHudPreview_DisposeDisposesView()
    {
        StaTestRunner.Run(() =>
        {
            var view = new DisposableWidget();
            var viewModel = new TrackingDisposable();
            var preview = new HomeHudPreview(
                view,
                new HudSize(100, 100),
                viewModel);

            preview.Dispose();
            preview.Dispose();

            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, viewModel.DisposeCount);
        });
    }

    [Fact]
    public void HomeHudPreview_WhenViewDisposalThrows_StillDisposesViewModel()
    {
        StaTestRunner.Run(() =>
        {
            var view = new DisposableWidget
            {
                DisposeException = new InvalidOperationException("view")
            };
            var viewModel = new TrackingDisposable();
            var preview = new HomeHudPreview(
                view,
                new HudSize(100, 100),
                viewModel);

            AggregateException exception = Assert.Throws<AggregateException>(preview.Dispose);
            preview.Dispose();

            Assert.Single(exception.InnerExceptions);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, viewModel.DisposeCount);
        });
    }

    [Fact]
    public void CustomizerCleanup_UnsubscribesPreviewAndIsIdempotent()
    {
        StaTestRunner.Run(() =>
        {
            var window = new HomeWidgetCustomizerWindow();
            var viewModel = new HomeWidgetCustomizerViewModel([]);
            window.DataContext = viewModel;

            Assert.Contains(
                GetEventHandlers(
                    viewModel,
                    typeof(HomeWidgetCustomizerViewModel),
                    nameof(HomeWidgetCustomizerViewModel.PreviewInvalidated)),
                handler => ReferenceEquals(handler.Target, window));

            window.CleanupPreviewResources();
            window.CleanupPreviewResources();

            Assert.DoesNotContain(
                GetEventHandlers(
                    viewModel,
                    typeof(HomeWidgetCustomizerViewModel),
                    nameof(HomeWidgetCustomizerViewModel.PreviewInvalidated)),
                handler => ReferenceEquals(handler.Target, window));
        });
    }

    [Fact]
    public void SettingsPreviewCleanup_DisposesAndClearsOwnedPreview()
    {
        StaTestRunner.Run(() =>
        {
            var view = new DisposableWidget
            {
                DisposeException = new InvalidOperationException("view")
            };
            var viewModel = new TrackingDisposable();
            var preview = new HomeHudPreview(
                view,
                new HudSize(100, 100),
                viewModel);
            HomeHudPreview? ownedPreview = preview;
            object? content = view;

            Assert.Throws<AggregateException>(() => HomePreviewLifecycle.Cleanup(
                ownedPreview,
                () => ownedPreview = null,
                () => content = null));
            HomePreviewLifecycle.Cleanup(
                ownedPreview,
                () => ownedPreview = null,
                () => content = null);

            Assert.Null(content);
            Assert.Null(ownedPreview);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, viewModel.DisposeCount);
        });
    }

    private static IReadOnlyList<Delegate> GetEventHandlers(
        object publisher,
        Type declaringType,
        string eventName)
    {
        FieldInfo eventField = Assert.IsAssignableFrom<FieldInfo>(
            declaringType.GetField(
                eventName,
                BindingFlags.Instance | BindingFlags.NonPublic));
        var handlers = eventField.GetValue(publisher) as MulticastDelegate;
        return handlers?.GetInvocationList() ?? [];
    }

    private sealed class FakeMusicChangeSource : IMusicChangeSource
    {
        private readonly HashSet<PropertyChangedEventHandler> _handlers = [];

        internal int SubscriptionCount => _handlers.Count;

        public int CurrentLyricIndex { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                if (value is not null)
                {
                    _handlers.Add(value);
                }
            }
            remove
            {
                if (value is not null)
                {
                    _handlers.Remove(value);
                }
            }
        }
    }

    private sealed class DisposableWidget : FrameworkElement, IDisposable
    {
        internal int DisposeCount { get; private set; }
        internal Exception? DisposeException { get; init; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
