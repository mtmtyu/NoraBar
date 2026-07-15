using System.Reflection;
using NoraBar.Hud;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class MainWindowDependencyTests
{
    [Theory]
    [InlineData(nameof(MainViewModel.DisableExpandOnFullscreen), true)]
    [InlineData(nameof(MainViewModel.SelectedLanguage), false)]
    [InlineData(nameof(MainViewModel.HasCustomPosition), false)]
    [InlineData(nameof(MainViewModel.IsPositionEditMode), false)]
    [InlineData(null, false)]
    public void TryScheduleHudPresentationRefresh_OnlyMatchesFullscreenExpansionSetting(
        string? propertyName,
        bool expected)
    {
        int schedulingCount = 0;

        bool scheduled = HudPresentationRefreshScheduler.TrySchedule(
            propertyName,
            _ => schedulingCount++,
            () => { });

        Assert.Equal(expected, scheduled);
        Assert.Equal(expected ? 1 : 0, schedulingCount);
    }

    [Fact]
    public void MainWindow_DependsOnRouterButDoesNotStoreHudModule()
    {
        Type type = typeof(MainWindow);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors());
        Type[] parameters = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(MainViewModel), parameters);
        Assert.Contains(typeof(HudRouter), parameters);
        Assert.Contains(typeof(Func<Task>), parameters);
        Assert.DoesNotContain(parameters, parameter => typeof(IHudModule).IsAssignableFrom(parameter));
        Assert.DoesNotContain(
            type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => typeof(IHudModule).IsAssignableFrom(field.FieldType));
    }

    [Fact]
    public void MainViewModel_DoesNotOwnHudPresentationState()
    {
        Type type = typeof(MainViewModel);

        Assert.Null(type.GetProperty("CurrentState"));
        Assert.Null(type.GetProperty("SetStateCommand"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldShowMainWindow_RequiresStartupLeaseAndNoShutdownRequest(
        bool startupCompletionAcquired,
        bool shutdownRequested,
        bool expected)
    {
        Assert.Equal(expected, App.ShouldShowMainWindow(
            startupCompletionAcquired,
            shutdownRequested));
    }
}
