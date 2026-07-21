using System.Collections.ObjectModel;
using System.Diagnostics;
using NoraBar.Hud;
using NoraBar.Models;
using NoraBar.Services;

namespace NoraBar.ViewModels;

public sealed class HudNavigationViewModel : ViewModelBase, IDisposable
{
    private readonly HudRouter _router;
    private readonly UserSettings _settings;
    private readonly Action _saveSettings;
    private readonly SemaphoreSlim _configurationGate = new(1, 1);
    private AppLanguage _language;
    private string _defaultHudId;
    private string _selectedSettingsHudId;
    private bool _isDisposed;

    internal HudNavigationViewModel(
        HudRouter router,
        IReadOnlyList<IHudModule> modules,
        UserSettings settings,
        AppLanguage language,
        Action saveSettings)
    {
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(saveSettings);
        _router = router;
        _settings = settings;
        _saveSettings = saveSettings;
        _language = language;
        _defaultHudId = settings.DefaultHudId;

        var modulesById = modules.ToDictionary(module => module.Id, StringComparer.Ordinal);
        var orderedModules = new List<IHudModule>();
        foreach (string id in settings.EnabledHudModuleIds)
        {
            if (modulesById.Remove(id, out IHudModule? module))
            {
                orderedModules.Add(module);
            }
        }

        orderedModules.AddRange(modulesById.Values.OrderBy(module => module.Metadata.DisplayOrder));
        Items = new ObservableCollection<HudNavigationItemViewModel>(
            orderedModules.Select(module => new HudNavigationItemViewModel(
                this,
                module,
                GetDisplayName(module.Id, language),
                settings.EnabledHudModuleIds.Contains(module.Id, StringComparer.Ordinal))));

        _selectedSettingsHudId = Items.FirstOrDefault()?.Id ?? string.Empty;
        _router.StateChanged += Router_StateChanged;
        RefreshCurrentItem();
    }

    public ObservableCollection<HudNavigationItemViewModel> Items { get; }

    public IReadOnlyList<HudNavigationItemViewModel> EnabledItems =>
        Items.Where(item => item.IsEnabled).ToArray();

    public bool ShowNavigation => EnabledItems.Count > 1;

    public string DefaultHudId
    {
        get => _defaultHudId;
        set
        {
            if (string.IsNullOrWhiteSpace(value)
                || string.Equals(_defaultHudId, value, StringComparison.Ordinal))
            {
                return;
            }

            _ = SetDefaultFromBindingAsync(value);
        }
    }

    public string SelectedSettingsHudId
    {
        get => _selectedSettingsHudId;
        set
        {
            if (SetProperty(ref _selectedSettingsHudId, value))
            {
                OnPropertyChanged(nameof(IsMusicSettingsSelected));
                OnPropertyChanged(nameof(IsHomeSettingsSelected));
            }
        }
    }

    public bool IsMusicSettingsSelected =>
        string.Equals(SelectedSettingsHudId, BuiltInHudIds.Music, StringComparison.Ordinal);

    public bool IsHomeSettingsSelected =>
        string.Equals(SelectedSettingsHudId, BuiltInHudIds.Home, StringComparison.Ordinal);

