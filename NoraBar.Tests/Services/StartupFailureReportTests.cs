using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public sealed class StartupFailureReportTests
{
    [Fact]
    public void Create_PreservesStartupAndEveryCleanupExceptionForTracing()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var routerFailure = new InvalidOperationException("router shutdown failed");
        var registryFailure = new InvalidOperationException("registry dispose failed");
        var cleanupFailures = new List<Exception>
        {
            routerFailure,
            registryFailure,
        };

        StartupFailureReport report = StartupFailureReport.Create(
            startupFailure,
            cleanupFailures);
        cleanupFailures.Clear();

        Assert.Same(startupFailure, report.StartupException);
        Assert.Equal(
            new Exception[] { routerFailure, registryFailure },
            report.CleanupExceptions);

        var traceMessages = new List<string>();
        report.WriteTrace(traceMessages.Add);

        Assert.Equal(3, traceMessages.Count);
        Assert.Contains(startupFailure.ToString(), traceMessages[0], StringComparison.Ordinal);
        Assert.Contains(routerFailure.ToString(), traceMessages[1], StringComparison.Ordinal);
        Assert.Contains(registryFailure.ToString(), traceMessages[2], StringComparison.Ordinal);
    }

    [Fact]
    public void UserMessage_RemainsConciseWhenCleanupFails()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var cleanupFailure = new InvalidOperationException(
            "long cleanup diagnostics that belong only in trace output");

        StartupFailureReport report = StartupFailureReport.Create(
            startupFailure,
            new[] { cleanupFailure });

        Assert.Contains(startupFailure.Message, report.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(cleanupFailure.Message, report.UserMessage, StringComparison.Ordinal);
    }
}
