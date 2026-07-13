using System.ComponentModel;
using NoraBar.Models;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Music;

internal sealed class MainViewModelMusicHudPresentationSource : IMusicHudPresentationSource
{
    private readonly MainViewModel _viewModel;

    internal MainViewModelMusicHudPresentationSource(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
    }

    public DesignVariant CurrentVariant => _viewModel.CurrentVariant;

    public bool ShowProgressBar => _viewModel.ShowProgressBar;

    public bool ShowLyrics => _viewModel.ShowLyrics;

    public bool HasMultipleSessions => _viewModel.Music.HasMultipleSessions;

    public object ViewDataContext => _viewModel;

    public event PropertyChangedEventHandler? ShellPropertyChanged
    {
        add => _viewModel.PropertyChanged += value;
        remove => _viewModel.PropertyChanged -= value;
    }

    public event PropertyChangedEventHandler? MusicPropertyChanged
    {
        add => _viewModel.Music.PropertyChanged += value;
        remove => _viewModel.Music.PropertyChanged -= value;
    }

    public void Cleanup()
    {
        _viewModel.Music.Cleanup();
    }
}
