using Xunit;
using System.Windows;
using NoraBar.Hud;
using NoraBar.Hud.Launcher;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherHudPreviewTests
{
    [Fact]
    public void Dispose_ClearsDataContextAndDisposesViewOnce()
    {
        StaTestRunner.Run(() =>
        {
            var view = new DisposableView { DataContext = new object() };
            var preview = new LauncherHudPreview(view, new HudSize(100, 100));

            preview.Dispose();
            preview.Dispose();

            Assert.Null(view.DataContext);
            Assert.Equal(1, view.DisposeCount);
        });
    }

    [Fact]
    public void SessionSuspend_ReleasesPreviewBeforeClearingHost()
    {
        StaTestRunner.Run(() =>
        {
            var order = new List<string>();
            var view = new DisposableView(() => order.Add("dispose"));
            var preview = new LauncherHudPreview(view, new HudSize(100, 100));
            var session = new LauncherPreviewSession();
            session.Show(() => preview, _ => { });

            session.Suspend(() => order.Add("clear"));

            Assert.Equal(["dispose", "clear"], order);
        });
    }

    private sealed class DisposableView(Action? onDispose = null) : FrameworkElement, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            onDispose?.Invoke();
        }
    }
}
