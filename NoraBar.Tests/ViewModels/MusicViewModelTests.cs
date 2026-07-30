using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

[Collection(WpfApplicationCollection.Name)]
public sealed class MusicViewModelTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void UpdateCurrentLyric_WhenLyricsChangeBeforeDispatcherExecution_RemainsSafe()
    {
        StaTestRunner.Run(cancellationToken =>
        {
            RunWithApplication(() =>
            {
                VerifyClearedLyricsRemainSafe(cancellationToken);
                VerifyShrunkLyricsRemainSafe(cancellationToken);
            });
        }, StaTimeout);
    }

    [Fact]
    public void UpdateCurrentLyric_WhenTrackChangesBeforeDispatcherExecution_IgnoresStaleResult()
    {
        StaTestRunner.Run(cancellationToken =>
        {
            RunWithApplication(() =>
            {
                var viewModel = new MusicViewModel();
                try
                {
                    viewModel.ShowLyrics = true;
                    (FieldInfo lyricsField, MethodInfo updateMethod) = GetLyricsMembers();
                    FieldInfo requestIdField = Assert.IsAssignableFrom<FieldInfo>(
                        typeof(MusicViewModel).GetField(
                            "_lyricsRequestId",
                            BindingFlags.NonPublic | BindingFlags.Instance));
                    lyricsField.SetValue(
                        viewModel,
                        new List<LyricLine>
                        {
                            new() { StartTime = TimeSpan.FromSeconds(1), Text = "Track A line 1" },
                            new() { StartTime = TimeSpan.FromSeconds(5), Text = "Track A line 2" }
                        });

                    updateMethod.Invoke(viewModel, [TimeSpan.FromSeconds(6)]);
                    requestIdField.SetValue(viewModel, 1);
                    lyricsField.SetValue(
                        viewModel,
                        new List<LyricLine>
                        {
                            new() { StartTime = TimeSpan.FromSeconds(1), Text = "Track B line 1" },
                            new() { StartTime = TimeSpan.FromSeconds(5), Text = "Track B line 2" }
                        });
                    viewModel.CurrentLyric = "Track B current";

                    DrainDispatcher(cancellationToken);

                    Assert.Equal("Track B current", viewModel.CurrentLyric);
                    Assert.Equal(-1, viewModel.CurrentLyricIndex);
                }
                finally
                {
                    viewModel.Cleanup();
                }
            });
        }, StaTimeout);
    }

    private static void VerifyClearedLyricsRemainSafe(
        CancellationToken cancellationToken)
    {
        var viewModel = new MusicViewModel();
        try
        {
            (FieldInfo lyricsField, MethodInfo updateMethod) = GetLyricsMembers();
            lyricsField.SetValue(
                viewModel,
                new List<LyricLine>
                {
                    new() { StartTime = TimeSpan.FromSeconds(1), Text = "Line 1" },
                    new() { StartTime = TimeSpan.FromSeconds(5), Text = "Line 2" }
                });

            updateMethod.Invoke(viewModel, [TimeSpan.FromSeconds(2)]);
            lyricsField.SetValue(viewModel, null);

            DrainDispatcher(cancellationToken);
        }
        finally
        {
            viewModel.Cleanup();
        }
    }

    private static void VerifyShrunkLyricsRemainSafe(
        CancellationToken cancellationToken)
    {
        var viewModel = new MusicViewModel();
        try
        {
            (FieldInfo lyricsField, MethodInfo updateMethod) = GetLyricsMembers();
            lyricsField.SetValue(
                viewModel,
                new List<LyricLine>
                {
                    new() { StartTime = TimeSpan.FromSeconds(1), Text = "Line 1" },
                    new() { StartTime = TimeSpan.FromSeconds(5), Text = "Line 2" },
                    new() { StartTime = TimeSpan.FromSeconds(10), Text = "Line 3" }
                });

            updateMethod.Invoke(viewModel, [TimeSpan.FromSeconds(12)]);
            lyricsField.SetValue(
                viewModel,
                new List<LyricLine>
                {
                    new() { StartTime = TimeSpan.FromSeconds(1), Text = "Short Line 1" }
                });

            DrainDispatcher(cancellationToken);
        }
        finally
        {
            viewModel.Cleanup();
        }
    }

    private static (FieldInfo LyricsField, MethodInfo UpdateMethod) GetLyricsMembers()
    {
        FieldInfo lyricsField = Assert.IsAssignableFrom<FieldInfo>(
            typeof(MusicViewModel).GetField(
                "_currentLyrics",
                BindingFlags.NonPublic | BindingFlags.Instance));
        MethodInfo updateMethod = Assert.IsAssignableFrom<MethodInfo>(
            typeof(MusicViewModel).GetMethod(
                "UpdateCurrentLyric",
                BindingFlags.NonPublic | BindingFlags.Instance));
        return (lyricsField, updateMethod);
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
        using CancellationTokenRegistration cancellationRegistration =
            cancellationToken.Register(() => dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() => frame.Continue = false)));
        dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
