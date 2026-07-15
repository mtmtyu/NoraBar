using NoraBar.ViewModels;

namespace NoraBar;

internal static class HudPresentationRefreshScheduler
{
    internal static bool TrySchedule(
        string? propertyName,
        Action<Action> schedule,
        Action refresh)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(refresh);

        if (propertyName != nameof(MainViewModel.DisableExpandOnFullscreen))
        {
            return false;
        }

        schedule(refresh);
        return true;
    }
}
