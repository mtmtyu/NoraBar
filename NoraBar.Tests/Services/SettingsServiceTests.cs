using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class SettingsServiceTests
{
    [Fact]
    public void FileStore_SaveAndLoad_UsesItsFixedTemporaryDirectory()
    {
        using var temporaryStore = new TemporarySettingsStore();
        var settings = new UserSettings { Language = AppLanguage.English };

        temporaryStore.Store.Save(settings);
        UserSettings loaded = temporaryStore.Store.Load();

        Assert.StartsWith(
            System.IO.Path.GetTempPath(),
            temporaryStore.DirectoryPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(System.IO.File.Exists(temporaryStore.Store.SettingsFilePath));
        Assert.Equal(AppLanguage.English, loaded.Language);
    }

    [Fact]
    public void IndependentStores_DoNotChangeOrRestoreProcessWideConfiguration()
    {
        using var first = new TemporarySettingsStore();
        using var second = new TemporarySettingsStore();
        first.Store.Save(new UserSettings { Language = AppLanguage.English });
        second.Store.Save(new UserSettings { Language = AppLanguage.Japanese });

        Assert.Equal(AppLanguage.English, first.Store.Load().Language);
        Assert.Equal(AppLanguage.Japanese, second.Store.Load().Language);
        Assert.NotEqual(first.Store.SettingsFilePath, second.Store.SettingsFilePath);
    }

    [Fact]
    public void DisposingOneStore_DoesNotDeleteAnotherStoresDirectory()
    {
        var first = new TemporarySettingsStore();
        using var second = new TemporarySettingsStore();
        first.Store.Save(new UserSettings());
        second.Store.Save(new UserSettings());

        first.Dispose();

        Assert.False(System.IO.Directory.Exists(first.DirectoryPath));
        Assert.True(System.IO.Directory.Exists(second.DirectoryPath));
        Assert.True(System.IO.File.Exists(second.Store.SettingsFilePath));
    }

    [Fact]
    public async Task IndependentStores_SaveAndLoadInParallel_DoNotInterfere()
    {
        using var first = new TemporarySettingsStore();
        using var second = new TemporarySettingsStore();

        await Task.WhenAll(
            Task.Run(() => SaveAndAssert(first.Store, AppLanguage.English)),
            Task.Run(() => SaveAndAssert(second.Store, AppLanguage.Japanese)));

        Assert.Equal(AppLanguage.English, first.Store.Load().Language);
        Assert.Equal(AppLanguage.Japanese, second.Store.Load().Language);
    }

    [Fact]
    public void Cleanup_RemovesOnlyDirectoryOwnedByTheStoreFixture()
    {
        string unrelatedDirectory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "NoraBar_Unrelated_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(unrelatedDirectory);
        var temporaryStore = new TemporarySettingsStore();
        temporaryStore.Store.Save(new UserSettings());

        try
        {
            temporaryStore.Dispose();

            Assert.False(System.IO.Directory.Exists(temporaryStore.DirectoryPath));
            Assert.True(System.IO.Directory.Exists(unrelatedDirectory));
        }
        finally
        {
            System.IO.Directory.Delete(unrelatedDirectory, recursive: true);
        }
    }

    private static void SaveAndAssert(FileSettingsStore store, AppLanguage language)
    {
        store.Save(new UserSettings { Language = language });
        Assert.Equal(language, store.Load().Language);
    }

    private sealed class TemporarySettingsStore : IDisposable
    {
        private bool _disposed;

        internal TemporarySettingsStore()
        {
            DirectoryPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NoraBar_SettingsStore_" + Guid.NewGuid().ToString("N"));
            Store = new FileSettingsStore(
                DirectoryPath,
                System.IO.Path.Combine(DirectoryPath, "legacy-settings.json"),
                enableFirstRunStartup: false);
        }

        internal string DirectoryPath { get; }
        internal FileSettingsStore Store { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (System.IO.Directory.Exists(DirectoryPath))
            {
                System.IO.Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
