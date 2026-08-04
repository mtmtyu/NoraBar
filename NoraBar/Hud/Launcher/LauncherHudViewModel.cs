using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Media;
using NoraBar.ViewModels;
using NoraBar.Services;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherHudViewModel : ViewModelBase, ILauncherHudPresentationSource, ILauncherHudStateSink
{
    private const int PeekItemLimit = 7;
    private const int ItemsPerRow = 7;
    private static readonly TimeSpan InventoryDebounce = TimeSpan.FromMilliseconds(150);
    private readonly LauncherSettingsViewModel _settings;
    private readonly LauncherRuntime _runtime;
    private readonly ILauncherApplicationCatalog _catalog;
    private readonly ILauncherWindowTracker _windowTracker;
    private readonly ILauncherUsageStore _usageStore;
    private readonly LauncherIconCache _iconCache;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _inventoryTimer;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private CancellationTokenSource? _activeCancellation;
    private IReadOnlyList<LauncherItem> _installedApplications = [];
    private LauncherPageEditorViewModel? _currentPage;
    private string _searchQuery = string.Empty;
    private bool _isExpanded;
    private bool _isSearchMode;
    private int _selectedSearchIndex;
    private bool _manualPageSelected;
    private string? _foregroundIdentity;
    private string? _foregroundLauncherItemId;
    private DateTimeOffset _foregroundStartedAt;
    private bool _isDisposed;

    internal LauncherHudViewModel(
        LauncherSettingsViewModel settings,
        LauncherRuntime runtime,
        ILauncherApplicationCatalog catalog,
        ILauncherWindowTracker windowTracker,
        ILauncherUsageStore usageStore,
        LauncherIconCache iconCache,
        Dispatcher dispatcher)
    {
        _settings = settings;
        _runtime = runtime;
        _catalog = catalog;
        _windowTracker = windowTracker;
        _usageStore = usageStore;
        _iconCache = iconCache;
        _dispatcher = dispatcher;
        _inventoryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = InventoryDebounce,
            IsEnabled = false
        };
        _inventoryTimer.Tick += InventoryTimer_Tick;
        Pages = settings.Pages;
        Groups = new ObservableCollection<LauncherGroupViewModel>();
        VisibleRows = new ObservableCollection<LauncherItemRowViewModel>();
        PeekItems = new ObservableCollection<LauncherItemViewModel>();
        SearchResults = new ObservableCollection<LauncherItemViewModel>();
    }

    public object ViewDataContext => this;
    public LauncherLocalization Strings => _settings.Strings;
    public ObservableCollection<LauncherPageEditorViewModel> Pages { get; }
    public ObservableCollection<LauncherGroupViewModel> Groups { get; }
    public ObservableCollection<LauncherItemRowViewModel> VisibleRows { get; }
    public ObservableCollection<LauncherItemViewModel> PeekItems { get; }
    public ObservableCollection<LauncherItemViewModel> SearchResults { get; }
    public bool HasSearchResults => SearchResults.Count > 0;

    public LauncherPageEditorViewModel? CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                _manualPageSelected = true;
                RebuildVisibleItems();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                IsSearchMode = !string.IsNullOrEmpty(value);
                RebuildSearchResults();
            }
        }
    }

    public bool IsExpanded { get => _isExpanded; private set => SetProperty(ref _isExpanded, value); }
    public bool IsSearchMode { get => _isSearchMode; private set => SetProperty(ref _isSearchMode, value); }
    public int SelectedSearchIndex
    {
        get => _selectedSearchIndex;
        set
        {
            int next = SearchResults.Count == 0 ? 0 : Math.Clamp(value, 0, SearchResults.Count - 1);
            if (!SetProperty(ref _selectedSearchIndex, next)) return;
            for (int index = 0; index < SearchResults.Count; index++) SearchResults[index].IsSelected = index == next;
        }
    }

    public event EventHandler? PresentationInvalidated;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _settings.ConfigurationChanged += Settings_ConfigurationChanged;
        _windowTracker.InventoryInvalidated += WindowTracker_InventoryInvalidated;
        _currentPage = Pages.FirstOrDefault();
        RebuildVisibleItems();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _activeCancellation?.Dispose();
        _activeCancellation = new CancellationTokenSource();
        _windowTracker.Start();
        _foregroundStartedAt = DateTimeOffset.UtcNow;
        _ = LoadCatalogAsync(_activeCancellation.Token);
        _inventoryTimer.Stop();
        _inventoryTimer.Start();
    }

    public void Stop()
    {
        _activeCancellation?.Cancel();
        _activeCancellation?.Dispose();
        _activeCancellation = null;
        _inventoryTimer.Stop();
        RecordForegroundDuration(DateTimeOffset.UtcNow);
        _windowTracker.Stop();
        SearchQuery = string.Empty;
        _manualPageSelected = false;
    }

    public void SetPresentationState(HudPresentationState state)
    {
        bool wasExpanded = IsExpanded;
        IsExpanded = state is HudPresentationState.Expanded or HudPresentationState.Pinned;
        if (state == HudPresentationState.Collapsed)
        {
            SearchQuery = string.Empty;
            _manualPageSelected = false;
            ApplyRules();
        }
        else if (IsExpanded && !wasExpanded && !_manualPageSelected)
        {
            ApplyRules();
        }
        PresentationInvalidated?.Invoke(this, EventArgs.Empty);
    }

    internal async Task ActivateAsync(LauncherItemViewModel item, bool requestNewInstance)
    {
        if (item.IsMissing) return;
        await _runtime.ActivateAsync(item.Item, requestNewInstance, _activeCancellation?.Token ?? CancellationToken.None);
        _usageStore.RecordLaunch(item.Id, _windowTracker.ForegroundApplicationId, DateTimeOffset.Now);
    }

    internal async Task<IReadOnlyList<LauncherWindow>> GetWindowsAsync(LauncherItemViewModel item) =>
        await _runtime.GetWindowsAsync(item.Item, CancellationToken.None);

    internal async Task FocusWindowAsync(LauncherWindow window) => await _runtime.FocusWindowAsync(window, CancellationToken.None);
    internal async Task CloseAsync(LauncherWindow window) => await _runtime.CloseAsync(window, CancellationToken.None);
    internal async Task ForceQuitAsync(LauncherWindow window) => await _runtime.ForceQuitAsync(window.ProcessId, CancellationToken.None);
    internal void EditInSettings(LauncherItemViewModel item) => _settings.SelectItem(item.Id);

    internal void NotifyCollapsed()
    {
        _manualPageSelected = false;
        SearchQuery = string.Empty;
        ApplyRules();
    }

    internal void MoveSearchSelection(int offset)
    {
        if (SearchResults.Count == 0) return;
        int next = (SelectedSearchIndex + offset) % SearchResults.Count;
        if (next < 0) next += SearchResults.Count;
        SelectedSearchIndex = next;
    }

    internal Task ActivateSelectedSearchResultAsync() => SearchResults.Count == 0
        ? Task.CompletedTask
        : ActivateAsync(SearchResults[SelectedSearchIndex], requestNewInstance: false);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _settings.ConfigurationChanged -= Settings_ConfigurationChanged;
        _windowTracker.InventoryInvalidated -= WindowTracker_InventoryInvalidated;
        _disposeCancellation.Cancel();
        _inventoryTimer.Tick -= InventoryTimer_Tick;
        Stop();
        _windowTracker.Dispose();
        _catalog.Dispose();
        _usageStore.Dispose();
        _disposeCancellation.Dispose();
    }

    private async Task LoadCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<LauncherItem> catalog = await _catalog.GetInstalledApplicationsAsync(cancellationToken);
            await _dispatcher.InvokeAsync(() =>
            {
                _installedApplications = catalog;
                RebuildSearchResults();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Trace.TraceError(exception.ToString()); }
    }

    private void RebuildVisibleItems()
    {
        Groups.Clear();
        PeekItems.Clear();
        LauncherPageEditorViewModel? page = CurrentPage ?? Pages.FirstOrDefault();
        if (page is null) return;
        var visibleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LauncherGroupEditorViewModel group in page.Groups)
        {
            LauncherItemViewModel[] items = group.Items.Select(editor => CreateItemViewModel(editor.ToModel())).ToArray();
            Groups.Add(new LauncherGroupViewModel(group.Id, group.DisplayName, items));
            foreach (LauncherItemViewModel item in items.Take(Math.Max(0, PeekItemLimit - PeekItems.Count)))
            {
                PeekItems.Add(item);
                visibleIds.Add(item.Id);
            }
        }

        if (_settings.SmartEnabled && PeekItems.Count < PeekItemLimit)
        {
            Dictionary<string, LauncherItem> candidates = page.Groups.SelectMany(group => group.Items)
                .Select(editor => editor.ToModel()).Concat(_installedApplications)
                .GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (LauncherSmartSuggestion suggestion in LauncherSmartScorer.Rank(
                _usageStore.Snapshot, visibleIds, _windowTracker.ForegroundApplicationId, DateTimeOffset.Now, PeekItemLimit - PeekItems.Count))
            {
                if (candidates.TryGetValue(suggestion.ApplicationId, out LauncherItem? item)) PeekItems.Add(CreateItemViewModel(item));
            }
        }
        foreach (LauncherGroupViewModel group in Groups)
        {
            LauncherItemViewModel[][] rows = group.Items.Chunk(ItemsPerRow).Select(row => row.ToArray()).ToArray();
            for (int index = 0; index < rows.Length; index++)
            {
                VisibleRows.Add(new LauncherItemRowViewModel(group.Id, group.DisplayName, index == 0, rows[index]));
            }
        }

        PresentationInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private LauncherItemViewModel CreateItemViewModel(LauncherItem item)
    {
        bool missing = item.Kind switch
        {
            LauncherItemKind.Url => !Uri.TryCreate(item.Target, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps,
            LauncherItemKind.PackagedApplication => false,
            LauncherItemKind.Folder => !Directory.Exists(item.Target),
            _ => !File.Exists(item.Target)
        };
        return new LauncherItemViewModel(item, missing, Strings);
    }

    internal async Task LoadIconAsync(LauncherItemViewModel item)
    {
        if (item.Icon is not null || _isDisposed) return;
        try
        {
            ImageSource? icon = await _iconCache.GetAsync(item.Item, _disposeCancellation.Token);
            _disposeCancellation.Token.ThrowIfCancellationRequested();
            item.Icon = icon;
        }
        catch (OperationCanceledException) { }
    }
    private void RebuildSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            OnPropertyChanged(nameof(HasSearchResults));
            return;
        }
        IEnumerable<LauncherItem> registered = Pages.SelectMany(page => page.Groups)
            .SelectMany(group => group.Items).Select(item => item.ToModel());
        foreach (LauncherSearchResult result in LauncherSearchEngine.Search(SearchQuery, registered, _installedApplications))
        {
            SearchResults.Add(CreateItemViewModel(result.Item));
        }
        SelectedSearchIndex = 0;
        OnPropertyChanged(nameof(HasSearchResults));

    }

    private void ApplyRules()
    {
        if (_manualPageSelected) return;
        string? pageId = LauncherRuleEvaluator.Evaluate(_settings.Rules, DateTimeOffset.Now, _windowTracker.ForegroundApplicationId);
        LauncherPageEditorViewModel? target = Pages.FirstOrDefault(page => string.Equals(page.Id, pageId, StringComparison.Ordinal));
        if (target is not null && !ReferenceEquals(_currentPage, target))
        {
            _currentPage = target;
            OnPropertyChanged(nameof(CurrentPage));
            RebuildVisibleItems();
        }
    }

    private async Task RefreshRunningStateAsync()
    {
        _inventoryTimer.Stop();
        LauncherItemViewModel[] items = Groups.SelectMany(group => group.Items).Concat(PeekItems).Concat(SearchResults)
            .DistinctBy(item => item.Id).ToArray();
        foreach (LauncherItemViewModel item in items.Where(item => item.Item.Kind is LauncherItemKind.Win32Application or LauncherItemKind.PackagedApplication))
        {
            try
            {
                IReadOnlyList<LauncherWindow> windows = await _runtime
                    .GetWindowsAsync(item.Item, _activeCancellation?.Token ?? CancellationToken.None);
                item.IsRunning = windows.Count > 0;
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private void Settings_ConfigurationChanged(object? sender, EventArgs e)
    {
        if (_currentPage is null || !Pages.Contains(_currentPage)) _currentPage = Pages.FirstOrDefault();
        OnPropertyChanged(nameof(Strings));
        RebuildVisibleItems();
    }

    private async void InventoryTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await RefreshRunningStateAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private void WindowTracker_InventoryInvalidated(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(() =>
        {
            if (_isDisposed) return;
            RecordForegroundTransition(DateTimeOffset.UtcNow);
            _inventoryTimer.Stop();
            _inventoryTimer.Start();
        });

    private void RecordForegroundTransition(DateTimeOffset timestamp)
    {
        string? identity = _windowTracker.ForegroundApplicationId;
        if (string.Equals(identity, _foregroundIdentity, StringComparison.OrdinalIgnoreCase)) return;
        RecordForegroundDuration(timestamp);
        _foregroundIdentity = identity;
        _foregroundLauncherItemId = ResolveLauncherItemId(identity);
        _foregroundStartedAt = timestamp;
    }

    private void RecordForegroundDuration(DateTimeOffset timestamp)
    {
        if (_foregroundLauncherItemId is not null && timestamp > _foregroundStartedAt)
        {
            _usageStore.RecordForegroundDuration(_foregroundLauncherItemId, timestamp - _foregroundStartedAt);
        }
        _foregroundIdentity = null;
        _foregroundLauncherItemId = null;
        _foregroundStartedAt = timestamp;
    }

    private string? ResolveLauncherItemId(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return null;
        return Pages.SelectMany(page => page.Groups)
            .SelectMany(group => group.Items)
            .Select(item => item.ToModel())
            .Concat(_installedApplications)
            .FirstOrDefault(item => string.Equals(item.Target, identity, StringComparison.OrdinalIgnoreCase))?.Id;
    }
}
