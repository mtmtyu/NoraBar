namespace NoraBar.Services;

internal sealed class StartupFailureReport
{
    private const string StartupTracePrefix = "NoraBar startup failed:";
    private const string CleanupTracePrefix = "NoraBar startup cleanup failed:";

    private StartupFailureReport(
        Exception startupException,
        IReadOnlyList<Exception> cleanupExceptions)
    {
        StartupException = startupException;
        CleanupExceptions = cleanupExceptions;
    }

    internal Exception StartupException { get; }

    internal IReadOnlyList<Exception> CleanupExceptions { get; }

    internal string UserMessage =>
        $"NoraBarの起動に失敗しました。{Environment.NewLine}{StartupException.Message}";

    internal static StartupFailureReport Create(
        Exception startupException,
        IEnumerable<Exception> cleanupExceptions)
    {
        ArgumentNullException.ThrowIfNull(startupException);
        ArgumentNullException.ThrowIfNull(cleanupExceptions);

        Exception[] capturedCleanupExceptions = cleanupExceptions.ToArray();
        return new StartupFailureReport(
            startupException,
            Array.AsReadOnly(capturedCleanupExceptions));
    }

    internal void WriteTrace(Action<string> writeError)
    {
        ArgumentNullException.ThrowIfNull(writeError);

        writeError($"{StartupTracePrefix}{Environment.NewLine}{StartupException}");
        foreach (Exception cleanupException in CleanupExceptions)
        {
            writeError($"{CleanupTracePrefix}{Environment.NewLine}{cleanupException}");
        }
    }
}
