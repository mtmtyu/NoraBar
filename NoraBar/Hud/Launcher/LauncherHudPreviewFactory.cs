using NoraBar.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherHudPreview : IDisposable
{
    private bool _isDisposed;
    internal LauncherHudPreview(FrameworkElement view, HudSize preferredSize)
    {
        View = view;
        PreferredSize = preferredSize;
    }
    internal FrameworkElement View { get; }
    internal HudSize PreferredSize { get; }
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        View.DataContext = null;
        (View as IDisposable)?.Dispose();
    }
}

internal sealed class LauncherPreviewSession
{
    private LauncherHudPreview? _preview;
    internal LauncherHudPreview Show(Func<LauncherHudPreview> create, Action<LauncherHudPreview> show)
    {
        if (_preview is not null) throw new InvalidOperationException("A Launcher preview is already active.");
        LauncherHudPreview preview = create();
        try
        {
            show(preview);
            _preview = preview;
            return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }
    }
    internal void Suspend(Action clearContent)
    {
        LauncherHudPreview? preview = _preview;
        _preview = null;
        BestEffortResourceReleaser.ReleaseAll(() => preview?.Dispose(), clearContent);
    }
}

internal static class LauncherHudPreviewFactory
{
    internal static LauncherHudPreview Create(LauncherSettingsViewModel settings)
    {
        FrameworkElement view = LauncherHudViewFactory.Create();
        view.DataContext = new LauncherPreviewViewModel(settings);
        return new LauncherHudPreview(view, LauncherHudLayout.Calculate(HudPresentationState.Expanded));
    }
}

internal sealed class LauncherPreviewViewModel
{
    internal LauncherPreviewViewModel(LauncherSettingsViewModel settings)
    {
        Strings = settings.Strings;
        Pages = settings.Pages;
        CurrentPage = settings.SelectedPage ?? settings.Pages.FirstOrDefault();
        Groups = new ObservableCollection<LauncherGroupViewModel>(
            (CurrentPage?.Groups ?? []).Select(group => new LauncherGroupViewModel(
                group.Id,
                group.DisplayName,
                group.Items.Select(item => new LauncherItemViewModel(item.ToModel(), item.IsMissing, settings.Strings)).ToArray())));
        VisibleRows = new ObservableCollection<LauncherItemRowViewModel>(Groups.SelectMany(group =>
            group.Items.Chunk(7).Select((row, index) => new LauncherItemRowViewModel(
                group.Id, group.DisplayName, index == 0, row.ToArray()))));
        PeekItems = new ObservableCollection<LauncherItemViewModel>(Groups.SelectMany(group => group.Items).Take(7));
    }
    public LauncherLocalization Strings { get; }
    public bool IsExpanded => true;
    public bool IsSearchMode => false;
    public string SearchQuery => string.Empty;
    public ObservableCollection<LauncherPageEditorViewModel> Pages { get; }
    public LauncherPageEditorViewModel? CurrentPage { get; }
    public ObservableCollection<LauncherGroupViewModel> Groups { get; }
    public ObservableCollection<LauncherItemRowViewModel> VisibleRows { get; }
    public ObservableCollection<LauncherItemViewModel> PeekItems { get; }
    public ObservableCollection<LauncherItemViewModel> SearchResults { get; } = [];
}
