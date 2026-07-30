namespace NoraBar;

internal sealed class HudOpacityTransitionState
{
    private bool _isContentVisible;

    internal bool TryTransition(bool collapseContent)
    {
        bool targetVisible = !collapseContent;
        if (_isContentVisible == targetVisible)
        {
            return false;
        }

        _isContentVisible = targetVisible;
        return true;
    }
}
