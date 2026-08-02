namespace NoraBar.Services;

internal readonly record struct MediaUpdateRequest(long Version);

internal sealed class MediaUpdateRequestTracker
{
    private long _version;

    internal MediaUpdateRequest Capture() =>
        new(Volatile.Read(ref _version));

    internal void InvalidatePendingRequests() =>
        Interlocked.Increment(ref _version);

    internal bool IsCurrent(MediaUpdateRequest request) =>
        request.Version == Volatile.Read(ref _version);
}
