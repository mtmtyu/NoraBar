using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherSearchEngineTests
{
    private static readonly LauncherItem Browser = new(
        "browser", "Microsoft Edge", LauncherItemKind.Win32Application, @"C:\Edge\msedge.exe");
    private static readonly LauncherItem Editor = new(
        "editor", "Visual Studio Code", LauncherItemKind.Win32Application, @"C:\Code\Code.exe");

    [Fact]
    public void Search_DeduplicatesInstalledApplicationAlreadyRegisteredCaseInsensitively()
    {
        LauncherSearchResult[] results = LauncherSearchEngine.Search(
            "EDGE",
            [Browser],
            [Editor, Browser with { Id = "installed-edge" }]);

        LauncherSearchResult result = Assert.Single(results);
        Assert.Equal(LauncherSearchSource.Registered, result.Source);
        Assert.Equal(Browser.Id, result.Item.Id);
    }

    [Fact]
    public void Search_ReturnsMatchingInstalledApplication()
    {
        LauncherSearchResult result = Assert.Single(LauncherSearchEngine.Search("visual", [], [Editor]));

        Assert.Equal(Editor.Id, result.Item.Id);
        Assert.Equal(LauncherSearchSource.Installed, result.Source);
    }

    [Fact]
    public void Search_RanksPrefixBeforeSubsequence()
    {
        LauncherItem prefix = Browser with { Id = "prefix", DisplayName = "Code Editor" };
        LauncherItem subsequence = Browser with { Id = "subsequence", DisplayName = "Visual Studio Code" };

        LauncherSearchResult[] results = LauncherSearchEngine.Search("code", [subsequence, prefix], []);

        Assert.Equal("prefix", results[0].Item.Id);
    }

    [Fact]
    public void Search_EmptyQueryReturnsNoSearchResults()
    {
        Assert.Empty(LauncherSearchEngine.Search("   ", [Browser], [Editor]));
    }
}
