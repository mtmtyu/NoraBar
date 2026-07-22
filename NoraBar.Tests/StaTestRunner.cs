using System.Runtime.ExceptionServices;

namespace NoraBar.Tests;

internal static class StaTestRunner
{
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
