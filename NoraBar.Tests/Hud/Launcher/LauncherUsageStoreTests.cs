using System.IO;
using NoraBar.Hud.Launcher;
using Xunit;

namespace NoraBar.Tests.Hud.Launcher;

public sealed class LauncherUsageStoreTests
{
    [Fact]
    public async Task DisposeAndClear_PersistForegroundUsageWithoutRestoringClearedHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"NoraBar.LauncherUsage.{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "usage.json");
        try
        {
            var store = new LauncherUsageStore(filePath);
            store.RecordLaunch("editor", "terminal", new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero));
            store.RecordForegroundDuration("editor", TimeSpan.FromMinutes(12));
            store.Dispose();

            using var reloaded = new LauncherUsageStore(filePath);
            LauncherUsageEntry entry = Assert.Single(reloaded.Snapshot);
            Assert.Equal(1, entry.LaunchCount);
            Assert.Equal(TimeSpan.FromMinutes(12), entry.ForegroundDuration);

            await reloaded.ClearAsync(CancellationToken.None);
            Assert.Empty(reloaded.Snapshot);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
