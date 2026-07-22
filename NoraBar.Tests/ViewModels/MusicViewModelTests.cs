using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Threading;
using NoraBar.Models;
using NoraBar.Services;
using NoraBar.ViewModels;
using Xunit;

namespace NoraBar.Tests.ViewModels;

public sealed class MusicViewModelTests
{
    [Fact]
    public void UpdateCurrentLyric_WhenLyricsClearedBeforeDispatcherExecution_DoesNotThrowNullReferenceException()
    {
        StaTestRunner.Run(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application();
            }

            var viewModel = new MusicViewModel();

            FieldInfo? currentLyricsField = typeof(MusicViewModel).GetField("_currentLyrics", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo? updateCurrentLyricMethod = typeof(MusicViewModel).GetMethod("UpdateCurrentLyric", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(currentLyricsField);
            Assert.NotNull(updateCurrentLyricMethod);

            var lyricsList = new List<LyricLine>
            {
                new LyricLine { StartTime = TimeSpan.FromSeconds(1), Text = "Line 1" },
                new LyricLine { StartTime = TimeSpan.FromSeconds(5), Text = "Line 2" }
            };

            currentLyricsField.SetValue(viewModel, lyricsList);

            // Trigger UpdateCurrentLyric which queues Dispatcher.InvokeAsync
            updateCurrentLyricMethod.Invoke(viewModel, [TimeSpan.FromSeconds(2)]);

            // Clear _currentLyrics to null before the queued Dispatcher action runs
            currentLyricsField.SetValue(viewModel, null);

            // Process Dispatcher queue frame to execute the queued action
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        });
    }

    [Fact]
    public void UpdateCurrentLyric_WhenLyricsListShrinksBeforeDispatcherExecution_DoesNotThrowArgumentOutOfRangeExceptionOrNullReference()
    {
        StaTestRunner.Run(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                _ = new System.Windows.Application();
            }

            var viewModel = new MusicViewModel();

            FieldInfo? currentLyricsField = typeof(MusicViewModel).GetField("_currentLyrics", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo? updateCurrentLyricMethod = typeof(MusicViewModel).GetMethod("UpdateCurrentLyric", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(currentLyricsField);
            Assert.NotNull(updateCurrentLyricMethod);

            var initialLyricsList = new List<LyricLine>
            {
                new LyricLine { StartTime = TimeSpan.FromSeconds(1), Text = "Line 1" },
                new LyricLine { StartTime = TimeSpan.FromSeconds(5), Text = "Line 2" },
                new LyricLine { StartTime = TimeSpan.FromSeconds(10), Text = "Line 3" }
            };

            currentLyricsField.SetValue(viewModel, initialLyricsList);

            // Trigger UpdateCurrentLyric for position 12s -> newIndex = 2
            updateCurrentLyricMethod.Invoke(viewModel, [TimeSpan.FromSeconds(12)]);

            // Replace _currentLyrics with a shorter list (count = 1) before Dispatcher runs
            var shorterLyricsList = new List<LyricLine>
            {
                new LyricLine { StartTime = TimeSpan.FromSeconds(1), Text = "Short Line 1" }
            };
            currentLyricsField.SetValue(viewModel, shorterLyricsList);

            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        });
    }
}
