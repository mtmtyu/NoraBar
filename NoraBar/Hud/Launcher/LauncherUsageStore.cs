using System.IO;
using System.Text.Json;

namespace NoraBar.Hud.Launcher;

internal interface ILauncherUsageStore : IDisposable
{
    IReadOnlyList<LauncherUsageEntry> Snapshot { get; }
    void RecordLaunch(string applicationId, string? foregroundApplicationId, DateTimeOffset timestamp);
    void RecordForegroundDuration(string applicationId, TimeSpan duration);
    Task ClearAsync(CancellationToken cancellationToken);
}

internal sealed class LauncherUsageStore : ILauncherUsageStore
{
    private const int MaximumApplicationEntries = 500;
    private const int MaximumContextEntriesPerApplication = 32;
    private static readonly TimeSpan PersistenceDelay = TimeSpan.FromSeconds(5);
    private readonly object _syncRoot = new();
    private readonly object _fileSyncRoot = new();
    private readonly string _filePath;
    private readonly Dictionary<string, MutableUsage> _entries = new(StringComparer.Ordinal);
    private readonly Timer _persistenceTimer;
    private bool _isDisposed;

    internal LauncherUsageStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NoraBar", "Launcher", "usage.json");
        Load();
        _persistenceTimer = new Timer(_ => PersistSafely(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public IReadOnlyList<LauncherUsageEntry> Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Values.Select(entry => entry.ToImmutable()).ToArray();
            }
        }
    }

    public void RecordLaunch(string applicationId, string? foregroundApplicationId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            MutableUsage entry = GetOrCreate(applicationId);
            entry.LaunchCount++;
            entry.LastUsed = timestamp;
            entry.HourBuckets[timestamp.Hour] = entry.HourBuckets.GetValueOrDefault(timestamp.Hour) + 1;
            if (!string.IsNullOrWhiteSpace(foregroundApplicationId)
                && !string.Equals(applicationId, foregroundApplicationId, StringComparison.Ordinal))
            {
                entry.ContextTransitions[foregroundApplicationId] =
                    entry.ContextTransitions.GetValueOrDefault(foregroundApplicationId) + 1;
                TrimLowest(entry.ContextTransitions, MaximumContextEntriesPerApplication);
            }
            TrimOldest();
            SchedulePersistence();
        }
    }

    public void RecordForegroundDuration(string applicationId, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            GetOrCreate(applicationId).ForegroundDuration += duration;
            SchedulePersistence();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _entries.Clear();
            _persistenceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        await Task.Run(() =>
        {
            lock (_fileSyncRoot)
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _persistenceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        using var callbackCompleted = new ManualResetEvent(false);
        if (_persistenceTimer.Dispose(callbackCompleted))
        {
            callbackCompleted.WaitOne();
        }
        PersistSafely();
    }

    private MutableUsage GetOrCreate(string id)
    {
        if (!_entries.TryGetValue(id, out MutableUsage? entry))
        {
            entry = new MutableUsage { ApplicationId = id, IsLaunchable = true };
            _entries.Add(id, entry);
        }
        return entry;
    }

    private void SchedulePersistence() => _persistenceTimer.Change(PersistenceDelay, Timeout.InfiniteTimeSpan);

    private void PersistSafely()
    {
        LauncherUsageEntry[] snapshot;
        lock (_syncRoot)
        {
            snapshot = _entries.Values.Select(entry => entry.ToImmutable()).ToArray();
        }
        try
        {
            lock (_fileSyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                string temporaryPath = _filePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot));
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
        }
        catch (Exception) when (_isDisposed) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            LauncherUsageEntry[] loaded = JsonSerializer.Deserialize<LauncherUsageEntry[]>(File.ReadAllText(_filePath)) ?? [];
            foreach (LauncherUsageEntry entry in LauncherSmartScorer.BoundHistory(loaded, MaximumApplicationEntries))
            {
                _entries[entry.ApplicationId] = MutableUsage.FromImmutable(entry);
            }
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void TrimOldest()
    {
        while (_entries.Count > MaximumApplicationEntries)
        {
            string oldest = _entries.Values.MinBy(entry => entry.LastUsed)!.ApplicationId;
            _entries.Remove(oldest);
        }
    }

    private static void TrimLowest(Dictionary<string, int> values, int maximumCount)
    {
        while (values.Count > maximumCount)
        {
            string lowest = values.MinBy(pair => pair.Value).Key;
            values.Remove(lowest);
        }
    }

    private sealed class MutableUsage
    {
        public required string ApplicationId { get; init; }
        public int LaunchCount { get; set; }
        public DateTimeOffset LastUsed { get; set; }
        public TimeSpan ForegroundDuration { get; set; }
        public bool IsLaunchable { get; set; }
        public Dictionary<int, int> HourBuckets { get; } = [];
        public Dictionary<string, int> ContextTransitions { get; } = new(StringComparer.Ordinal);

        public LauncherUsageEntry ToImmutable() => new(
            ApplicationId, LaunchCount, LastUsed, ForegroundDuration, IsLaunchable,
            new Dictionary<int, int>(HourBuckets),
            new Dictionary<string, int>(ContextTransitions, StringComparer.Ordinal));

        public static MutableUsage FromImmutable(LauncherUsageEntry entry)
        {
            var result = new MutableUsage
            {
                ApplicationId = entry.ApplicationId,
                LaunchCount = entry.LaunchCount,
                LastUsed = entry.LastUsed,
                ForegroundDuration = entry.ForegroundDuration,
                IsLaunchable = entry.IsLaunchable
            };
            foreach ((int hour, int count) in entry.HourBuckets) result.HourBuckets[hour] = count;
            foreach ((string context, int count) in entry.ContextTransitions) result.ContextTransitions[context] = count;
            return result;
        }
    }
}
