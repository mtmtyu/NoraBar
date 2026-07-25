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
    public void MediaControlsWidgetView_DisposeDetachesMusicViewModel()
    {
        StaTestRunner.Run(() =>
        {
            var mainViewModel = new MainViewModel();
            var view = new MediaControlsWidgetView
            {
                DataContext = mainViewModel
            };
            IDisposable disposable = Assert.IsAssignableFrom<IDisposable>(view);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Contains(
                GetPropertyChangedHandlers(mainViewModel.Music),
                handler => ReferenceEquals(handler.Target, view));

            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.DoesNotContain(
                GetPropertyChangedHandlers(mainViewModel.Music),
                handler => ReferenceEquals(handler.Target, view));

            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            disposable.Dispose();

            Assert.DoesNotContain(
                GetPropertyChangedHandlers(mainViewModel.Music),
                handler => ReferenceEquals(handler.Target, view));
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
    private static IReadOnlyList<Delegate> GetPropertyChangedHandlers(
        MusicViewModel viewModel)
    {
        FieldInfo eventField = Assert.IsAssignableFrom<FieldInfo>(
            typeof(ViewModelBase).GetField(
                nameof(ViewModelBase.PropertyChanged),
                BindingFlags.Instance | BindingFlags.NonPublic));
        var handlers = eventField.GetValue(viewModel) as MulticastDelegate;
        return handlers?.GetInvocationList() ?? [];
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