    internal async Task NavigateToAsync(string hudId)
    {
        try
        {
            await _router.NavigateToAsync(hudId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    internal async Task NavigateRelativeAsync(int offset)
    {
        HudNavigationItemViewModel[] enabled = [.. EnabledItems];
        if (enabled.Length < 2)
        {
            return;
        }

        int currentIndex = Array.FindIndex(
            enabled,
            item => string.Equals(item.Id, _router.CurrentHudId, StringComparison.Ordinal));
        int nextIndex = (currentIndex + offset) % enabled.Length;
        if (nextIndex < 0)
        {
            nextIndex += enabled.Length;
        }

        await NavigateToAsync(enabled[nextIndex].Id);
    }

    internal async Task SetEnabledAsync(string hudId, bool enabled)
    {
        await _configurationGate.WaitAsync();
        try
        {
            HudNavigationItemViewModel item = GetItem(hudId);
            if (item.IsEnabled == enabled)
            {
                return;
            }

            if (!enabled && EnabledItems.Count == 1)
            {
                item.RefreshEnabled();
                return;
            }

            item.SetEnabled(enabled);
            string[] enabledIds = Items
                .Where(candidate => candidate.IsEnabled)
                .Select(candidate => candidate.Id)
                .ToArray();
            string defaultHudId = enabledIds.Contains(_defaultHudId, StringComparer.Ordinal)
                ? _defaultHudId
                : enabledIds[0];

            try
            {
                await _router.ApplyConfigurationAsync(
                    defaultHudId,
                    enabledIds,
                    CancellationToken.None);
            }
            catch
            {
                item.SetEnabled(!enabled);
                throw;
            }

            _defaultHudId = defaultHudId;
            _settings.DefaultHudId = defaultHudId;
            _settings.EnabledHudModuleIds = [.. enabledIds];
            _saveSettings();
            NotifyConfigurationChanged();
        }
        finally
        {
            _configurationGate.Release();
        }
    }

    internal async Task SetEnabledFromBindingAsync(string hudId, bool enabled)
    {
        try
        {
            await SetEnabledAsync(hudId, enabled);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    internal async Task MoveAsync(string hudId, int offset)
    {
        await _configurationGate.WaitAsync();
        try
        {
            int oldIndex = Items
                .Select((item, index) => (item, index))
                .First(pair => string.Equals(pair.item.Id, hudId, StringComparison.Ordinal))
                .index;
            int newIndex = Math.Clamp(oldIndex + offset, 0, Items.Count - 1);
            if (oldIndex == newIndex)
            {
                return;
            }

            Items.Move(oldIndex, newIndex);
            string[] enabledIds = Items
                .Where(item => item.IsEnabled)
                .Select(item => item.Id)
                .ToArray();
            await _router.ApplyConfigurationAsync(
                _defaultHudId,
                enabledIds,
                CancellationToken.None);
            _settings.EnabledHudModuleIds = [.. enabledIds];
            _saveSettings();
            NotifyConfigurationChanged();
        }
        finally
        {
            _configurationGate.Release();
        }
    }

    internal async Task ResetToDefaultsAsync()
    {
        await _configurationGate.WaitAsync();
        try
        {
            string[] defaultIds = [BuiltInHudIds.Music, BuiltInHudIds.Home];
            for (int targetIndex = 0; targetIndex < defaultIds.Length; targetIndex++)
            {
                int currentIndex = Items
                    .Select((item, index) => (item, index))
                    .First(pair => string.Equals(
                        pair.item.Id,
                        defaultIds[targetIndex],
                        StringComparison.Ordinal))
                    .index;
                Items.Move(currentIndex, targetIndex);
            }

            foreach (HudNavigationItemViewModel item in Items)
            {
                item.SetEnabled(defaultIds.Contains(item.Id, StringComparer.Ordinal));
            }

            await _router.ApplyConfigurationAsync(
                BuiltInHudIds.Music,
                defaultIds,
                CancellationToken.None);
            _defaultHudId = BuiltInHudIds.Music;
            _settings.DefaultHudId = BuiltInHudIds.Music;
            _settings.EnabledHudModuleIds = [.. defaultIds];
            _saveSettings();
            NotifyConfigurationChanged();
        }
        finally
        {
            _configurationGate.Release();
        }
    }

    internal async Task ResetToDefaultsFromBindingAsync()
    {
        try
        {
            await ResetToDefaultsAsync();
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    internal void RefreshLocalizedText(AppLanguage language)
    {
        _language = language;
        foreach (HudNavigationItemViewModel item in Items)
        {
            item.DisplayName = GetDisplayName(item.Id, _language);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _router.StateChanged -= Router_StateChanged;
        _configurationGate.Dispose();
        _isDisposed = true;
    }

    private async Task SetDefaultFromBindingAsync(string hudId)
    {
        try
        {
            await _configurationGate.WaitAsync();
            try
            {
                if (!EnabledItems.Any(
                        item => string.Equals(item.Id, hudId, StringComparison.Ordinal)))
                {
                    return;
                }

                await _router.ApplyConfigurationAsync(
                    hudId,
                    EnabledItems.Select(item => item.Id),
                    CancellationToken.None);
                _defaultHudId = hudId;
                _settings.DefaultHudId = hudId;
                _saveSettings();
                OnPropertyChanged(nameof(DefaultHudId));
            }
            finally
            {
                _configurationGate.Release();
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private void Router_StateChanged(object? sender, EventArgs e) => RefreshCurrentItem();

    private void RefreshCurrentItem()
    {
        string? currentHudId = _router.CurrentHudId;
        foreach (HudNavigationItemViewModel item in Items)
        {
            item.IsCurrent = string.Equals(item.Id, currentHudId, StringComparison.Ordinal);
        }
    }

    private HudNavigationItemViewModel GetItem(string hudId) =>
        Items.First(item => string.Equals(item.Id, hudId, StringComparison.Ordinal));

    private void NotifyConfigurationChanged()
    {
        OnPropertyChanged(nameof(EnabledItems));
        OnPropertyChanged(nameof(ShowNavigation));
        OnPropertyChanged(nameof(DefaultHudId));
    }

    private static string GetDisplayName(string hudId, AppLanguage language)
    {
        bool japanese = language == AppLanguage.Japanese;
        return hudId switch
        {
            BuiltInHudIds.Music => japanese ? "音楽" : "Music",
            BuiltInHudIds.Home => japanese ? "ホーム" : "Home",
            _ => hudId
        };
    }
}
