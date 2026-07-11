using System;
using System.Threading;
using System.Threading.Tasks;

namespace NoraBar.Services
{
    internal sealed class VisualizerRestartCoordinator
    {
        private readonly SemaphoreSlim _restartLock = new(1, 1);

        public async Task<bool> RestartAsync(Func<Task<bool>> restartAsync)
        {
            await _restartLock.WaitAsync();
            try
            {
                return await restartAsync();
            }
            finally
            {
                _restartLock.Release();
            }
        }
    }
}
