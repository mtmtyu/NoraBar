namespace NoraBar.Hud;

/// <summary>
/// HUDモジュールの選択、表示状態、ライフサイクルを直列化して管理します。
/// </summary>
public sealed class HudRouter
{
    private readonly HudRegistry _registry;
    private readonly string _configuredDefaultHudId;
    private readonly string[] _configuredEnabledHudModuleIds;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly HashSet<IHudModule> _initializedModules = [];
    private List<string> _enabledHudModuleIds;
    private string _effectiveDefaultHudId;
    private string? _currentHudId;
    private IHudModule? _currentModule;
    private HudPresentationState _presentationState = HudPresentationState.Collapsed;
    private bool _isInitialized;
    private bool _isShuttingDown;
    private bool _isTransitioning;
    private bool _hasPendingPresentationInvalidation;

    public HudRouter(
        HudRegistry registry,
        string configuredDefaultHudId,
        IEnumerable<string> configuredEnabledHudModuleIds)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuredDefaultHudId);
        ArgumentNullException.ThrowIfNull(configuredEnabledHudModuleIds);

        _registry = registry;
        _configuredDefaultHudId = configuredDefaultHudId;
        _configuredEnabledHudModuleIds = configuredEnabledHudModuleIds.ToArray();
        (_enabledHudModuleIds, _effectiveDefaultHudId) = ResolveRuntimeConfiguration(
            _configuredEnabledHudModuleIds);
    }

    public event EventHandler? StateChanged;

    public event EventHandler? PresentationChanged;

    public IHudModule? CurrentModule
    {
        get
        {
            lock (_stateLock)
            {
                return _currentModule;
            }
        }
    }

    public string? CurrentHudId
    {
        get
        {
            lock (_stateLock)
            {
                return _currentHudId;
            }
        }
    }

    public HudPresentationState PresentationState
    {
        get
        {
            lock (_stateLock)
            {
                return _presentationState;
            }
        }
    }

    public string EffectiveDefaultHudId
    {
        get
        {
            lock (_stateLock)
            {
                return _effectiveDefaultHudId;
            }
        }
    }

    public IReadOnlyList<string> EnabledHudModuleIds
    {
        get
        {
            lock (_stateLock)
            {
                return _enabledHudModuleIds.ToArray();
            }
        }
    }

    public HudRouterSnapshot GetSnapshot()
    {
        lock (_stateLock)
        {
            return new HudRouterSnapshot(
                _currentHudId,
                _currentModule,
                _presentationState,
                _isInitialized,
                _isShuttingDown);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            lock (_stateLock)
            {
                if (_isInitialized || _isShuttingDown)
                {
                    return;
                }

                _isTransitioning = true;
                _hasPendingPresentationInvalidation = false;
            }

            IHudModule module = GetRegisteredModule(_effectiveDefaultHudId);
            bool subscriptionAttempted = false;
            try
            {
                await InitializeModuleOnceAsync(module, cancellationToken);
                subscriptionAttempted = true;
                Subscribe(module);
                await module.ActivateAsync(cancellationToken);
            }
            catch (Exception initializationException)
            {
                var cleanupExceptions = new List<Exception>();
                if (subscriptionAttempted)
                {
                    CaptureFailure(() => Unsubscribe(module), cleanupExceptions);
                    await CaptureFailureAsync(
                        () => module.DeactivateAsync(CancellationToken.None),
                        cleanupExceptions);
                }

                lock (_stateLock)
                {
                    _isTransitioning = false;
                    _hasPendingPresentationInvalidation = false;
                }

                if (cleanupExceptions.Count > 0)
                {
                    throw new HudNavigationException(module.Id, initializationException, cleanupExceptions);
                }

                throw;
            }

            bool notifyPresentation;
            lock (_stateLock)
            {
                _currentHudId = module.Id;
                _currentModule = module;
                _presentationState = HudPresentationState.Collapsed;
                _isInitialized = true;
                _isTransitioning = false;
                notifyPresentation = TakePendingPresentationInvalidation();
            }

            OnStateChanged();
            if (notifyPresentation)
            {
                OnPresentationChanged();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task NavigateToAsync(string hudId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hudId);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCanNavigate();
            List<string> enabledIds;
            string effectiveDefaultHudId;
            IHudModule? currentModule;
            lock (_stateLock)
            {
                enabledIds = [.. _enabledHudModuleIds];
                effectiveDefaultHudId = _effectiveDefaultHudId;
                currentModule = _currentModule;
            }

            string targetHudId = ResolveNavigationTarget(hudId, enabledIds, effectiveDefaultHudId);
            IHudModule targetModule = GetRegisteredModule(targetHudId);
            if (ReferenceEquals(targetModule, currentModule))
            {
                return;
            }

            await TransitionToAsync(targetModule, cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DisableAsync(string hudId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hudId);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCanNavigate();
            List<string> currentEnabledIds;
            string? currentHudId;
            IHudModule? currentModule;
            lock (_stateLock)
            {
                currentEnabledIds = [.. _enabledHudModuleIds];
                currentHudId = _currentHudId;
                currentModule = _currentModule;
            }

            List<string> updatedIds = currentEnabledIds
                .Where(id => !string.Equals(id, hudId, StringComparison.Ordinal))
                .ToList();
            EnsureRuntimeModuleAvailable(updatedIds);
            string updatedDefault = ResolveEffectiveDefault(updatedIds);

            if (string.Equals(currentHudId, hudId, StringComparison.Ordinal))
            {
                string fallbackId = ResolveNavigationTarget(updatedDefault, updatedIds, updatedDefault);
                IHudModule fallback = GetRegisteredModule(fallbackId);
                if (!ReferenceEquals(fallback, currentModule))
                {
                    await TransitionToAsync(fallback, cancellationToken);
                }
            }

            bool changed;
            lock (_stateLock)
            {
                changed = !_enabledHudModuleIds.SequenceEqual(updatedIds, StringComparer.Ordinal)
                    || !string.Equals(_effectiveDefaultHudId, updatedDefault, StringComparison.Ordinal);
                _enabledHudModuleIds = updatedIds;
                _effectiveDefaultHudId = updatedDefault;
            }

            if (changed)
            {
                OnStateChanged();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public bool SetPresentationState(HudPresentationState presentationState)
    {
        lock (_stateLock)
        {
            if (!_isInitialized || _isShuttingDown || _isTransitioning
                || _presentationState == presentationState)
            {
                return false;
            }

            _presentationState = presentationState;
        }

        OnStateChanged();
        return true;
    }

    public bool CollapseFromPointerLeave()
    {
        lock (_stateLock)
        {
            if (!_isInitialized || _isShuttingDown || _isTransitioning
                || _presentationState is HudPresentationState.Collapsed or HudPresentationState.Pinned)
            {
                return false;
            }

            _presentationState = HudPresentationState.Collapsed;
        }

        OnStateChanged();
        return true;
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            IHudModule? currentModule;
            lock (_stateLock)
            {
                if (_isShuttingDown)
                {
                    return;
                }

                _isShuttingDown = true;
                _isTransitioning = true;
                _hasPendingPresentationInvalidation = false;
                currentModule = _currentModule;
            }

            var shutdownExceptions = new List<Exception>();
            if (currentModule is not null)
            {
                CaptureFailure(() => Unsubscribe(currentModule), shutdownExceptions);
                await CaptureFailureAsync(
                    () => currentModule.DeactivateAsync(cancellationToken),
                    shutdownExceptions);
            }

            lock (_stateLock)
            {
                _currentHudId = null;
                _currentModule = null;
                _isInitialized = false;
                _isTransitioning = false;
            }

            OnStateChanged();
            ThrowShutdownFailures(shutdownExceptions);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task TransitionToAsync(IHudModule targetModule, CancellationToken cancellationToken)
    {
        IHudModule previousModule;
        lock (_stateLock)
        {
            previousModule = _currentModule
                ?? throw new InvalidOperationException("The HUD router has no current module.");
            _isTransitioning = true;
            _hasPendingPresentationInvalidation = false;
        }

        bool targetLifecycleStarted = false;
        bool targetSubscriptionAttempted = false;
        try
        {
            Unsubscribe(previousModule);
            await previousModule.DeactivateAsync(cancellationToken);
            await InitializeModuleOnceAsync(targetModule, cancellationToken);
            targetLifecycleStarted = true;
            targetSubscriptionAttempted = true;
            Subscribe(targetModule);
            await targetModule.ActivateAsync(cancellationToken);
        }
        catch (Exception navigationException)
        {
            await HandleFailedTransitionAsync(
                targetModule,
                previousModule,
                navigationException,
                targetLifecycleStarted,
                targetSubscriptionAttempted);
        }

        bool notifyPresentation;
        lock (_stateLock)
        {
            _currentHudId = targetModule.Id;
            _currentModule = targetModule;
            _isTransitioning = false;
            notifyPresentation = TakePendingPresentationInvalidation();
        }

        OnStateChanged();
        if (notifyPresentation)
        {
            OnPresentationChanged();
        }
    }

    private async Task HandleFailedTransitionAsync(
        IHudModule targetModule,
        IHudModule previousModule,
        Exception navigationException,
        bool targetLifecycleStarted,
        bool targetSubscriptionAttempted)
    {
        var recoveryExceptions = new List<Exception>();
        if (targetSubscriptionAttempted)
        {
            CaptureFailure(() => Unsubscribe(targetModule), recoveryExceptions);
        }

        if (targetLifecycleStarted)
        {
            await CaptureFailureAsync(
                () => targetModule.DeactivateAsync(CancellationToken.None),
                recoveryExceptions);
        }

        IHudModule? recoveredModule = await TryRecoverAsync(previousModule, recoveryExceptions);
        if (recoveredModule is null)
        {
            IHudModule defaultModule = GetRegisteredModule(_effectiveDefaultHudId);
            if (!ReferenceEquals(defaultModule, previousModule))
            {
                recoveredModule = await TryRecoverAsync(defaultModule, recoveryExceptions);
            }
        }

        lock (_stateLock)
        {
            if (recoveredModule is not null)
            {
                _currentHudId = recoveredModule.Id;
                _currentModule = recoveredModule;
            }
            else
            {
                _currentHudId = null;
                _currentModule = null;
                _presentationState = HudPresentationState.Collapsed;
                _isInitialized = false;
            }

            _isTransitioning = false;
            _hasPendingPresentationInvalidation = false;
        }

        OnStateChanged();
        throw new HudNavigationException(targetModule.Id, navigationException, recoveryExceptions);
    }

    private async Task<IHudModule?> TryRecoverAsync(
        IHudModule module,
        List<Exception> recoveryExceptions)
    {
        try
        {
            Unsubscribe(module);
        }
        catch (Exception exception)
        {
            recoveryExceptions.Add(exception);
            return null;
        }

        bool subscriptionAttempted = false;
        try
        {
            await InitializeModuleOnceAsync(module, CancellationToken.None);
            subscriptionAttempted = true;
            Subscribe(module);
            await module.ActivateAsync(CancellationToken.None);
            return module;
        }
        catch (Exception exception)
        {
            recoveryExceptions.Add(exception);
            if (subscriptionAttempted)
            {
                CaptureFailure(() => Unsubscribe(module), recoveryExceptions);
            }

            await CaptureFailureAsync(
                () => module.DeactivateAsync(CancellationToken.None),
                recoveryExceptions);

            return null;
        }
    }

    private async Task InitializeModuleOnceAsync(IHudModule module, CancellationToken cancellationToken)
    {
        if (_initializedModules.Contains(module))
        {
            return;
        }

        await module.InitializeAsync(cancellationToken);
        _initializedModules.Add(module);
    }

    private void OnModulePresentationInvalidated(object? sender, EventArgs eventArgs)
    {
        lock (_stateLock)
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (_isTransitioning)
            {
                _hasPendingPresentationInvalidation = true;
                return;
            }

            if (!_isInitialized)
            {
                return;
            }
        }

        OnPresentationChanged();
    }

    private void Subscribe(IHudModule module) =>
        module.PresentationInvalidated += OnModulePresentationInvalidated;

    private void Unsubscribe(IHudModule module) =>
        module.PresentationInvalidated -= OnModulePresentationInvalidated;

    private void EnsureCanNavigate()
    {
        lock (_stateLock)
        {
            if (_isShuttingDown)
            {
                throw new ObjectDisposedException(nameof(HudRouter));
            }

            if (!_isInitialized)
            {
                throw new InvalidOperationException("The HUD router has not been initialized.");
            }
        }
    }

    private (List<string> EnabledIds, string EffectiveDefaultId) ResolveRuntimeConfiguration(
        IEnumerable<string> configuredIds)
    {
        var enabledIds = new List<string>();
        foreach (string id in configuredIds)
        {
            if (!enabledIds.Contains(id, StringComparer.Ordinal) && _registry.TryGet(id, out _))
            {
                enabledIds.Add(id);
            }
        }

        EnsureRuntimeModuleAvailable(enabledIds);
        return (enabledIds, ResolveEffectiveDefault(enabledIds));
    }

    private void EnsureRuntimeModuleAvailable(List<string> enabledIds)
    {
        if (enabledIds.Count > 0)
        {
            return;
        }

        if (_registry.TryGet(BuiltInHudIds.Music, out _))
        {
            enabledIds.Add(BuiltInHudIds.Music);
            return;
        }

        IHudModule firstModule = _registry.Modules.FirstOrDefault()
            ?? throw new InvalidOperationException("At least one HUD module must be registered.");
        enabledIds.Add(firstModule.Id);
    }

    private string ResolveEffectiveDefault(IReadOnlyList<string> enabledIds)
    {
        if (enabledIds.Contains(_configuredDefaultHudId, StringComparer.Ordinal))
        {
            return _configuredDefaultHudId;
        }

        if (enabledIds.Contains(BuiltInHudIds.Music, StringComparer.Ordinal))
        {
            return BuiltInHudIds.Music;
        }

        return _registry.Modules
            .First(module => enabledIds.Contains(module.Id, StringComparer.Ordinal))
            .Id;
    }

    private static string ResolveNavigationTarget(
        string requestedHudId,
        IReadOnlyList<string> enabledIds,
        string effectiveDefaultHudId) =>
        enabledIds.Contains(requestedHudId, StringComparer.Ordinal)
            ? requestedHudId
            : effectiveDefaultHudId;

    private IHudModule GetRegisteredModule(string hudId) =>
        _registry.TryGet(hudId, out IHudModule? module)
            ? module!
            : throw new InvalidOperationException($"HUD module '{hudId}' is not registered.");

    private bool TakePendingPresentationInvalidation()
    {
        bool pending = _hasPendingPresentationInvalidation;
        _hasPendingPresentationInvalidation = false;
        return pending;
    }

    private static async Task CaptureFailureAsync(
        Func<ValueTask> operation,
        ICollection<Exception> exceptions)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private static void CaptureFailure(Action operation, ICollection<Exception> exceptions)
    {
        try
        {
            operation();
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private static void ThrowShutdownFailures(IReadOnlyList<Exception> exceptions)
    {
        if (exceptions.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException("Multiple HUD shutdown operations failed.", exceptions);
        }
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnPresentationChanged() => PresentationChanged?.Invoke(this, EventArgs.Empty);
}
