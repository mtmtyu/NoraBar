using System.Reflection;
using System.Windows;
using NoraBar.Hud;
using NoraBar.Tests.Hud;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class MainWindowPointerLeaveSuppressionTests
{
    [Fact]
    public void SuppressPointerLeaveCollapse_SuppressesForSpecifiedDuration()
    {
        StaTestRunner.Run(() =>
        {
            var viewModel = new MainViewModel();
            var registry = new HudRegistry();
            var music = new FakeHudModule(BuiltInHudIds.Music);
            registry.Register(music);
            var router = new HudRouter(registry, BuiltInHudIds.Music, [BuiltInHudIds.Music]);
            var window = new MainWindow(viewModel, router, () => Task.CompletedTask);

            window.SuppressPointerLeaveCollapse(TimeSpan.FromMilliseconds(500));

            Assert.True(window.IsPointerLeaveCollapseSuppressed);
        });
    }

    [Fact]
    public void SuppressPointerLeaveCollapse_ExpiresAfterDuration()
    {
        StaTestRunner.Run(async () =>
        {
            var viewModel = new MainViewModel();
            var registry = new HudRegistry();
            var music = new FakeHudModule(BuiltInHudIds.Music);
            registry.Register(music);
            var router = new HudRouter(registry, BuiltInHudIds.Music, [BuiltInHudIds.Music]);
            var window = new MainWindow(viewModel, router, () => Task.CompletedTask);

            window.SuppressPointerLeaveCollapse(TimeSpan.FromMilliseconds(50));
            Assert.True(window.IsPointerLeaveCollapseSuppressed);

            await Task.Delay(100);
            Assert.False(window.IsPointerLeaveCollapseSuppressed);
        });
    }
}
