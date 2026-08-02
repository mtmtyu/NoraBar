using System.Windows;
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
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(viewModel);
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

internal sealed class HomePreviewSession
{
    private HomeHudPreview? _preview;

    internal HomeHudPreview? Current => _preview;

    internal HomeHudPreview Show(
        Func<HomeHudPreview> createPreview,
        Action<HomeHudPreview> showPreview,
        Action clearContent,
        Action<Exception> reportCleanupFailure)
    {
        ArgumentNullException.ThrowIfNull(createPreview);
        ArgumentNullException.ThrowIfNull(showPreview);
        ArgumentNullException.ThrowIfNull(clearContent);
        ArgumentNullException.ThrowIfNull(reportCleanupFailure);
        if (_preview is not null)
        {
            throw new InvalidOperationException("A Home HUD preview is already active.");
        }

        HomeHudPreview preview = createPreview();
        _preview = preview;
        try
        {
            showPreview(preview);
            return preview;
        }
        catch
        {
            HomeHudPreview failedPreview = preview;
            BestEffortResourceReleaser.ReleaseAllAndReport(
                reportCleanupFailure,
                clearContent,
                () => _preview = null,
                failedPreview.Dispose);
            throw;
        }
    }

    internal void Suspend(
        Action clearContent,
        Action<Exception> reportCleanupFailure)
    {
        ArgumentNullException.ThrowIfNull(clearContent);
        ArgumentNullException.ThrowIfNull(reportCleanupFailure);
        HomeHudPreview? preview = _preview;
        BestEffortResourceReleaser.ReleaseAllAndReport(
            reportCleanupFailure,
            () => preview?.Dispose(),
            () => _preview = null,
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
        Func<FrameworkElement> createView,
        Action<FrameworkElement, object> assignDataContext,
        Func<IReadOnlyList<Widgets.HomeWidgetConfig>?, double, double, HudSize> calculateLayout,
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
            view = createView();
            assignDataContext(view, source.ViewDataContext);
            HudSize preferredSize = calculateLayout(
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
