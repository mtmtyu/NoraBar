using System;

namespace NoraBar.Services
{
    internal readonly record struct PlaybackSnapshot(bool IsPlaying, TimeSpan Position);

    internal sealed class PlaybackStateCoordinator
    {
        private static readonly TimeSpan RequestedStateTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PositionMatchTolerance = TimeSpan.FromSeconds(1);
        private readonly object _syncRoot = new();
        private bool? _requestedIsPlaying;
        private TimeSpan _requestedPosition;
        private DateTimeOffset _requestedAt;
        private bool? _lastPublishedIsPlaying;

        public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

        public PlaybackSnapshot CreateSnapshot(
            bool reportedIsPlaying,
            TimeSpan reportedPosition,
            DateTimeOffset lastUpdatedTime,
            TimeSpan endTime,
            DateTimeOffset now)
        {
            PlaybackSnapshot snapshot;
            bool shouldPublish;

            lock (_syncRoot)
            {
                if (_requestedIsPlaying.HasValue)
                {
                    TimeSpan requestAge = GetPositiveElapsed(now, _requestedAt);
                    TimeSpan expectedPosition = _requestedPosition;
                    if (_requestedIsPlaying.Value)
                    {
                        expectedPosition += requestAge;
                    }

                    bool reportedStateAndPositionMatch =
                        reportedIsPlaying == _requestedIsPlaying.Value &&
                        IsPositionClose(reportedPosition, expectedPosition);
                    if (reportedStateAndPositionMatch || requestAge >= RequestedStateTimeout)
                    {
                        _requestedIsPlaying = null;
                    }
                }

                bool isPlaying = _requestedIsPlaying ?? reportedIsPlaying;
                TimeSpan position;

                if (_requestedIsPlaying.HasValue)
                {
                    position = _requestedPosition;
                    if (isPlaying)
                    {
                        position += GetPositiveElapsed(now, _requestedAt);
                    }
                }
                else
                {
                    position = reportedPosition;
                    if (isPlaying && lastUpdatedTime != default)
                    {
                        position += GetPositiveElapsed(now, lastUpdatedTime.ToUniversalTime());
                    }
                }

                position = ClampPosition(position, endTime);
                shouldPublish = MarkStateForPublication(isPlaying);
                snapshot = new PlaybackSnapshot(isPlaying, position);
            }

            PublishStateChanged(snapshot.IsPlaying, shouldPublish);
            return snapshot;
        }

        public PlaybackSnapshot ApplyRequestedState(
            bool isPlaying,
            TimeSpan position,
            TimeSpan endTime,
            DateTimeOffset now)
        {
            PlaybackSnapshot snapshot;
            bool shouldPublish;

            lock (_syncRoot)
            {
                _requestedIsPlaying = isPlaying;
                _requestedPosition = ClampPosition(position, endTime);
                _requestedAt = now;
                shouldPublish = MarkStateForPublication(isPlaying);
                snapshot = new PlaybackSnapshot(isPlaying, _requestedPosition);
            }

            PublishStateChanged(snapshot.IsPlaying, shouldPublish);
            return snapshot;
        }

        public void ApplyAuthoritativeState(bool isPlaying)
        {
            bool shouldPublish;

            lock (_syncRoot)
            {
                shouldPublish = MarkStateForPublication(isPlaying);
            }

            PublishStateChanged(isPlaying, shouldPublish);
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                _requestedIsPlaying = null;
                _requestedPosition = TimeSpan.Zero;
                _requestedAt = default;
                _lastPublishedIsPlaying = null;
            }
        }

        private bool MarkStateForPublication(bool isPlaying)
        {
            if (_lastPublishedIsPlaying == isPlaying)
            {
                return false;
            }

            _lastPublishedIsPlaying = isPlaying;
            return true;
        }

        private void PublishStateChanged(bool isPlaying, bool shouldPublish)
        {
            if (!shouldPublish)
            {
                return;
            }

            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                IsPlaying = isPlaying
            });
        }

        private static bool IsPositionClose(TimeSpan reportedPosition, TimeSpan expectedPosition)
        {
            return (reportedPosition - expectedPosition).Duration() <= PositionMatchTolerance;
        }

        private static TimeSpan GetPositiveElapsed(DateTimeOffset now, DateTimeOffset then)
        {
            TimeSpan elapsed = now - then;
            return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
        }

        private static TimeSpan ClampPosition(TimeSpan position, TimeSpan endTime)
        {
            if (position < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (endTime > TimeSpan.Zero && position > endTime)
            {
                return endTime;
            }

            return position;
        }
    }
}