using System.ComponentModel;
using System.Windows;
using NoraBar.ViewModels;
using NoraBar.Views.Island.DesignC_LyricsFocus;
using Xunit;

namespace NoraBar.Tests.Views;

public sealed class DesignCMusicViewLifecycleTests
{
    [Fact]
    public void Unload_RemovesSubscriptionAndCancelsPendingLyricScroll()
    {
        StaTestRunner.Run(() =>
        {
            bool isLoaded = false;
            var source = new FakeMusicChangeSource { CurrentLyricIndex = 0 };
            var view = new DesignCMusicView(
                () => isLoaded,
                initializeComponent: false)
            {
                DataContext = source
            };

            isLoaded = true;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(1, source.SubscriptionCount);
            Assert.True(view.HasPendingLyricScroll);

            isLoaded = false;
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

            Assert.Equal(0, source.SubscriptionCount);
            Assert.False(view.HasPendingLyricScroll);
            view.Dispose();
        });
    }

    [Fact]
    public void Dispose_RemovesSubscriptionAndPreventsReload()
    {
        StaTestRunner.Run(() =>
        {
            var source = new FakeMusicChangeSource();
            var view = new DesignCMusicView(
                () => true,
                initializeComponent: false)
            {
                DataContext = source
            };
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            view.Dispose();
            view.Dispose();
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.Equal(0, source.SubscriptionCount);
            Assert.False(view.HasPendingLyricScroll);
        });
    }

    private sealed class FakeMusicChangeSource : IMusicChangeSource
    {
        private readonly HashSet<PropertyChangedEventHandler> _handlers = [];

        internal int SubscriptionCount => _handlers.Count;

        public int CurrentLyricIndex { get; set; } = -1;

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
}
