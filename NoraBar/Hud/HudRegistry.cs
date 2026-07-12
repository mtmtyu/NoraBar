using System.Runtime.ExceptionServices;

namespace NoraBar.Hud;

/// <summary>
/// 利用可能なHUDモジュールを登録順に保持し、IDによる検索と一括破棄を提供します。
/// </summary>
public sealed class HudRegistry : IAsyncDisposable
{
    private readonly object _syncRoot = new();
    private readonly List<IHudModule> _modules = [];
    private readonly Dictionary<string, IHudModule> _modulesById = new(StringComparer.Ordinal);
    private bool _disposeStarted;
    private Task? _disposeTask;

    /// <summary>
    /// 登録されたHUDモジュールを登録順で取得します。
    /// </summary>
    public IReadOnlyList<IHudModule> Modules
    {
        get
        {
            lock (_syncRoot)
            {
                return _modules.ToArray();
            }
        }
    }

    /// <summary>
    /// HUDモジュールを登録します。
    /// </summary>
    /// <param name="module">登録するHUDモジュール。</param>
    /// <exception cref="ArgumentNullException"><paramref name="module"/>が<c>null</c>の場合。</exception>
    /// <exception cref="ArgumentException">モジュールIDが無効、または同一IDが登録済みの場合。</exception>
    /// <exception cref="ObjectDisposedException">レジストリの破棄が開始済みの場合。</exception>
    public void Register(IHudModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        string id = module.Id;

        lock (_syncRoot)
        {
            if (_disposeStarted)
            {
                throw new ObjectDisposedException(nameof(HudRegistry));
            }

            if (!IsValidId(id))
            {
                throw new ArgumentException("HUD module ID must not be blank, padded, or contain control characters.", nameof(module));
            }

            if (!_modulesById.TryAdd(id, module))
            {
                throw new ArgumentException($"A HUD module with ID '{id}' is already registered.", nameof(module));
            }

            _modules.Add(module);
        }
    }

    /// <summary>
    /// 指定されたIDとOrdinalで一致するHUDモジュールを検索します。
    /// </summary>
    /// <param name="id">検索するHUDモジュールID。</param>
    /// <param name="module">見つかったHUDモジュール。見つからない場合は<c>null</c>です。</param>
    /// <returns>一致するモジュールが見つかった場合は<c>true</c>、それ以外は<c>false</c>。</returns>
    public bool TryGet(string id, out IHudModule? module)
    {
        lock (_syncRoot)
        {
            return _modulesById.TryGetValue(id, out module);
        }
    }

    /// <summary>
    /// 登録された全HUDモジュールを登録順に一度だけ破棄します。
    /// </summary>
    /// <returns>破棄処理を表す非同期操作。</returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?> completionSource;
        IHudModule[] modules;

        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _disposeStarted = true;
            completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completionSource.Task;
            modules = _modules.ToArray();
        }

        _ = DisposeModulesAndSignalCompletionAsync(modules, completionSource);
        return new ValueTask(completionSource.Task);
    }

    private static async Task DisposeModulesAndSignalCompletionAsync(
        IReadOnlyList<IHudModule> modules,
        TaskCompletionSource<object?> completionSource)
    {
        try
        {
            await DisposeModulesAsync(modules);
            completionSource.SetResult(null);
        }
        catch (Exception exception)
        {
            completionSource.SetException(exception);
        }
    }

    private static async Task DisposeModulesAsync(IReadOnlyList<IHudModule> modules)
    {
        List<Exception> exceptions = [];

        foreach (IHudModule module in modules)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException("Multiple HUD modules failed to dispose.", exceptions);
        }
    }

    private static bool IsValidId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in id)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
