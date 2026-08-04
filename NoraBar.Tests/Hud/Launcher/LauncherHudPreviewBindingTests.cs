using System.Windows;
using NoraBar.Hud.Launcher;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherHudPreviewBindingTests
{
    [Fact]
    public void FactoryCreate_BindsSharedLauncherViewToPreviewViewModel()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                new EmptyCatalog(),
                new EmptyUsageStore());

            using LauncherHudPreview preview = LauncherHudPreviewFactory.Create(settings);
            var host = new Window
            {
                Content = preview.View,
                Width = 640,
                Height = 420,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };
            try
            {
                host.Show();
                host.UpdateLayout();
                Assert.IsType<LauncherPreviewViewModel>(preview.View.DataContext);
            }
            finally
            {
                host.Close();
            }
        });
    }

    private sealed class EmptyCatalog : ILauncherApplicationCatalog
    {
        public Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);

        public Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);

        public void Dispose() { }
    }

    private sealed class EmptyUsageStore : ILauncherUsageStore
    {
        public IReadOnlyList<LauncherUsageEntry> Snapshot => [];
        public void RecordLaunch(string applicationId, string? foregroundApplicationId, DateTimeOffset timestamp) { }
        public void RecordForegroundDuration(string applicationId, TimeSpan duration) { }
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() { }
    }
}
