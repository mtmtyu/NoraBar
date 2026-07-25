using System.ComponentModel;

namespace NoraBar.ViewModels;

internal interface IMusicChangeSource : INotifyPropertyChanged
{
    int CurrentLyricIndex { get; }
}
