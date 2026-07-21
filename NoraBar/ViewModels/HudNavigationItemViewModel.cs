using System.Windows.Input;
using NoraBar.Hud;

namespace NoraBar.ViewModels;

public sealed class HudNavigationItemViewModel : ViewModelBase
{
    private readonly HudNavigationViewModel _owner;
    private string _displayName;
    private bool _isEnabled;
    private bool _isCurrent;

    internal HudNavigationItemViewModel(
        HudNavigationViewModel owner,
        IHudModule module,
        string displayName,
        bool isEnabled)
    {
        _owner = owner;
        Id = module.Id;
        Metadata = module.Metadata;
        _displayName = displayName;
        _isEnabled = isEnabled;
        IconText = string.Equals(Id, BuiltInHudIds.Home, StringComparison.Ordinal)
            ? "\uE80F"
            : "\uE93C";
        NavigateCommand = new RelayCommand(async _ => await _owner.NavigateToAsync(Id));
        MoveUpCommand = new RelayCommand(async _ => await _owner.MoveAsync(Id, -1));
        MoveDownCommand = new RelayCommand(async _ => await _owner.MoveAsync(Id, 1));
    }

    public string Id { get; }

    public HudModuleMetadata Metadata { get; }

    public string IconText { get; }

    public string DisplayName
    {
        get => _displayName;
        internal set => SetProperty(ref _displayName, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _ = _owner.SetEnabledFromBindingAsync(Id, value);
        }
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        internal set => SetProperty(ref _isCurrent, value);
    }

    public ICommand NavigateCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    internal void SetEnabled(bool value) => SetProperty(ref _isEnabled, value, nameof(IsEnabled));

    internal void RefreshEnabled() => OnPropertyChanged(nameof(IsEnabled));
}
