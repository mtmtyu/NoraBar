using NoraBar.Hud;
using NoraBar.Hud.Launcher;
using NoraBar.Services;
using NoraBar.ViewModels;
using NoraBar.Views.Launcher;
using System.Windows;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherHudViewModelTests
{
    [Fact]
    public void SetPresentationState_TransitionsIsExpandedAndFiresPropertyChanged()
    {
        StaTestRunner.Run(() =>
        {
            var catalog = new FakeCatalog();
            var usageStore = new FakeUsageStore();
            var windowTracker = new FakeWindowTracker();
            var runtime = new LauncherRuntime(new FakeLauncherPlatform());
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                catalog,
                usageStore);
            var iconCache = new LauncherIconCache();

            var viewModel = new LauncherHudViewModel(
                settings,
                runtime,
                catalog,
                windowTracker,
                usageStore,
                iconCache,
                System.Windows.Threading.Dispatcher.CurrentDispatcher);

            viewModel.Initialize();

            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

            // Initially expanded
            viewModel.SetPresentationState(HudPresentationState.Expanded);
            Assert.True(viewModel.IsExpanded);

            changedProperties.Clear();

            // Collapse notification should set IsExpanded to false
            viewModel.NotifyCollapsed();
            Assert.False(viewModel.IsExpanded);
            Assert.Contains(nameof(LauncherHudViewModel.IsExpanded), changedProperties);

            changedProperties.Clear();

            // Expand again should set IsExpanded to true and raise PropertyChanged
            viewModel.SetPresentationState(HudPresentationState.Expanded);
            Assert.True(viewModel.IsExpanded);
            Assert.Contains(nameof(LauncherHudViewModel.IsExpanded), changedProperties);

            viewModel.Dispose();
        });
    }

    [Fact]
    public void InitialSmallSizeChange_DoesNotDemoteExpandedContentToPeek()
    {
        StaTestRunner.Run(() =>
        {
            var catalog = new FakeCatalog();
            var usageStore = new FakeUsageStore();
            var windowTracker = new FakeWindowTracker();
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                catalog,
                usageStore);
            var viewModel = new LauncherHudViewModel(
                settings,
                new LauncherRuntime(new FakeLauncherPlatform()),
                catalog,
                windowTracker,
                usageStore,
                new LauncherIconCache(),
                System.Windows.Threading.Dispatcher.CurrentDispatcher);
            viewModel.Initialize();
            viewModel.SetPresentationState(HudPresentationState.Expanded);
            var view = new LauncherHudView { DataContext = viewModel };
            var host = new Window
            {
                Content = view,
                Width = 760,
                Height = 10,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };
            try
            {
                host.Show();
                host.UpdateLayout();
                Assert.True(viewModel.IsExpanded);
            }
            finally
            {
                host.Content = null;
                host.Close();
                viewModel.Dispose();
            }
        });
    }

    [Fact]
    public void InitializeAndRebuild_DoesNotDuplicateVisibleRows()
    {
        StaTestRunner.Run(() =>
        {
            var catalog = new FakeCatalog();
            var usageStore = new FakeUsageStore();
            var windowTracker = new FakeWindowTracker();
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                catalog,
                usageStore);
            settings.Pages.First().Groups.First().Items.Add(LauncherItemEditorViewModel.FromModel(
                new LauncherItem("app-1", "App 1", LauncherItemKind.Win32Application, "app.exe"),
                () => { }));

            var viewModel = new LauncherHudViewModel(
                settings,
                new LauncherRuntime(new FakeLauncherPlatform()),
                catalog,
                windowTracker,
                usageStore,
                new LauncherIconCache(),
                System.Windows.Threading.Dispatcher.CurrentDispatcher);

            viewModel.Initialize();
            int initialRowCount = viewModel.VisibleRows.Count;
            Assert.Equal(1, initialRowCount);

            var page2 = LauncherPageEditorViewModel.FromModel(new LauncherPage("page-2", "Page 2", []), () => { });
            viewModel.Pages.Add(page2);
            viewModel.CurrentPage = page2;
            viewModel.CurrentPage = viewModel.Pages.First();

            Assert.Equal(initialRowCount, viewModel.VisibleRows.Count);

            viewModel.Dispose();
        });
    }

    [Fact]
    public void CurrentPage_UpdatesIsSelectedOnPages()
    {
        StaTestRunner.Run(() =>
        {
            var catalog = new FakeCatalog();
            var usageStore = new FakeUsageStore();
            var windowTracker = new FakeWindowTracker();
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                catalog,
                usageStore);
            var viewModel = new LauncherHudViewModel(
                settings,
                new LauncherRuntime(new FakeLauncherPlatform()),
                catalog,
                windowTracker,
                usageStore,
                new LauncherIconCache(),
                System.Windows.Threading.Dispatcher.CurrentDispatcher);

            var page2 = LauncherPageEditorViewModel.FromModel(new LauncherPage("page-2", "Page 2", []), () => { });
            viewModel.Pages.Add(page2);

            viewModel.Initialize();

            Assert.True(viewModel.Pages[0].IsSelected);
            Assert.False(viewModel.Pages[1].IsSelected);

            viewModel.CurrentPage = page2;

            Assert.False(viewModel.Pages[0].IsSelected);
            Assert.True(viewModel.Pages[1].IsSelected);

            viewModel.Dispose();
        });
    }

    [Fact]
    public void MoveSearchSelection_NavigatesWithGridOffsets()
    {
        StaTestRunner.Run(() =>
        {
            var catalog = new FakeCatalog();
            var usageStore = new FakeUsageStore();
            var windowTracker = new FakeWindowTracker();
            var settings = new LauncherSettingsViewModel(
                new UserSettings(),
                () => { },
                catalog,
                usageStore);
            for (int i = 1; i <= 10; i++)
            {
                settings.Pages.First().Groups.First().Items.Add(LauncherItemEditorViewModel.FromModel(
                    new LauncherItem($"app-{i}", $"App {i}", LauncherItemKind.Win32Application, $"app{i}.exe"),
                    () => { }));
            }

            var viewModel = new LauncherHudViewModel(
                settings,
                new LauncherRuntime(new FakeLauncherPlatform()),
                catalog,
                windowTracker,
                usageStore,
                new LauncherIconCache(),
                System.Windows.Threading.Dispatcher.CurrentDispatcher);

            viewModel.Initialize();
            viewModel.SearchQuery = "App";

            Assert.Equal(10, viewModel.SearchResults.Count);
            Assert.Equal(0, viewModel.SelectedSearchIndex);

            // Move Right (+1)
            viewModel.MoveSearchSelection(1);
            Assert.Equal(1, viewModel.SelectedSearchIndex);

            // Move Down (+ItemsPerRow = +7) -> 1 + 7 = 8
            viewModel.MoveSearchSelection(LauncherHudViewModel.ItemsPerRow);
            Assert.Equal(8, viewModel.SelectedSearchIndex);

            // Move Up (-ItemsPerRow = -7) -> 8 - 7 = 1
            viewModel.MoveSearchSelection(-LauncherHudViewModel.ItemsPerRow);
            Assert.Equal(1, viewModel.SelectedSearchIndex);

            // Move Left (-1) -> 1 - 1 = 0
            viewModel.MoveSearchSelection(-1);
            Assert.Equal(0, viewModel.SelectedSearchIndex);

            viewModel.Dispose();
        });
    }

    private sealed class FakeLauncherPlatform : ILauncherPlatform
    {
        public ValueTask<IReadOnlyList<LauncherWindow>> GetWindowsAsync(LauncherItem item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<LauncherWindow>>([]);

        public ValueTask LaunchAsync(LauncherLaunchRequest request, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask FocusWindowAsync(LauncherWindow window, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask CloseWindowAsync(LauncherWindow window, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask ForceQuitAsync(int processId, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public bool IsTargetAvailable(string target) => true;
    }

    private sealed class FakeCatalog : ILauncherApplicationCatalog
    {
        public Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);
        public Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LauncherItem>>([]);
        public void Dispose() { }
    }

    private sealed class FakeWindowTracker : ILauncherWindowTracker
    {
        public string? ForegroundApplicationId => null;
        public event EventHandler? InventoryInvalidated { add { } remove { } }
        public DateTimeOffset GetLastActivated(nint handle) => DateTimeOffset.MinValue;
        public void Start() { }
        public void Stop() { }
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
