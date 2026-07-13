using System.ComponentModel;
using NoraBar.Models;

namespace NoraBar.Hud.Music;

internal interface IMusicHudPresentationSource
{
    DesignVariant CurrentVariant { get; }

    bool ShowProgressBar { get; }

    bool ShowLyrics { get; }

    bool HasMultipleSessions { get; }

    object ViewDataContext { get; }

    event PropertyChangedEventHandler? ShellPropertyChanged;

    event PropertyChangedEventHandler? MusicPropertyChanged;

    void Cleanup();
}
