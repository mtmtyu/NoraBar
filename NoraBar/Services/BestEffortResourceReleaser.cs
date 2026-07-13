namespace NoraBar.Services;

internal static class BestEffortResourceReleaser
{
    internal static void ReleaseAll(params Action[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var exceptions = new List<Exception>();

        foreach (Action operation in operations)
        {
            ArgumentNullException.ThrowIfNull(operation);

            try
            {
                operation();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                "シェル資源の解放中にエラーが発生しました。",
                exceptions);
        }
    }
}
