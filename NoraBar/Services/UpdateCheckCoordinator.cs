using System;
using System.Threading;
using System.Threading.Tasks;

namespace NoraBar.Services
{
    internal readonly record struct UpdateCheckResult(string? TagName, string? ReleaseUrl);

    internal sealed class UpdateCheckCoordinator
    {
        private readonly object _syncRoot = new();
        private readonly Func<Task<UpdateCheckResult>> _requestUpdateAsync;
        private Task<UpdateCheckResult>? _inFlightRequest;

        public UpdateCheckCoordinator(Func<Task<UpdateCheckResult>> requestUpdateAsync)
        {
            _requestUpdateAsync = requestUpdateAsync;
        }

        public Task<UpdateCheckResult> CheckAsync()
        {
            lock (_syncRoot)
            {
                if (_inFlightRequest is not null)
                {
                    return _inFlightRequest;
                }

                Task<UpdateCheckResult> request = _requestUpdateAsync();
                _inFlightRequest = request;
                _ = request.ContinueWith(
                    completedRequest => ClearCompletedRequest(completedRequest),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return request;
            }
        }

        private void ClearCompletedRequest(Task<UpdateCheckResult> completedRequest)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_inFlightRequest, completedRequest))
                {
                    _inFlightRequest = null;
                }
            }
        }
    }
}
