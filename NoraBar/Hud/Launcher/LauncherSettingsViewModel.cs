using System.IO;
using System.Collections.ObjectModel;
using NoraBar.Services;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherSettingsViewModel : ViewModelBase
{
    private readonly UserSettings _userSettings;
    private readonly Action _saveSettings;
    private readonly ILauncherApplicationCatalog _catalog;
    private readonly ILauncherUsageStore _usageStore;
    private LauncherPageEditorViewModel? _selectedPage;
    private LauncherGroupEditorViewModel? _selectedGroup;
    private LauncherItemEditorViewModel? _selectedItem;
    private bool _smartEnabled;
    private string _openShortcutText;
    private string _searchShortcutText;
    private string? _shortcutError;

    internal LauncherSettingsViewModel(
        UserSettings userSettings,
        Action saveSettings,
        ILauncherApplicationCatalog catalog,
        ILauncherUsageStore usageStore)
    {
        ArgumentNullException.ThrowIfNull(userSettings);
        ArgumentNullException.ThrowIfNull(saveSettings);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(usageStore);
        _userSettings = userSettings;
        _saveSettings = saveSettings;
        _catalog = catalog;
        _usageStore = usageStore;
        LauncherHudSettings settings = LauncherHudSettingsJson.Read(userSettings);
        Pages = new ObservableCollection<LauncherPageEditorViewModel>(
            settings.Pages.Select(page => LauncherPageEditorViewModel.FromModel(page, Persist)));
        Rules = new ObservableCollection<LauncherRule>(settings.Rules);
        _smartEnabled = settings.SmartEnabled;
        _openShortcutText = FormatShortcut(settings.OpenShortcut);
        _searchShortcutText = FormatShortcut(settings.SearchShortcut);
        _selectedPage = Pages.FirstOrDefault();
        _selectedGroup = _selectedPage?.Groups.FirstOrDefault();
    }

    public LauncherLocalization Strings => new(_userSettings.Language);
    public ObservableCollection<LauncherPageEditorViewModel> Pages { get; }
    public ObservableCollection<LauncherRule> Rules { get; }
    public IReadOnlyList<LauncherItemKind> ItemKinds { get; } = Enum.GetValues<LauncherItemKind>();
    public IReadOnlyList<LauncherExistingInstanceBehavior> ExistingInstanceBehaviors { get; } = Enum.GetValues<LauncherExistingInstanceBehavior>();
    public LauncherPageEditorViewModel? SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }
    public LauncherGroupEditorViewModel? SelectedGroup { get => _selectedGroup; set => SetProperty(ref _selectedGroup, value); }
    public LauncherItemEditorViewModel? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }

    public bool SmartEnabled
    {
        get => _smartEnabled;
        set { if (SetProperty(ref _smartEnabled, value)) Persist(); }
    }

    public string OpenShortcutText { get => _openShortcutText; set => SetProperty(ref _openShortcutText, value); }
    public string SearchShortcutText { get => _searchShortcutText; set => SetProperty(ref _searchShortcutText, value); }
    public string? ShortcutError { get => _shortcutError; private set => SetProperty(ref _shortcutError, value); }

    internal IReadOnlyList<LauncherRule> RulesSnapshot => Rules.ToArray();
    internal event EventHandler? ConfigurationChanged;
    internal event EventHandler<LauncherShortcutsChangedEventArgs>? ShortcutsChanged;
    internal event EventHandler<LauncherSettingsSelectionEventArgs>? EditRequested;

    internal LauncherPageEditorViewModel AddPage(string displayName)
    {
        var page = new LauncherPageEditorViewModel(CreateId("page"), NormalizeName(displayName, "Page"), Persist);
        page.Groups.Add(new LauncherGroupEditorViewModel(CreateId("group"), "Applications", Persist));
        Pages.Add(page);
        SelectedPage = page;
        SelectedGroup = page.Groups[0];
        Persist();
        return page;
    }

    internal void RemovePage(LauncherPageEditorViewModel page)
    {
        if (Pages.Count <= 1) return;
        int index = Pages.IndexOf(page);
        if (index < 0) return;
        Pages.RemoveAt(index);
        Rules.RemoveWhere(rule => string.Equals(rule.TargetPageId, page.Id, StringComparison.Ordinal));
        SelectedPage = Pages[Math.Min(index, Pages.Count - 1)];
        SelectedGroup = SelectedPage.Groups.FirstOrDefault();
        Persist();
    }

    internal void MovePage(LauncherPageEditorViewModel page, int offset) { Move(Pages, page, offset); Persist(); }

    internal LauncherGroupEditorViewModel AddGroup(LauncherPageEditorViewModel page, string displayName)
    {
        var group = new LauncherGroupEditorViewModel(CreateId("group"), NormalizeName(displayName, "Group"), Persist);
        page.Groups.Add(group);
        SelectedPage = page;
        SelectedGroup = group;
        Persist();
        return group;
    }

    internal void RemoveGroup(LauncherPageEditorViewModel page, LauncherGroupEditorViewModel group)
    {
        if (page.Groups.Count <= 1) return;
        page.Groups.Remove(group);
        SelectedGroup = page.Groups.FirstOrDefault();
        Persist();
    }

    internal void MoveGroup(LauncherPageEditorViewModel page, LauncherGroupEditorViewModel group, int offset) { Move(page.Groups, group, offset); Persist(); }

    internal LauncherItemEditorViewModel AddItem(LauncherGroupEditorViewModel group, LauncherItem item)
    {
        var editor = LauncherItemEditorViewModel.FromModel(item, Persist);
        group.Items.Add(editor);
        SelectedGroup = group;
        SelectedItem = editor;
        Persist();
        return editor;
    }

    internal void RemoveItem(LauncherGroupEditorViewModel group, LauncherItemEditorViewModel item)
    {
        group.Items.Remove(item);
        SelectedItem = null;
        Persist();
    }

    internal void MoveItem(LauncherGroupEditorViewModel group, LauncherItemEditorViewModel item, int offset) { Move(group.Items, item, offset); Persist(); }

    internal void AddRule(LauncherRule rule) { Rules.Add(rule); Persist(); }
    internal void ReplaceRule(LauncherRule original, LauncherRule replacement)
    {
        int index = Rules.IndexOf(original);
        if (index >= 0) { Rules[index] = replacement; Persist(); }
    }
    internal void RemoveRule(LauncherRule rule) { if (Rules.Remove(rule)) Persist(); }
    internal void MoveRule(LauncherRule rule, int offset) { Move(Rules, rule, offset); Persist(); }

    internal Task<IReadOnlyList<LauncherItem>> GetInstalledApplicationsAsync(CancellationToken cancellationToken) =>
        _catalog.GetInstalledApplicationsAsync(cancellationToken);

    internal Task<IReadOnlyList<LauncherItem>> GetRunningApplicationsAsync(CancellationToken cancellationToken) =>
        _catalog.GetRunningApplicationsAsync(cancellationToken);

    internal async Task ClearUsageAsync(CancellationToken cancellationToken) =>
        await _usageStore.ClearAsync(cancellationToken);

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Strings));
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ReloadFromSettings()
    {
        LauncherHudSettings settings = LauncherHudSettingsJson.Read(_userSettings);
        Pages.Clear();
        foreach (LauncherPage page in settings.Pages)
        {
            Pages.Add(LauncherPageEditorViewModel.FromModel(page, Persist));
        }
        Rules.Clear();
        foreach (LauncherRule rule in settings.Rules)
        {
            Rules.Add(rule);
        }
        _smartEnabled = settings.SmartEnabled;
        _openShortcutText = FormatShortcut(settings.OpenShortcut);
        _searchShortcutText = FormatShortcut(settings.SearchShortcut);
        SelectedPage = Pages.FirstOrDefault();
        SelectedGroup = SelectedPage?.Groups.FirstOrDefault();
        SelectedItem = null;
        OnPropertyChanged(nameof(SmartEnabled));
        OnPropertyChanged(nameof(OpenShortcutText));
        OnPropertyChanged(nameof(SearchShortcutText));
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
    internal void ReportShortcutError(string? error) => ShortcutError = error;

    internal bool ApplyShortcuts(out LauncherShortcut? open, out LauncherShortcut? search)
    {
        search = null;
        if (!TryParseShortcut(OpenShortcutText, out open, out string? error)
            || !TryParseShortcut(SearchShortcutText, out search, out error))
        {
            ShortcutError = error;
            return false;
        }
        var change = new LauncherShortcutsChangedEventArgs(open, search);
        ShortcutsChanged?.Invoke(this, change);
        if (!change.Accepted)
        {
            ShortcutError = change.Error;
            return false;
        }
        ShortcutError = null;
        Persist(open, search);
        return true;
    }

    internal void SelectItem(string itemId)
    {
        foreach (LauncherPageEditorViewModel page in Pages)
        {
            foreach (LauncherGroupEditorViewModel group in page.Groups)
            {
                LauncherItemEditorViewModel? item = group.Items.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
                if (item is null) continue;
                SelectedPage = page;
                SelectedGroup = group;
                SelectedItem = item;
                EditRequested?.Invoke(this, new LauncherSettingsSelectionEventArgs(itemId));
                return;
            }
        }
    }

    private void Persist() => Persist(ParseShortcutOrNull(OpenShortcutText), ParseShortcutOrNull(SearchShortcutText));

    private void Persist(LauncherShortcut? open, LauncherShortcut? search)
    {
        LauncherHudSettingsJson.Write(_userSettings, new LauncherHudSettings(
            LauncherHudSettings.CurrentPayloadVersion,
            Pages.Select(page => page.ToModel()).ToArray(),
            SmartEnabled,
            Rules.ToArray(),
            open,
            search));
        _saveSettings();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Move<T>(ObservableCollection<T> collection, T item, int offset)
    {
        int oldIndex = collection.IndexOf(item);
        int newIndex = Math.Clamp(oldIndex + offset, 0, collection.Count - 1);
        if (oldIndex >= 0 && oldIndex != newIndex) collection.Move(oldIndex, newIndex);
    }

    private bool TryParseShortcut(string value, out LauncherShortcut? shortcut, out string? error)
    {
        shortcut = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        string[] parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = Strings.ShortcutNeedsModifier;
            return false;
        }
        string[] validModifiers = ["Alt", "Ctrl", "Control", "Shift", "Win", "Windows"];
        if (parts[..^1].Any(part => !validModifiers.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            error = Strings.ShortcutModifierInvalid;
            return false;
        }
        shortcut = new LauncherShortcut(string.Join('+', parts[..^1]), parts[^1]);
        return true;
    }

    private LauncherShortcut? ParseShortcutOrNull(string value) =>
        TryParseShortcut(value, out LauncherShortcut? shortcut, out _) ? shortcut : null;

    private static string FormatShortcut(LauncherShortcut? shortcut) =>
        shortcut is null ? string.Empty : $"{shortcut.Modifiers}+{shortcut.Key}";

    private static string NormalizeName(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string CreateId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}

internal sealed record LauncherSettingsSelectionEventArgs(string ItemId);
internal sealed class LauncherShortcutsChangedEventArgs(LauncherShortcut? open, LauncherShortcut? search) : EventArgs
{
    internal LauncherShortcut? Open { get; } = open;
    internal LauncherShortcut? Search { get; } = search;
    internal bool Accepted { get; set; } = true;
    internal string? Error { get; set; }
}

internal sealed class LauncherPageEditorViewModel : ViewModelBase
{
    private readonly Action _changed;
    private string _displayName;
    private bool _isSelected;
    internal LauncherPageEditorViewModel(string id, string displayName, Action changed)
    {
        Id = id; _displayName = displayName; _changed = changed;
        Groups = new ObservableCollection<LauncherGroupEditorViewModel>();
    }
    public string Id { get; }
    public string DisplayName { get => _displayName; set { if (SetProperty(ref _displayName, value)) _changed(); } }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public ObservableCollection<LauncherGroupEditorViewModel> Groups { get; }
    internal LauncherPage ToModel() => new(Id, DisplayName, Groups.Select(group => group.ToModel()).ToArray());
    internal static LauncherPageEditorViewModel FromModel(LauncherPage page, Action changed)
    {
        var result = new LauncherPageEditorViewModel(page.Id, page.DisplayName, changed);
        foreach (LauncherGroup group in page.Groups) result.Groups.Add(LauncherGroupEditorViewModel.FromModel(group, changed));
        return result;
    }
}

internal sealed class LauncherGroupEditorViewModel : ViewModelBase
{
    private readonly Action _changed;
    private string _displayName;
    internal LauncherGroupEditorViewModel(string id, string displayName, Action changed)
    {
        Id = id; _displayName = displayName; _changed = changed;
        Items = new ObservableCollection<LauncherItemEditorViewModel>();
    }
    public string Id { get; }
    public string DisplayName { get => _displayName; set { if (SetProperty(ref _displayName, value)) _changed(); } }
    public ObservableCollection<LauncherItemEditorViewModel> Items { get; }
    internal LauncherGroup ToModel() => new(Id, DisplayName, Items.Select(item => item.ToModel()).ToArray());
    internal static LauncherGroupEditorViewModel FromModel(LauncherGroup group, Action changed)
    {
        var result = new LauncherGroupEditorViewModel(group.Id, group.DisplayName, changed);
        foreach (LauncherItem item in group.Items) result.Items.Add(LauncherItemEditorViewModel.FromModel(item, changed));
        return result;
    }
}

internal sealed class LauncherItemEditorViewModel : ViewModelBase
{
    private readonly Action _changed;
    private string _displayName;
    private LauncherItemKind _kind;
    private string _target;
    private string? _arguments;
    private string? _workingDirectory;
    private bool _runAsAdministrator;
    private LauncherExistingInstanceBehavior _existingInstanceBehavior;
    private string? _browserTarget;
    private string? _customIconPath;
    private LauncherItemEditorViewModel(LauncherItem item, Action changed)
    {
        Id = item.Id; _displayName = item.DisplayName; _kind = item.Kind; _target = item.Target;
        _arguments = item.Arguments; _workingDirectory = item.WorkingDirectory;
        _runAsAdministrator = item.RunAsAdministrator; _existingInstanceBehavior = item.ExistingInstanceBehavior;
        _browserTarget = item.BrowserTarget; _customIconPath = item.CustomIconPath; _changed = changed;
    }
    public string Id { get; }
    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public LauncherItemKind Kind { get => _kind; set => Set(ref _kind, value); }
    public string Target { get => _target; set => Set(ref _target, value); }
    public string? Arguments { get => _arguments; set => Set(ref _arguments, value); }
    public string? WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, value); }
    public bool RunAsAdministrator { get => _runAsAdministrator; set => Set(ref _runAsAdministrator, value); }
    public LauncherExistingInstanceBehavior ExistingInstanceBehavior { get => _existingInstanceBehavior; set => Set(ref _existingInstanceBehavior, value); }
    public string? BrowserTarget { get => _browserTarget; set => Set(ref _browserTarget, value); }
    public string? CustomIconPath { get => _customIconPath; set => Set(ref _customIconPath, value); }
    public bool IsMissing => Kind switch { LauncherItemKind.Url => false, LauncherItemKind.PackagedApplication => false, LauncherItemKind.Folder => !Directory.Exists(Target), _ => !File.Exists(Target) };
    internal LauncherItem ToModel() => new(Id, DisplayName, Kind, Target, Arguments, WorkingDirectory, RunAsAdministrator, ExistingInstanceBehavior, BrowserTarget, CustomIconPath);
    internal static LauncherItemEditorViewModel FromModel(LauncherItem item, Action changed) => new(item, changed);
    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName)) { _changed(); OnPropertyChanged(nameof(IsMissing)); }
    }
}

internal static class ObservableCollectionExtensions
{
    internal static void RemoveWhere<T>(this ObservableCollection<T> collection, Func<T, bool> predicate)
    {
        for (int index = collection.Count - 1; index >= 0; index--) if (predicate(collection[index])) collection.RemoveAt(index);
    }
}
