using System.Runtime.ExceptionServices;

namespace NoraBar.Tests;

internal static class StaTestRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
        if (!thread.Join(DefaultTimeout))
        {
            throw new TimeoutException(
                $"STA test action did not complete within {DefaultTimeout}.");
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
