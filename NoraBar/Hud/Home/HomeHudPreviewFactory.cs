using System.Windows;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;

namespace NoraBar.Hud.Home;

internal sealed class HomeHudPreview : IDisposable
{
    private readonly IDisposable _viewModel;
    private bool _isDisposed;

    internal HomeHudPreview(
        FrameworkElement view,
        HudSize preferredSize,
        IDisposable viewModel)
    {
        View = view;
        PreferredSize = preferredSize;
        _viewModel = viewModel;
    }

    internal FrameworkElement View { get; }

    internal HudSize PreferredSize { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        BestEffortResourceReleaser.ReleaseAll(
            () => (View as IDisposable)?.Dispose(),
            _viewModel.Dispose);
    }
}

internal static class HomePreviewLifecycle
{
    internal static void Cleanup(
        IDisposable? preview,
        Action clearOwnership,
        Action clearContent)
    {
        ArgumentNullException.ThrowIfNull(clearOwnership);
        ArgumentNullException.ThrowIfNull(clearContent);
        BestEffortResourceReleaser.ReleaseAll(
            () => preview?.Dispose(),
            clearOwnership,
            clearContent);
    }
}
internal static class HomeHudPreviewFactory
{
    internal static HomeHudPreview Create(MainViewModel mainViewModel)
    {
        ArgumentNullException.ThrowIfNull(mainViewModel);
        return Create(
            new HomeHudViewModel(mainViewModel),
            HomeHudViewFactory.Create,
            static (view, dataContext) => view.DataContext = dataContext,
            HomeHudLayout.Calculate,
            static (view, preferredSize, source) =>
                new HomeHudPreview(view, preferredSize, source));
    }

    internal static HomeHudPreview Create(
        IHomeHudPresentationSource source,
        Func<HomeHudDesignVariant, FrameworkElement> createView,
        Action<FrameworkElement, object> assignDataContext,
        Func<HomeHudDesignVariant, IReadOnlyList<Widgets.HomeWidgetConfig>?, double, double, HudSize> calculateLayout,
        Func<FrameworkElement, HudSize, IDisposable, HomeHudPreview> createPreview)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(createView);
        ArgumentNullException.ThrowIfNull(assignDataContext);
        ArgumentNullException.ThrowIfNull(calculateLayout);
        ArgumentNullException.ThrowIfNull(createPreview);

        FrameworkElement? view = null;
        try
        {
            source.Initialize();
            view = createView(source.DesignVariant);
            assignDataContext(view, source.ViewDataContext);
            HudSize preferredSize = calculateLayout(
                source.DesignVariant,
                source.ActiveWidgets,
                source.MaxWidgetWidth,
                source.MaxWidgetHeight);
            source.Start();
            return createPreview(view, preferredSize, source);
        }
        catch (Exception creationException)
        {
            try
            {
                BestEffortResourceReleaser.ReleaseAll(
                    () => (view as IDisposable)?.Dispose(),
                    source.Dispose);
            }
            catch (AggregateException cleanupException)
            {
                throw new AggregateException(
                    "Home HUD preview creation and cleanup failed.",
                    [creationException, .. cleanupException.InnerExceptions]);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(creationException)
                .Throw();
            throw;
        }
    }
}
