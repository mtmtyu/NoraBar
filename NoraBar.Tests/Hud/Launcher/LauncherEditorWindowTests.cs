using NoraBar.Hud.Launcher;
using NoraBar.Services;
using NoraBar.Views.Launcher;
using System.Windows;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherEditorWindowTests
{
    [Fact]
    public void LauncherEditorWindow_BindsToViewModel_AndSupportsPageGroupItemOperations()
    {
        StaTestRunner.Run(() =>
        {
            var userSettings = new UserSettings();
            bool saveCalled = false;
            var settingsVm = new LauncherSettingsViewModel(
                userSettings,
                () => saveCalled = true,
                new FakeCatalog(),
                new FakeUsageStore());

            var window = new LauncherEditorWindow
            {
                DataContext = settingsVm
            };

            Assert.NotNull(window.DataContext);
            Assert.Same(settingsVm, window.DataContext);

            // Add Page
            var newPage = settingsVm.AddPage("Test Visual Page");
            Assert.Contains(newPage, settingsVm.Pages);
            Assert.True(saveCalled);

            // Add Item to Group
            var group = newPage.Groups[0];
            var newItem = settingsVm.AddItem(group, new LauncherItem("test-item", "Test Item", LauncherItemKind.File, "C:\\test.txt"));
            Assert.Contains(newItem, group.Items);

            window.Close();
        });
    }

    private sealed class FakeCatalog : ILauncherApplicationCatalog
    {
        public Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);
        public Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);
        public void Dispose() { }
    }

    private sealed class FakeUsageStore : ILauncherUsageStore
    {
        public IReadOnlyList<LauncherUsageEntry> Snapshot => [];
        public void RecordLaunch(string applicationId, string? contextApplicationId, DateTimeOffset timestamp) { }
        public void RecordForegroundDuration(string applicationId, TimeSpan duration) { }
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() { }
    }
}
