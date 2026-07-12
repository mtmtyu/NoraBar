using NoraBar.Hud;
using Xunit;

namespace NoraBar.Tests.Hud;

public sealed class HudRegistryTests
{
    [Fact]
    public void Register_AddsModuleToRegistry()
    {
        var registry = new HudRegistry();
        var module = new FakeHudModule(BuiltInHudIds.Music);

        registry.Register(module);

        Assert.Same(module, registry.Modules[0]);
    }

    [Fact]
    public void Register_RejectsDuplicateIdUsingOrdinalComparison()
    {
        var registry = new HudRegistry();
        registry.Register(new FakeHudModule("music"));

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeHudModule("music")));
        registry.Register(new FakeHudModule("Music"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" music")]
    [InlineData("music ")]
    [InlineData("mus\u0000ic")]
    [InlineData("mus\u001fic")]
    [InlineData("mus\u007fic")]
    public void Register_RejectsInvalidIds(string? id)
    {
        var registry = new HudRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(new FakeHudModule(id!)));
    }

    [Fact]
    public void TryGet_UsesOrdinalComparison()
    {
        var registry = new HudRegistry();
        var module = new FakeHudModule("music");
        registry.Register(module);

        bool foundExactId = registry.TryGet("music", out IHudModule? exactModule);
        bool foundDifferentCase = registry.TryGet("Music", out IHudModule? differentCaseModule);

        Assert.True(foundExactId);
        Assert.Same(module, exactModule);
        Assert.False(foundDifferentCase);
        Assert.Null(differentCaseModule);
    }

    [Fact]
    public void Modules_PreservesRegistrationOrder()
    {
        var registry = new HudRegistry();
        var music = new FakeHudModule("music");
        var launcher = new FakeHudModule("launcher");

        registry.Register(music);
        registry.Register(launcher);

        Assert.Equal(new IHudModule[] { music, launcher }, registry.Modules);
    }

    [Fact]
    public async Task DisposeAsync_DisposesEachModuleOnceInRegistrationOrder()
    {
        var calls = new List<string>();
        var registry = new HudRegistry();
        var music = new FakeHudModule("music", calls);
        var launcher = new FakeHudModule("launcher", calls);
        registry.Register(music);
        registry.Register(launcher);

        await registry.DisposeAsync();
        await registry.DisposeAsync();

        Assert.Equal(new[] { "music:dispose", "launcher:dispose" }, calls);
        Assert.Equal(1, music.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task Register_AfterDisposeCompleted_ThrowsObjectDisposedException()
    {
        var registry = new HudRegistry();
        await registry.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => registry.Register(new FakeHudModule("launcher")));
    }

    [Fact]
    public async Task Register_WhileDisposeIsInProgress_ThrowsWithoutChangingRegistry()
    {
        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new HudRegistry();
        var music = new FakeHudModule("music")
        {
            DisposeStartedSignal = disposeStarted,
            DisposeWaitTask = allowDispose.Task
        };
        registry.Register(music);

        Task disposeTask = registry.DisposeAsync().AsTask();
        await disposeStarted.Task;
        Exception? registerException = Record.Exception(
            () => registry.Register(new FakeHudModule("launcher")));
        allowDispose.SetResult(true);
        Exception? disposeException = await Record.ExceptionAsync(() => disposeTask);

        Assert.IsType<ObjectDisposedException>(registerException);
        Assert.Null(disposeException);
        Assert.Equal(new IHudModule[] { music }, registry.Modules);
        Assert.False(registry.TryGet("launcher", out _));
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentCallsWaitForSharedCompletionAndDisposeOnce()
    {
        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new HudRegistry();
        var music = new FakeHudModule("music")
        {
            DisposeStartedSignal = disposeStarted,
            DisposeWaitTask = allowDispose.Task
        };
        registry.Register(music);

        Task firstDispose = registry.DisposeAsync().AsTask();
        await disposeStarted.Task;
        Task secondDispose = registry.DisposeAsync().AsTask();
        bool secondCompletedBeforeRelease = secondDispose.IsCompleted;
        allowDispose.SetResult(true);
        await Task.WhenAll(firstDispose, secondDispose);

        Assert.False(secondCompletedBeforeRelease);
        Assert.Equal(1, music.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenModuleFails_DisposesRemainingModulesAndRethrowsOriginalException()
    {
        var expectedException = new InvalidOperationException("music dispose failed");
        var registry = new HudRegistry();
        var music = new FakeHudModule("music") { DisposeException = expectedException };
        var launcher = new FakeHudModule("launcher");
        registry.Register(music);
        registry.Register(launcher);

        InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.DisposeAsync().AsTask());

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, music.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenMultipleModulesFail_ThrowsAggregateAfterDisposingAllModules()
    {
        var musicException = new InvalidOperationException("music dispose failed");
        var launcherException = new InvalidOperationException("launcher dispose failed");
        var registry = new HudRegistry();
        var music = new FakeHudModule("music") { DisposeException = musicException };
        var launcher = new FakeHudModule("launcher") { DisposeException = launcherException };
        registry.Register(music);
        registry.Register(launcher);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => registry.DisposeAsync().AsTask());

        Assert.Equal(new Exception[] { musicException, launcherException }, exception.InnerExceptions);
        Assert.Equal(1, music.DisposeCount);
        Assert.Equal(1, launcher.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentCallsShareFailure()
    {
        var expectedException = new InvalidOperationException("music dispose failed");
        var disposeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDispose = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new HudRegistry();
        var music = new FakeHudModule("music")
        {
            DisposeException = expectedException,
            DisposeStartedSignal = disposeStarted,
            DisposeWaitTask = allowDispose.Task
        };
        registry.Register(music);

        Task firstDispose = registry.DisposeAsync().AsTask();
        await disposeStarted.Task;
        Task secondDispose = registry.DisposeAsync().AsTask();
        allowDispose.SetResult(true);
        Exception? firstException = await Record.ExceptionAsync(() => firstDispose);
        Exception? secondException = await Record.ExceptionAsync(() => secondDispose);

        Assert.Same(expectedException, firstException);
        Assert.Same(expectedException, secondException);
        Assert.Equal(1, music.DisposeCount);
    }
}
