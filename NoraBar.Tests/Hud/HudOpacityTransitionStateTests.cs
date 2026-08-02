using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HudOpacityTransitionStateTests
{
    [Fact]
    public void TryTransition_AnimatesOnlyWhenVisibilityStateChanges()
    {
        var state = new HudOpacityTransitionState();

        Assert.True(state.TryTransition(collapseContent: false));
        Assert.False(state.TryTransition(collapseContent: false));
        Assert.True(state.TryTransition(collapseContent: true));
        Assert.False(state.TryTransition(collapseContent: true));
        Assert.True(state.TryTransition(collapseContent: false));
    }
}
