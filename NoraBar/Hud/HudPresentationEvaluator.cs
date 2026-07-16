using System.Windows;

namespace NoraBar.Hud;

internal readonly record struct HudPresentationEvaluation(
    FrameworkElement View,
    HudSize PreferredSize);

internal static class HudPresentationEvaluator
{
    internal static bool TryEvaluate(
        HudRouterSnapshot snapshot,
        bool suppressExpansion,
        out HudPresentationEvaluation evaluation)
    {
        if (!snapshot.IsInitialized
            || snapshot.IsShuttingDown
            || snapshot.CurrentModule is null
            || snapshot.PresentationState == HudPresentationState.Collapsed
            || suppressExpansion)
        {
            evaluation = default;
            return false;
        }

        var context = new HudViewContext(snapshot.PresentationState);
        FrameworkElement view = snapshot.CurrentModule.GetView(context);
        HudSize preferredSize = snapshot.CurrentModule.GetPreferredSize(context);
        evaluation = new HudPresentationEvaluation(view, preferredSize);
        return true;
    }
}
