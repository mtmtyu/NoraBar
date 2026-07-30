using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

public sealed class HomeWorldClockItemViewModel : ViewModelBase
{
    private string _label;
    private string _timeText = string.Empty;
    private string _dateText = string.Empty;
    private string _timeZoneId;

    public HomeWorldClockItemViewModel(string label, string timeZoneId)
    {
        _label = label;
        _timeZoneId = timeZoneId;
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string TimeZoneId
    {
        get => _timeZoneId;
        set => SetProperty(ref _timeZoneId, value);
    }

    public string TimeText
    {
        get => _timeText;
        internal set => SetProperty(ref _timeText, value);
    }

    public string DateText
    {
        get => _dateText;
        internal set => SetProperty(ref _dateText, value);
    }
}
