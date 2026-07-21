using System.Windows;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudPreview : IDisposable
{
    private readonly HomeHudViewModel _viewModel;

    internal HomeHudPreview(
        FrameworkElement view,
        HudSize preferredSize,
        HomeHudViewModel viewModel)
    {
        View = view;
        PreferredSize = preferredSize;
        _viewModel = viewModel;
    }

    internal FrameworkElement View { get; }

    internal HudSize PreferredSize { get; }

    public void Dispose() => _viewModel.Dispose();
}

internal static class HomeHudPreviewFactory
{
    internal static HomeHudPreview Create(MainViewModel mainViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        var source = new HomeHudViewModel(mainViewModel);
        source.Initialize();
        source.Start();
        FrameworkElement view = HomeHudViewFactory.Create(source.DesignVariant);
        view.DataContext = source;
        return new HomeHudPreview(
            view,
            HomeHudLayout.Calculate(source.DesignVariant),
            source);
    }
}
