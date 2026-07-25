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
            var viewModel = new HomeHudViewModel(new MainViewModel());
            var preview = new HomeHudPreview(
                view,
                new HudSize(100, 100),
                viewModel);

            preview.Dispose();
            preview.Dispose();

            Assert.Equal(1, view.DisposeCount);
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

    private sealed class DisposableWidget : FrameworkElement, IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
