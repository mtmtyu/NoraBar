using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NoraBar.Hud;
using NoraBar.Hud.Home;
using NoraBar.Hud.Home.Widgets;
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
    public void MediaArtworkDefaultAndMediumStyles_UseSameTemplate()
    {
        StaTestRunner.Run(() =>
        {
            var view = new MediaControlsWidgetView();

            view.SetStyle(HomeWidgetStyle.MediaArtworkHover);
            DataTemplate? defaultTemplate = view.MediaContentControl.ContentTemplate;
            view.SetStyle(HomeWidgetStyle.MediaArtworkHoverMedium);

            Assert.Same(defaultTemplate, view.MediaContentControl.ContentTemplate);
            view.Dispose();
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
    public void WidgetCatalogPreview_UnloadedDisposesViewAndClearsContent()
    {
        StaTestRunner.Run(() =>
        {
            var item = new HomeWidgetCustomizerItemViewModel(
                "catalog",
                HomeWidgetType.MediaControls,
                HomeWidgetStyle.MediaCompact,
                NoraBar.Models.AppLanguage.English);
            var catalogView = new WidgetCatalogItemView { DataContext = item };
            catalogView.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            MediaControlsWidgetView preview = Assert.IsType<MediaControlsWidgetView>(
                catalogView.PreviewContentHost.Content);

            catalogView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.True(preview.IsDisposed);
            Assert.Null(catalogView.PreviewContentHost.Content);
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

    [Fact]
    public void RebuildWidgets_ReplacesNestedWidgetAndRemovesItsSubscription()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeWidgetSource(
                [new HomeWidgetConfig("media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)]);
            var view = new DynamicWidgetHomeView { DataContext = source };
            FrameworkElement oldWrapper = Assert.IsAssignableFrom<FrameworkElement>(
                Assert.Single(view.WidgetsContainer.Children));
            MediaControlsWidgetView oldWidget = Assert.IsType<MediaControlsWidgetView>(
                FindDescendant<MediaControlsWidgetView>(oldWrapper));
            oldWidget.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal(1, source.SubscriptionCountFor(oldWidget));

            source.ActiveWidgets =
                [new HomeWidgetConfig("clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal)];
            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);
            FrameworkElement newWrapper = Assert.IsAssignableFrom<FrameworkElement>(
                Assert.Single(view.WidgetsContainer.Children));

            Assert.NotSame(oldWrapper, newWrapper);
            Assert.Null(FindDescendant<MediaControlsWidgetView>(newWrapper));
            Assert.True(oldWidget.IsDisposed);
            Assert.Equal(0, source.SubscriptionCountFor(oldWidget));
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_LyricScrollStopsAtMaximumAttempts()
    {
        StaTestRunner.Run(() =>
        {
            int attempts = 0;
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(
                () => true,
                _ =>
                {
                    attempts++;
                    return false;
                })
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            view.SetStyle(HomeWidgetStyle.MediaBlurLyrics);
            DrainDispatcher(MediaControlsWidgetView.MaxLyricContainerAttempts);

            Assert.Equal(MediaControlsWidgetView.MaxLyricContainerAttempts, attempts);
            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void DisposeChildViews_ReleasesManagedOnlyChild()
    {
        StaTestRunner.Run(() =>
        {
            var container = new Grid();
            var child = new ManagedOnlyWidget();
            container.Children.Add(child);

            DynamicWidgetHomeView.DisposeChildViews(container);

            Assert.Equal(1, child.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeChildViews_WhenDisposeSucceeds_DoesNotDuplicateManagedRelease()
    {
        StaTestRunner.Run(() =>
        {
            var container = new Grid();
            var child = new ManagedDisposableWidget();
            container.Children.Add(child);

            DynamicWidgetHomeView.DisposeChildViews(container);

            Assert.Equal(1, child.DisposeCount);
            Assert.Equal(0, child.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeChildViews_WhenDisposeFails_ReleasesManagedResourcesAndPreservesFailure()
    {
        StaTestRunner.Run(() =>
        {
            var disposeFailure = new InvalidOperationException("dispose");
            var container = new Grid();
            var child = new ManagedDisposableWidget
            {
                DisposeException = disposeFailure
            };
            container.Children.Add(child);

            AggregateException exception = Assert.Throws<AggregateException>(
                () => DynamicWidgetHomeView.DisposeChildViews(container));

            Assert.Contains(disposeFailure, exception.Flatten().InnerExceptions);
            Assert.Equal(1, child.DisposeCount);
            Assert.Equal(1, child.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeChildViews_WhenDisposeAndManagedReleaseFail_PreservesBothFailures()
    {
        StaTestRunner.Run(() =>
        {
            var disposeFailure = new InvalidOperationException("dispose");
            var releaseFailure = new InvalidOperationException("release");
            var container = new Grid();
            var child = new ManagedDisposableWidget
            {
                DisposeException = disposeFailure,
                ManagedReleaseException = releaseFailure
            };
            container.Children.Add(child);

            AggregateException exception = Assert.Throws<AggregateException>(
                () => DynamicWidgetHomeView.DisposeChildViews(container));

            Assert.Equal(
                new Exception[] { disposeFailure, releaseFailure },
                exception.Flatten().InnerExceptions);
            Assert.Equal(1, child.DisposeCount);
            Assert.Equal(1, child.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeChildViews_ReleasesNestedManagedChild()
    {
        StaTestRunner.Run(() =>
        {
            var container = new Grid();
            var wrapper = new Grid();
            var child = new ManagedOnlyWidget();
            wrapper.Children.Add(child);
            container.Children.Add(wrapper);

            DynamicWidgetHomeView.DisposeChildViews(container);

            Assert.Equal(1, child.ManagedReleaseCount);
        });
    }

    [Fact]
    public void DisposeChildViews_WhenOneChildFails_AttemptsAllChildren()
    {
        StaTestRunner.Run(() =>
        {
            var disposeFailure = new InvalidOperationException("dispose");
            var container = new Grid();
            var failingChild = new ManagedDisposableWidget
            {
                DisposeException = disposeFailure
            };
            var succeedingChild = new DisposableWidget();
            container.Children.Add(failingChild);
            container.Children.Add(succeedingChild);

            AggregateException exception = Assert.Throws<AggregateException>(
                () => DynamicWidgetHomeView.DisposeChildViews(container));

            Assert.Contains(disposeFailure, exception.Flatten().InnerExceptions);
            Assert.Equal(1, failingChild.DisposeCount);
            Assert.Equal(1, failingChild.ManagedReleaseCount);
            Assert.Equal(1, succeedingChild.DisposeCount);
        });
    }

    [Fact]
    public void MediaControlsWidgetView_LyricScrollSucceedsWhenContainerAppearsLater()
    {
        StaTestRunner.Run(() =>
        {
            int attempts = 0;
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(
                () => true,
                _ => ++attempts == 3)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            view.SetStyle(HomeWidgetStyle.MediaBlurLyrics);
            DrainDispatcher(3);

            Assert.Equal(3, attempts);
            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_NewerLyricRequestCancelsOlderRequest()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(() => true, _ => true)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.SetStyle(HomeWidgetStyle.MediaBlurLyrics);
            CancellationToken oldToken = Assert.IsType<CancellationToken>(
                view.PendingLyricScrollToken);

            source.RaisePropertyChanged(nameof(IMusicChangeSource.CurrentLyricIndex));
            CancellationToken newToken = Assert.IsType<CancellationToken>(
                view.PendingLyricScrollToken);

            Assert.True(oldToken.IsCancellationRequested);
            Assert.NotEqual(oldToken, newToken);
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);
            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void MediaControlsWidgetView_LyricScrollExceptionClearsPendingState()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new MediaControlsWidgetView(
                () => true,
                _ => throw new InvalidOperationException("scroll"))
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            view.SetStyle(HomeWidgetStyle.MediaBlurLyrics);
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);

            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void ScheduledRebuild_WhenWidgetDisposalFails_DisposesAllClearsTreeAndReportsFailure()
    {
        StaTestRunner.Run(() =>
        {
            var failures = new List<Exception>();
            var source = new FakeHomeWidgetSource([]);
            var view = new DynamicWidgetHomeView(failures.Add)
            {
                DataContext = source
            };
            var throwingWidget = new DisposableWidget
            {
                DisposeException = new InvalidOperationException("widget")
            };
            var normalWidget = new DisposableWidget();
            view.WidgetsContainer.Children.Add(throwingWidget);
            view.WidgetsContainer.Children.Add(normalWidget);

            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);

            Assert.Equal(1, throwingWidget.DisposeCount);
            Assert.Equal(1, normalWidget.DisposeCount);
            Assert.Empty(view.WidgetsContainer.Children);
            Assert.False(view.HasPendingRebuild);
            Assert.Single(failures);
            view.Dispose();
        });
    }

    [Fact]
    public void ScheduledRebuild_WhenManagedWidgetDisposalFails_ReleasesManagedSubscription()
    {
        StaTestRunner.Run(() =>
        {
            var failures = new List<Exception>();
            var source = new FakeHomeWidgetSource([]);
            var subscriptionSource = new FakeLongLivedSource();
            var view = new DynamicWidgetHomeView(failures.Add)
            {
                DataContext = source
            };
            var throwingWidget = new ThrowingManagedWidget(subscriptionSource);
            var normalWidget = new DisposableWidget();
            view.WidgetsContainer.Children.Add(throwingWidget);
            view.WidgetsContainer.Children.Add(normalWidget);
            List<IHomeHudManagedResource> managedResources =
                GetManagedChildResources(view);
            managedResources.Add(throwingWidget);

            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);

            Assert.Equal(1, throwingWidget.DisposeCount);
            Assert.Equal(1, throwingWidget.ManagedReleaseCount);
            Assert.Equal(1, normalWidget.DisposeCount);
            Assert.Equal(0, subscriptionSource.SubscriptionCount);
            Assert.Empty(view.WidgetsContainer.Children);
            Assert.Empty(managedResources);
            Assert.Single(failures);
            view.Dispose();
        });
    }

    [Fact]
    public void HomePreviewSession_HideAndShowAgain_RecreatesDisposedPreview()
    {
        StaTestRunner.Run(() =>
        {
            var firstView = new DisposableWidget
            {
                DisposeException = new InvalidOperationException("view")
            };
            var firstOwner = new TrackingDisposable();
            var firstPreview = new HomeHudPreview(
                firstView,
                new HudSize(100, 100),
                firstOwner);
            var secondOwner = new TrackingDisposable();
            var secondPreview = new HomeHudPreview(
                new DisposableWidget(),
                new HudSize(100, 100),
                secondOwner);
            var session = new HomePreviewSession();
            object? content = null;
            var failures = new List<Exception>();

            session.Show(
                () => firstPreview,
                preview => content = preview.View,
                () => content = null,
                failures.Add);
            session.Suspend(
                () => content = null,
                failures.Add);
            session.Show(
                () => secondPreview,
                preview => content = preview.View,
                () => content = null,
                failures.Add);

            Assert.Equal(1, firstView.DisposeCount);
            Assert.Equal(1, firstOwner.DisposeCount);
            Assert.Same(secondPreview, session.Current);
            Assert.Same(secondPreview.View, content);
            Assert.Single(failures);

            session.Suspend(() => content = null, failures.Add);
            Assert.Null(session.Current);
            Assert.Null(content);
            Assert.Equal(1, secondOwner.DisposeCount);
        });
    }

    [Fact]
    public void HomePreviewSession_WhenHostingFails_ClearsAndDisposesPreview()
    {
        StaTestRunner.Run(() =>
        {
            var owner = new TrackingDisposable();
            var preview = new HomeHudPreview(
                new DisposableWidget(),
                new HudSize(100, 100),
                owner);
            var session = new HomePreviewSession();
            var hostingFailure = new InvalidOperationException("host");
            object? content = null;

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                session.Show(
                    () => preview,
                    hostedPreview =>
                    {
                        content = hostedPreview.View;
                        throw hostingFailure;
                    },
                    () => content = null,
                    _ => { }));

            Assert.Same(hostingFailure, exception);
            Assert.Null(session.Current);
            Assert.Null(content);
            Assert.Equal(1, Assert.IsType<DisposableWidget>(preview.View).DisposeCount);
            Assert.Equal(1, owner.DisposeCount);
        });
    }

    [Fact]
    public void HomePreviewSession_WhenHostingAndCleanupFail_PreservesHostingFailure()
    {
        StaTestRunner.Run(() =>
        {
            var cleanupFailure = new InvalidOperationException("cleanup");
            var view = new DisposableWidget { DisposeException = cleanupFailure };
            var owner = new TrackingDisposable();
            var preview = new HomeHudPreview(
                view,
                new HudSize(100, 100),
                owner);
            var session = new HomePreviewSession();
            var hostingFailure = new InvalidOperationException("host");
            object? content = null;
            var reportedFailures = new List<Exception>();

            Exception exception = Assert.Throws<InvalidOperationException>(() =>
                session.Show(
                    () => preview,
                    hostedPreview =>
                    {
                        content = hostedPreview.View;
                        throw hostingFailure;
                    },
                    () => content = null,
                    reportedFailures.Add));

            Assert.Same(hostingFailure, exception);
            Assert.Null(session.Current);
            Assert.Null(content);
            Assert.Equal(1, view.DisposeCount);
            Assert.Equal(1, owner.DisposeCount);
            AggregateException reported = Assert.IsType<AggregateException>(
                Assert.Single(reportedFailures));
            AggregateException previewFailure = Assert.IsType<AggregateException>(
                Assert.Single(reported.InnerExceptions));
            Assert.Same(cleanupFailure, Assert.Single(previewFailure.InnerExceptions));
        });
    }

    [Fact]
    public void ReleaseManagedResources_RemovesHomeAndMediaSubscriptions()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeWidgetSource(
                [new HomeWidgetConfig("media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)]);
            var view = new DynamicWidgetHomeView { DataContext = source };
            MediaControlsWidgetView mediaWidget = Assert.IsType<MediaControlsWidgetView>(
                FindDescendant<MediaControlsWidgetView>(
                    Assert.IsAssignableFrom<FrameworkElement>(
                        Assert.Single(view.WidgetsContainer.Children))));
            mediaWidget.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal(2, source.SubscriptionCount);

            view.ReleaseManagedResources();

            Assert.Equal(0, source.SubscriptionCount);
            view.Dispose();
        });
    }

    [Fact]
    public void RebuildWidgets_RepeatedRequestsCreateOnlyOneNewTree()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeWidgetSource(
                [new HomeWidgetConfig("media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)]);
            var view = new DynamicWidgetHomeView { DataContext = source };
            int initialRebuildCount = view.RebuildCount;

            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.MaxWidgetWidth));
            source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.MaxWidgetHeight));
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);

            Assert.Equal(initialRebuildCount + 1, view.RebuildCount);
            Assert.Single(view.WidgetsContainer.Children);
            view.Dispose();
        });
    }

    [Fact]
    public void RebuildWidgets_RepeatedRebuildsDoNotAccumulateWidgetSubscriptions()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeWidgetSource(
                [new HomeWidgetConfig("media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)]);
            var view = new DynamicWidgetHomeView { DataContext = source };
            MediaControlsWidgetView currentWidget = Assert.IsType<MediaControlsWidgetView>(
                FindDescendant<MediaControlsWidgetView>(
                    Assert.IsAssignableFrom<FrameworkElement>(
                        Assert.Single(view.WidgetsContainer.Children))));
            currentWidget.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            for (int rebuild = 0; rebuild < 100; rebuild++)
            {
                MediaControlsWidgetView oldWidget = currentWidget;
                source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
                Dispatcher.CurrentDispatcher.Invoke(
                    () => { },
                    DispatcherPriority.ContextIdle);
                currentWidget = Assert.IsType<MediaControlsWidgetView>(
                    FindDescendant<MediaControlsWidgetView>(
                        Assert.IsAssignableFrom<FrameworkElement>(
                            Assert.Single(view.WidgetsContainer.Children))));
                currentWidget.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                Assert.True(oldWidget.IsDisposed);
                Assert.Equal(0, source.SubscriptionCountFor(oldWidget));
                Assert.Equal(1, source.SubscriptionCountFor(currentWidget));
            }

            view.Dispose();
            Assert.Equal(0, source.SubscriptionCountFor(currentWidget));
            Assert.Equal(0, source.SubscriptionCount);
        });
    }

    [Fact]
    public void RebuildWidgets_RemovedMediaWidgetCanBeCollected()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeHomeWidgetSource(
                [new HomeWidgetConfig("media", HomeWidgetType.MediaControls, HomeWidgetStyle.MediaCompact)]);
            var view = new DynamicWidgetHomeView { DataContext = source };

            WeakReference oldWidget = RebuildAndReleaseOldMediaWidget(view, source);
            for (int attempt = 0; attempt < 3 && oldWidget.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(oldWidget.IsAlive);
            view.Dispose();
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RebuildAndReleaseOldMediaWidget(
        DynamicWidgetHomeView view,
        FakeHomeWidgetSource source)
    {
        FrameworkElement wrapper = Assert.IsAssignableFrom<FrameworkElement>(
            Assert.Single(view.WidgetsContainer.Children));
        MediaControlsWidgetView widget = Assert.IsType<MediaControlsWidgetView>(
            FindDescendant<MediaControlsWidgetView>(wrapper));
        widget.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var weakReference = new WeakReference(widget);

        source.ActiveWidgets =
            [new HomeWidgetConfig("clock", HomeWidgetType.DigitalClock, HomeWidgetStyle.ClockMinimal)];
        source.RaisePropertyChanged(nameof(IHomeWidgetPresentationSource.ActiveWidgets));
        Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            DispatcherPriority.ContextIdle);
        return weakReference;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void DrainDispatcher(int passes)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ContextIdle);
        }
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

    private static List<IHomeHudManagedResource> GetManagedChildResources(
        DynamicWidgetHomeView view)
    {
        FieldInfo field = Assert.IsAssignableFrom<FieldInfo>(
            typeof(DynamicWidgetHomeView).GetField(
                "_managedChildResources",
                BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<List<IHomeHudManagedResource>>(field.GetValue(view));
    }

    private sealed class FakeHomeWidgetSource : IHomeWidgetPresentationSource, IMusicChangeSource
    {
        private readonly HashSet<PropertyChangedEventHandler> _handlers = [];

        internal FakeHomeWidgetSource(IReadOnlyList<HomeWidgetConfig> activeWidgets)
        {
            ActiveWidgets = activeWidgets;
        }

        public IReadOnlyList<HomeWidgetConfig> ActiveWidgets { get; set; }

        public IReadOnlyList<HomeWorldClockItemViewModel> WorldClockItems { get; set; } = Array.Empty<HomeWorldClockItemViewModel>();

        public double MaxWidgetWidth { get; set; } = 800;

        public double MaxWidgetHeight { get; set; } = 300;

        public bool IsWidgetEditMode { get; set; }

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

        public void UpdateActiveWidgets(IReadOnlyList<HomeWidgetConfig> widgets) =>
            ActiveWidgets = widgets;

        internal int SubscriptionCountFor(object target) =>
            _handlers.Count(handler => ReferenceEquals(handler.Target, target));

        internal int SubscriptionCount => _handlers.Count;

        internal void RaisePropertyChanged(string propertyName)
        {
            var eventArgs = new PropertyChangedEventArgs(propertyName);
            foreach (PropertyChangedEventHandler handler in _handlers.ToArray())
            {
                handler(this, eventArgs);
            }
        }
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

        internal void RaisePropertyChanged(string propertyName)
        {
            var eventArgs = new PropertyChangedEventArgs(propertyName);
            foreach (PropertyChangedEventHandler handler in _handlers.ToArray())
            {
                handler(this, eventArgs);
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

    private sealed class ManagedOnlyWidget : FrameworkElement, IHomeHudManagedResource
    {
        internal int ManagedReleaseCount { get; private set; }

        public void ReleaseManagedResources() => ManagedReleaseCount++;
    }

    private sealed class ManagedDisposableWidget : FrameworkElement, IDisposable,
        IHomeHudManagedResource
    {
        internal int DisposeCount { get; private set; }
        internal int ManagedReleaseCount { get; private set; }
        internal Exception? DisposeException { get; init; }
        internal Exception? ManagedReleaseException { get; init; }

        public void Dispose()
        {
            DisposeCount++;
            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }

        public void ReleaseManagedResources()
        {
            ManagedReleaseCount++;
            if (ManagedReleaseException is not null)
            {
                throw ManagedReleaseException;
            }
        }
    }

    private sealed class FakeLongLivedSource
    {
        internal int SubscriptionCount { get; private set; }

        internal void Subscribe() => SubscriptionCount++;
        internal void Unsubscribe() => SubscriptionCount--;
    }

    private sealed class ThrowingManagedWidget : FrameworkElement, IDisposable, IHomeHudManagedResource
    {
        private readonly FakeLongLivedSource _source;
        private bool _isReleased;

        internal ThrowingManagedWidget(FakeLongLivedSource source)
        {
            _source = source;
            _source.Subscribe();
        }

        internal int DisposeCount { get; private set; }
        internal int ManagedReleaseCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("widget");
        }

        public void ReleaseManagedResources()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            ManagedReleaseCount++;
            _source.Unsubscribe();
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
