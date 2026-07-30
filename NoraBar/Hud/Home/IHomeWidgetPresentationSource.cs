using System.ComponentModel;
using NoraBar.Hud.Home.Widgets;

namespace NoraBar.Hud.Home;

internal interface IHomeWidgetPresentationSource : INotifyPropertyChanged
{
    IReadOnlyList<HomeWidgetConfig> ActiveWidgets { get; }

    IReadOnlyList<HomeWorldClockItemViewModel> WorldClockItems { get; }

    double MaxWidgetWidth { get; }

    double MaxWidgetHeight { get; }

    bool IsWidgetEditMode { get; }

    void UpdateActiveWidgets(IReadOnlyList<HomeWidgetConfig> widgets);
}
