using System.IO;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class MainWindowAsyncEventSafetyTests
{
    [Fact]
    public void NavigationWheelHandler_ReportsAsyncFailuresBeforeFinallyCleanup()
    {
        string source = File.ReadAllText(GetMainWindowSourcePath());
        int methodStart = source.IndexOf(
            "private async void HudNavigation_PreviewMouseWheel",
            StringComparison.Ordinal);
        int nextMethod = source.IndexOf(
            "private double GetPresentationWidth",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && nextMethod > methodStart);
        string handler = source[methodStart..nextMethod];

        Assert.Contains("catch (Exception exception)", handler, StringComparison.Ordinal);
        Assert.Contains("Trace.TraceError(exception.ToString())", handler, StringComparison.Ordinal);
        Assert.True(
            handler.IndexOf("catch (Exception exception)", StringComparison.Ordinal)
            < handler.IndexOf("finally", StringComparison.Ordinal));
    }

    private static string GetMainWindowSourcePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "NoraBar.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "NoraBar", "MainWindow.xaml.cs");
    }
}
