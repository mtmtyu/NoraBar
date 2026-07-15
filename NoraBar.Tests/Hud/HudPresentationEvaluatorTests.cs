using System.Runtime.ExceptionServices;
using System.Windows;
using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HudPresentationEvaluatorTests
{
    [Fact]
    public void TryEvaluate_WhenCollapsed_DoesNotQueryModulePresentation()
    {
        RunInSta(() =>
        {
            var module = new CountingHudModule();
            var snapshot = CreateSnapshot(module, HudPresentationState.Collapsed);

            bool evaluated = HudPresentationEvaluator.TryEvaluate(
                snapshot,
                suppressExpansion: false,
                out _);

            Assert.False(evaluated);
            Assert.Equal(0, module.GetViewCount);
            Assert.Equal(0, module.GetPreferredSizeCount);
        });
    }

    [Fact]
    public void TryEvaluate_WhenExpandedAndSuppressed_DoesNotQueryModulePresentation()
    {
        RunInSta(() =>
        {
            var module = new CountingHudModule();
            var snapshot = CreateSnapshot(module, HudPresentationState.Expanded);

            bool evaluated = HudPresentationEvaluator.TryEvaluate(
                snapshot,
                suppressExpansion: true,
                out _);

            Assert.False(evaluated);
            Assert.Equal(0, module.GetViewCount);
            Assert.Equal(0, module.GetPreferredSizeCount);
        });
    }

    [Fact]
    public void TryEvaluate_WhenExpandedAndNotSuppressed_QueriesViewAndSizeOnce()
    {
        RunInSta(() =>
        {
            var module = new CountingHudModule();
            var snapshot = CreateSnapshot(module, HudPresentationState.Expanded);

            bool evaluated = HudPresentationEvaluator.TryEvaluate(
                snapshot,
                suppressExpansion: false,
                out HudPresentationEvaluation evaluation);

            Assert.True(evaluated);
            Assert.Same(module.View, evaluation.View);
            Assert.Equal(module.PreferredSize, evaluation.PreferredSize);
            Assert.Equal(1, module.GetViewCount);
            Assert.Equal(1, module.GetPreferredSizeCount);
        });
    }

    private static HudRouterSnapshot CreateSnapshot(
        IHudModule module,
        HudPresentationState presentationState)
    {
        return new HudRouterSnapshot(
            module.Id,
            module,
            presentationState,
            IsInitialized: true,
            IsShuttingDown: false);
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class CountingHudModule : IHudModule
    {
        private FrameworkElement? _view;

        public string Id => "counting";

        public HudModuleMetadata Metadata { get; } = new("Counting", 0);

        public FrameworkElement View => _view ??= new FrameworkElement();

        public HudSize PreferredSize { get; } = new(320, 80);

        public int GetViewCount { get; private set; }

        public int GetPreferredSizeCount { get; private set; }

        public event EventHandler? PresentationInvalidated
        {
            add { }
            remove { }
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ActivateAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DeactivateAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public FrameworkElement GetView(HudViewContext context)
        {
            GetViewCount++;
            return View;
        }

        public HudSize GetPreferredSize(HudViewContext context)
        {
            GetPreferredSizeCount++;
            return PreferredSize;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
