using System.Runtime.ExceptionServices;

namespace NoraBar.Tests;

internal static class StaTestRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TimeoutCleanupJoinTimeout = TimeSpan.FromSeconds(5);

    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Run(_ => action(), DefaultTimeout);
    }

    internal static void Run(
        Action<CancellationToken> action,
        TimeSpan timeout,
        Action? timeoutCleanup = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var cancellation = new CancellationTokenSource();
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action(cancellation.Token);
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(timeout))
        {
            var timeoutException = new TimeoutException(
                $"STA test action did not complete within {timeout}.");
            List<Exception> additionalFailures = [];
            try
            {
                cancellation.Cancel();
            }
            catch (Exception cancellationException)
            {
                additionalFailures.Add(cancellationException);
            }

            if (timeoutCleanup is not null)
            {
                try
                {
                    timeoutCleanup();
                }
                catch (Exception cleanupException)
                {
                    additionalFailures.Add(cleanupException);
                }
            }

            bool actionExited = thread.Join(TimeoutCleanupJoinTimeout);
            if (!actionExited)
            {
                additionalFailures.Add(new TimeoutException(
                    $"Timed-out STA test action did not exit within {TimeoutCleanupJoinTimeout} after cleanup was signaled."));
            }
            else if (exception is not null)
            {
                additionalFailures.Add(exception);
            }

            if (actionExited)
            {
                cancellation.Dispose();
            }

            if (additionalFailures.Count > 0)
            {
                throw new AggregateException(
                    "STA test action timed out and cleanup also failed.",
                    [timeoutException, .. additionalFailures]);
            }

            throw timeoutException;
        }

        cancellation.Dispose();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
