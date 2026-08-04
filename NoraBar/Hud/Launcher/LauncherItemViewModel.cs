using System.Windows.Media;
using NoraBar.ViewModels;
using NoraBar.Services;
using NoraBar.Models;

namespace NoraBar.Hud.Launcher;

internal sealed class LauncherItemViewModel : ViewModelBase
{
    private ImageSource? _icon;
    private bool _isRunning;
    private bool _isSelected;

    internal LauncherItemViewModel(LauncherItem item, bool isMissing, LauncherLocalization? strings = null)
    {
        Item = item;
        IsMissing = isMissing;
        Strings = strings ?? new LauncherLocalization(AppLanguage.English);
    }

    internal LauncherItem Item { get; }
    private LauncherLocalization Strings { get; }
    public string Id => Item.Id;
    public string DisplayName => Item.DisplayName;
    public bool IsMissing { get; }
    public string Tooltip => IsMissing ? $"{DisplayName}\n{Strings.TargetNotFound}: {Item.Target}" : $"{DisplayName}\n{Item.Target}";
    public ImageSource? Icon { get => _icon; internal set => SetProperty(ref _icon, value); }
    public bool IsRunning { get => _isRunning; internal set => SetProperty(ref _isRunning, value); }
    public bool IsSelected { get => _isSelected; internal set => SetProperty(ref _isSelected, value); }
}

internal sealed record LauncherGroupViewModel(string Id, string DisplayName, IReadOnlyList<LauncherItemViewModel> Items);

internal sealed record LauncherItemRowViewModel(string GroupId, string GroupDisplayName, bool ShowHeader, IReadOnlyList<LauncherItemViewModel> Items);
