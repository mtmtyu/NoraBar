using System.Collections.Concurrent;
using System.Windows.Threading;
using NoraBar.Services;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

[Collection(WpfApplicationCollection.Name)]
public sealed class MusicViewModelTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void SuccessfulRequest_CompletingAfterLyricsAreDisabled_DoesNotUpdateLyrics()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> request = service.EnqueueRequest();
            Task update = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            viewModel.ShowLyrics = false;
            request.SetResult(Success("stale line"));
            PumpUntil(update, cancellationToken);
            DrainDispatcher(cancellationToken);
            AssertLyricsCleared(viewModel);
        });
    }

    [Fact]
    public void ErrorRequest_CompletingAfterLyricsAreDisabled_DoesNotUpdateMessage()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> request = service.EnqueueRequest();
            Task update = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            viewModel.ShowLyrics = false;
            request.SetResult(new LyricsResult { Error = LyricsResultError.NetworkError });
            PumpUntil(update, cancellationToken);
            DrainDispatcher(cancellationToken);
            AssertLyricsCleared(viewModel);
        });
    }

    [Fact]
    public void PreviousTrack_CompletingAfterTrackChange_DoesNotOverwriteCurrentTrack()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> trackA = service.EnqueueRequest();
            TaskCompletionSource<LyricsResult> trackB = service.EnqueueRequest();
            Task updateA = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            Task updateB = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track B"));
            trackA.SetResult(Success("Track A line"));
            trackB.SetResult(Success("Track B line"));
            PumpUntil(Task.WhenAll(updateA, updateB), cancellationToken);
            DrainDispatcher(cancellationToken);
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(3));
            DrainDispatcher(cancellationToken);
            Assert.Equal("Track B", viewModel.Title);
            Assert.Equal("Track B line", viewModel.CurrentLyric);
            Assert.Single(viewModel.LyricsList);
            Assert.Equal("Track B line", viewModel.LyricsList[0].Text);
        });
    }

    [Fact]
    public void CachedResult_QueuedBeforeTrackChange_DoesNotOverwriteCurrentTrack()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> initialA = service.EnqueueRequest();
            Task firstUpdate = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            initialA.SetResult(Success("cached Track A line"));
            PumpUntil(firstUpdate, cancellationToken);
            DrainDispatcher(cancellationToken);

            TaskCompletionSource<LyricsResult> trackB = service.EnqueueRequest();
            Task switchToB = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track B"));
            Task cachedA = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            TaskCompletionSource<LyricsResult> finalB = service.EnqueueRequest();
            Task switchBackToB = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track B"));
            trackB.SetResult(Success("obsolete Track B line"));
            finalB.SetResult(Success("current Track B line"));
            PumpUntil(Task.WhenAll(switchToB, cachedA, switchBackToB), cancellationToken);
            DrainDispatcher(cancellationToken);
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(3));
            DrainDispatcher(cancellationToken);
            Assert.Equal("Track B", viewModel.Title);
            Assert.Equal("current Track B line", viewModel.CurrentLyric);
        });
    }

    [Fact]
    public void QueuedResultCallback_BecomingStaleBeforeExecution_DoesNotUpdateLyrics()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> request = service.EnqueueRequest();
            Task update = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            request.SetResult(Success("queued stale line"));
            PumpUntil(update, cancellationToken);
            viewModel.ShowLyrics = false;
            DrainDispatcher(cancellationToken);
            AssertLyricsCleared(viewModel);
        });
    }

    [Fact]
    public void NewerRequest_CompletingBeforeOlderRequest_RemainsCurrent()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> older = service.EnqueueRequest();
            TaskCompletionSource<LyricsResult> newer = service.EnqueueRequest();
            Task olderUpdate = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            Task newerUpdate = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track B"));
            newer.SetResult(Success("newer line"));
            PumpUntil(newerUpdate, cancellationToken);
            DrainDispatcher(cancellationToken);
            older.SetResult(Success("older line"));
            PumpUntil(olderUpdate, cancellationToken);
            DrainDispatcher(cancellationToken);
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(3));
            DrainDispatcher(cancellationToken);
            Assert.Equal("Track B", viewModel.Title);
            Assert.Equal("newer line", viewModel.CurrentLyric);
        });
    }

    [Fact]
    public void DisablingLyrics_ClearsAllLyricsUiState()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            CompleteTrack(viewModel, service, "Track A", "line", cancellationToken);
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(3));
            DrainDispatcher(cancellationToken);
            Assert.Equal("line", viewModel.CurrentLyric);
            viewModel.ShowLyrics = false;
            AssertLyricsCleared(viewModel);
        });
    }

    [Fact]
    public void ReEnablingLyrics_AllowsNewRequestToUpdateUi()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> stale = service.EnqueueRequest();
            Task initialUpdate = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            viewModel.ShowLyrics = false;
            stale.SetResult(Success("stale line"));
            PumpUntil(initialUpdate, cancellationToken);

            TaskCompletionSource<LyricsResult> current = service.EnqueueRequest();
            viewModel.ShowLyrics = true;
            current.SetResult(Success("fresh line"));
            PumpUntil(() => viewModel.LyricsList.Count == 1, cancellationToken);
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(3));
            DrainDispatcher(cancellationToken);
            Assert.Equal("fresh line", viewModel.CurrentLyric);
            Assert.Single(viewModel.LyricsList);
        });
    }

    [Fact]
    public void LoadingCallback_BecomingStaleBeforeExecution_DoesNotDisplayLoadingState()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            TaskCompletionSource<LyricsResult> request = service.EnqueueRequest();
            Task update = viewModel.ProcessMediaInfoChangedAsync(MediaInfo("Track A"));
            viewModel.ShowLyrics = false;
            DrainDispatcher(cancellationToken);
            Assert.Equal(string.Empty, viewModel.CurrentLyric);
            request.SetResult(Success("ignored"));
            PumpUntil(update, cancellationToken);
        });
    }

    [Fact]
    public void CurrentLyricCallback_BecomingStaleOrDisabled_DoesNotChangeSelection()
    {
        RunLyricsTest((viewModel, service, cancellationToken) =>
        {
            CompleteTrack(viewModel, service, "Track A", "first", cancellationToken, "second");
            viewModel.ProcessMediaTimelineChanged(TimeSpan.FromSeconds(6), TimeSpan.FromMinutes(3));
            viewModel.ShowLyrics = false;
            DrainDispatcher(cancellationToken);
            AssertLyricsCleared(viewModel);
        });
    }

    private static void RunLyricsTest(Action<MusicViewModel, ControlledLyricsService, CancellationToken> action)
    {
        StaTestRunner.Run(cancellationToken => RunWithApplication(() =>
        {
            var service = new ControlledLyricsService();
            var viewModel = new MusicViewModel(service, static () => Task.CompletedTask, false, true);
            try
            {
                action(viewModel, service, cancellationToken);
            }
            finally
            {
                viewModel.Cleanup();
            }
        }), StaTimeout);
    }

    private static void CompleteTrack(
        MusicViewModel viewModel,
        ControlledLyricsService service,
        string title,
        string firstLine,
        CancellationToken cancellationToken,
        string? secondLine = null)
    {
        TaskCompletionSource<LyricsResult> request = service.EnqueueRequest();
        Task update = viewModel.ProcessMediaInfoChangedAsync(MediaInfo(title));
        request.SetResult(Success(firstLine, secondLine));
        PumpUntil(update, cancellationToken);
        DrainDispatcher(cancellationToken);
    }

    private static MediaInfoChangedEventArgs MediaInfo(string title) => new()
    {
        Title = title,
        Artist = "Artist",
        AlbumTitle = "Album"
    };

    private static LyricsResult Success(string firstLine, string? secondLine = null)
    {
        var lyrics = new List<LyricLine>
        {
            new() { StartTime = TimeSpan.FromSeconds(1), Text = firstLine }
        };
        if (secondLine is not null)
        {
            lyrics.Add(new LyricLine { StartTime = TimeSpan.FromSeconds(5), Text = secondLine });
        }

        return new LyricsResult { Lyrics = lyrics, Error = LyricsResultError.None };
    }

    private static void AssertLyricsCleared(MusicViewModel viewModel)
    {
        Assert.Empty(viewModel.LyricsList);
        Assert.Equal(-1, viewModel.CurrentLyricIndex);
        Assert.Equal(string.Empty, viewModel.CurrentLyric);
    }

    private static void PumpUntil(Task task, CancellationToken cancellationToken)
    {
        PumpUntil(() => task.IsCompleted, cancellationToken);
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> condition, CancellationToken cancellationToken)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static void RunWithApplication(Action action)
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        bool ownsApplication = application is null;
        application ??= new System.Windows.Application();
        try
        {
            action();
        }
        finally
        {
            if (ownsApplication)
            {
                application.Shutdown();
            }
        }
    }

    private static void DrainDispatcher(CancellationToken cancellationToken)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        using CancellationTokenRegistration registration =
            cancellationToken.Register(() => dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() => frame.Continue = false)));
        dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class ControlledLyricsService : ILyricsService
    {
        private readonly ConcurrentQueue<TaskCompletionSource<LyricsResult>> _responses = new();

        internal TaskCompletionSource<LyricsResult> EnqueueRequest()
        {
            var response = new TaskCompletionSource<LyricsResult>();
            _responses.Enqueue(response);
            return response;
        }

        public Task<LyricsResult> GetLyricsAsync(
            string trackName,
            string artistName,
            string? albumName,
            double durationInSeconds)
        {
            Assert.True(_responses.TryDequeue(out TaskCompletionSource<LyricsResult>? response));
            return response.Task;
        }
    }
}
