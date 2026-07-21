namespace NoraBar.Hud;

/// <summary>
/// HUDモジュールの選択、表示状態、ライフサイクルを直列化して管理します。
/// </summary>
public sealed class HudRouter
{
    private readonly HudRegistry _registry;
    private string _configuredDefaultHudId;
    private string[] _configuredEnabledHudModuleIds;
    private readonly Action<Exception> _reportDeferredPublicationFailure;
    private readonly Action<Func<Task>> _scheduleDeferredPublication;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _publicationGate = new(1, 1);
    private readonly HashSet<IHudModule> _initializedModules = [];
    private readonly List<ModulePresentationSubscription> _pendingModulePresentationRemovals = [];
    private List<string> _enabledHudModuleIds;
    private string _effectiveDefaultHudId;
    private string? _currentHudId;
    private IHudModule? _currentModule;
    private HudPresentationState _presentationState = HudPresentationState.Collapsed;
    private bool _isInitialized;
    private bool _isShuttingDown;
    private bool _isTransitioning;
    private ModulePresentationSubscription? _pendingPresentationInvalidationSubscription;
    private bool _isPresentationInvalidationDrainScheduled;
    private ModulePresentationSubscription? _modulePresentationSubscription;
    private int _activePublicationCount;
    private int _activeLifecycleStatePublicationCount;
    private int _lifecycleMutationWaiterCount;
    private TaskCompletionSource? _publicationIdleSource;

    public HudRouter(
        HudRegistry registry,
        string configuredDefaultHudId,
        IEnumerable<string> configuredEnabledHudModuleIds)
        : this(
            registry,
            configuredDefaultHudId,
            configuredEnabledHudModuleIds,
            static exception => System.Diagnostics.Trace.TraceError(exception.ToString()),
            static operation => _ = Task.Run(operation))
    {
    }

    internal HudRouter(
        HudRegistry registry,
        string configuredDefaultHudId,
        IEnumerable<string> configuredEnabledHudModuleIds,
        Action<Exception> reportDeferredPublicationFailure)
        : this(
            registry,
            configuredDefaultHudId,
            configuredEnabledHudModuleIds,
            reportDeferredPublicationFailure,
            static operation => _ = Task.Run(operation))
    {
    }

    internal HudRouter(
        HudRegistry registry,
        string configuredDefaultHudId,
        IEnumerable<string> configuredEnabledHudModuleIds,
        Action<Exception> reportDeferredPublicationFailure,
        Action<Func<Task>> scheduleDeferredPublication)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuredDefaultHudId);
        ArgumentNullException.ThrowIfNull(configuredEnabledHudModuleIds);
        ArgumentNullException.ThrowIfNull(reportDeferredPublicationFailure);
        ArgumentNullException.ThrowIfNull(scheduleDeferredPublication);

        _registry = registry;
        _configuredDefaultHudId = configuredDefaultHudId;
        _configuredEnabledHudModuleIds = configuredEnabledHudModuleIds.ToArray();
        _reportDeferredPublicationFailure = reportDeferredPublicationFailure;
        _scheduleDeferredPublication = scheduleDeferredPublication;
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
            await EnterLifecycleMutationAsync(cancellationToken);
            try
            {
                lock (_stateLock)
                {
                    if (_isInitialized || _isShuttingDown)
                    {
                        return;
                    }

                    _isTransitioning = true;
                    _pendingPresentationInvalidationSubscription = null;
                }
            }
            finally
            {
                _publicationGate.Release();
                ExitLifecycleMutation();
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

                await UpdateStateAsync(() =>
                {
                    _isTransitioning = false;
                    _pendingPresentationInvalidationSubscription = null;
                });

                if (cleanupExceptions.Count > 0)
                {
                    throw new HudNavigationException(module.Id, initializationException, cleanupExceptions);
                }

                throw;
            }

            await PublishStateChangedAsync(() =>
            {
                _currentHudId = module.Id;
                _currentModule = module;
                _presentationState = HudPresentationState.Collapsed;
                _isInitialized = true;
                _isTransitioning = false;
            });
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

            await EnterLifecycleMutationAsync(cancellationToken);
            try
            {
                lock (_stateLock)
                {
                    _isTransitioning = true;
                    _pendingPresentationInvalidationSubscription = null;
                }
            }
            finally
            {
                _publicationGate.Release();
                ExitLifecycleMutation();
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
            List<string> updatedIds;
            string updatedDefault;
            IHudModule? fallback = null;
            await EnterLifecycleMutationAsync(cancellationToken);
            try
            {
                EnsureCanNavigate();
                List<string> currentEnabledIds;
                string currentEffectiveDefaultHudId;
                string? currentHudId;
                IHudModule? currentModule;
                lock (_stateLock)
                {
                    currentEnabledIds = [.. _enabledHudModuleIds];
                    currentEffectiveDefaultHudId = _effectiveDefaultHudId;
                    currentHudId = _currentHudId;
                    currentModule = _currentModule;
                }

                updatedIds = currentEnabledIds
                    .Where(id => !string.Equals(id, hudId, StringComparison.Ordinal))
                    .ToList();
                EnsureRuntimeModuleAvailable(updatedIds);
                updatedDefault = ResolveEffectiveDefault(updatedIds);
                bool changed = !currentEnabledIds.SequenceEqual(updatedIds, StringComparer.Ordinal)
                    || !string.Equals(
                        currentEffectiveDefaultHudId,
                        updatedDefault,
                        StringComparison.Ordinal);
                if (!changed)
                {
                    return;
                }

                if (string.Equals(currentHudId, hudId, StringComparison.Ordinal))
                {
                    string fallbackId = ResolveNavigationTarget(updatedDefault, updatedIds, updatedDefault);
                    IHudModule resolvedFallback = GetRegisteredModule(fallbackId);
                    if (!ReferenceEquals(resolvedFallback, currentModule))
                    {
                        fallback = resolvedFallback;
                    }
                }

                lock (_stateLock)
                {
                    _isTransitioning = true;
                    _pendingPresentationInvalidationSubscription = null;
                }
            }
            finally
            {
                _publicationGate.Release();
                ExitLifecycleMutation();
            }

            if (fallback is not null)
            {
                await TransitionToAsync(fallback, cancellationToken, publishState: false);
            }

            await PublishStateChangedAsync(() =>
            {
                _enabledHudModuleIds = updatedIds;
                _effectiveDefaultHudId = updatedDefault;
                _isTransitioning = false;
            });
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ApplyConfigurationAsync(
        string configuredDefaultHudId,
        IEnumerable<string> configuredEnabledHudModuleIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuredDefaultHudId);
        ArgumentNullException.ThrowIfNull(configuredEnabledHudModuleIds);
        string[] configuredIds = configuredEnabledHudModuleIds.ToArray();

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            List<string> updatedIds;
            string updatedDefault;
            IHudModule? targetModule = null;
            await EnterLifecycleMutationAsync(cancellationToken);
            try
            {
                EnsureCanNavigate();
                (updatedIds, updatedDefault) = ResolveRuntimeConfiguration(
                    configuredIds,
                    configuredDefaultHudId);

                string? currentHudId;
                IHudModule? currentModule;
                lock (_stateLock)
                {
                    currentHudId = _currentHudId;
                    currentModule = _currentModule;
                }

                if (currentHudId is null
                    || !updatedIds.Contains(currentHudId, StringComparer.Ordinal))
                {
                    targetModule = GetRegisteredModule(updatedDefault);
                    if (ReferenceEquals(targetModule, currentModule))
                    {
                        targetModule = null;
                    }
                }

                lock (_stateLock)
                {
                    _isTransitioning = true;
                    _pendingPresentationInvalidationSubscription = null;
                }
            }
            finally
            {
                _publicationGate.Release();
                ExitLifecycleMutation();
            }

            if (targetModule is not null)
            {
                await TransitionToAsync(targetModule, cancellationToken, publishState: false);
            }

            await PublishStateChangedAsync(() =>
            {
                _configuredDefaultHudId = configuredDefaultHudId;
                _configuredEnabledHudModuleIds = configuredIds;
                _enabledHudModuleIds = updatedIds;
                _effectiveDefaultHudId = updatedDefault;
                _isTransitioning = false;
            });
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public bool SetPresentationState(HudPresentationState presentationState)
    {
        if (!TryEnterPublication())
        {
            return false;
        }

        bool notifyPresentation;
        try
        {
            lock (_stateLock)
            {
                if (!_isInitialized || _isShuttingDown || _isTransitioning
                    || _activeLifecycleStatePublicationCount > 0
                    || _lifecycleMutationWaiterCount > 0
                    || _presentationState == presentationState)
                {
                    return false;
                }

                _presentationState = presentationState;
                notifyPresentation = TakePublishablePendingPresentationInvalidation();
                BeginPublicationLocked(PublicationKind.PresentationState);
            }
        }
        finally
        {
            _publicationGate.Release();
        }

        PublishStateNotification(PublicationKind.PresentationState, notifyPresentation);
        return true;
    }

    public bool CollapseFromPointerLeave()
    {
        if (!TryEnterPublication())
        {
            return false;
        }

        bool notifyPresentation;
        try
        {
            lock (_stateLock)
            {
                if (!_isInitialized || _isShuttingDown || _isTransitioning
                    || _activeLifecycleStatePublicationCount > 0
                    || _lifecycleMutationWaiterCount > 0
                    || _presentationState is HudPresentationState.Collapsed or HudPresentationState.Pinned)
                {
                    return false;
                }

                _presentationState = HudPresentationState.Collapsed;
                notifyPresentation = TakePublishablePendingPresentationInvalidation();
                BeginPublicationLocked(PublicationKind.PresentationState);
            }
        }
        finally
        {
            _publicationGate.Release();
        }

        PublishStateNotification(PublicationKind.PresentationState, notifyPresentation);
        return true;
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            IHudModule? currentModule;
            await EnterLifecycleMutationAsync(cancellationToken);
            try
            {
                lock (_stateLock)
                {
                    if (_isShuttingDown)
                    {
                        return;
                    }

                    _isShuttingDown = true;
                    _isTransitioning = true;
                    _pendingPresentationInvalidationSubscription = null;
                    currentModule = _currentModule;
                }
            }
            finally
            {
                _publicationGate.Release();
                ExitLifecycleMutation();
            }

            var shutdownExceptions = new List<Exception>();
            if (currentModule is not null)
            {
                CaptureFailure(() => Unsubscribe(currentModule), shutdownExceptions);
                await CaptureFailureAsync(
                    () => currentModule.DeactivateAsync(cancellationToken),
                    shutdownExceptions);
            }

            await PublishStateChangedAsync(() =>
            {
                _currentHudId = null;
                _currentModule = null;
                _isInitialized = false;
                _isTransitioning = false;
            }, publishPendingPresentation: false, publicationExceptions: shutdownExceptions);
            ThrowShutdownFailures(shutdownExceptions);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task TransitionToAsync(
        IHudModule targetModule,
        CancellationToken cancellationToken,
        bool publishState = true)
    {
        IHudModule previousModule;
        lock (_stateLock)
        {
            previousModule = _currentModule
                ?? throw new InvalidOperationException("The HUD router has no current module.");
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

        if (publishState)
        {
            await PublishStateChangedAsync(() =>
            {
                _currentHudId = targetModule.Id;
                _currentModule = targetModule;
                _isTransitioning = false;
            });
        }
        else
        {
            await UpdateStateAsync(() =>
            {
                _currentHudId = targetModule.Id;
                _currentModule = targetModule;
            });
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

        await PublishStateChangedAsync(() =>
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
                _pendingPresentationInvalidationSubscription = null;
            }

            _isTransitioning = false;
        }, publicationExceptions: recoveryExceptions);
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

    private void OnModulePresentationInvalidated(
        ModulePresentationSubscription subscription,
        object? sender,
        EventArgs eventArgs)
    {
        if (!TryEnterPublication())
        {
            bool scheduleDrain = false;
            lock (_stateLock)
            {
                if (ReferenceEquals(_modulePresentationSubscription, subscription)
                    && !_isShuttingDown
                    && (_isInitialized || _isTransitioning))
                {
                    _pendingPresentationInvalidationSubscription = subscription;
                    scheduleDrain = TrySchedulePresentationInvalidationDrainLocked();
                }
            }

            if (scheduleDrain)
            {
                SchedulePresentationInvalidationDrain();
            }

            return;
        }

        bool notifyPresentation = false;
        try
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_modulePresentationSubscription, subscription)
                    && !_isShuttingDown)
                {
                    if (_isTransitioning
                        || _activePublicationCount > 0
                        || _lifecycleMutationWaiterCount > 0)
                    {
                        _pendingPresentationInvalidationSubscription = subscription;
                    }
                    else if (_isInitialized)
                    {
                        notifyPresentation = true;
                        BeginPublicationLocked(PublicationKind.Presentation);
                    }
                }
            }
        }
        finally
        {
            _publicationGate.Release();
        }

        if (notifyPresentation)
        {
            PublishPresentationNotification();
        }
    }

    private void Subscribe(IHudModule module)
    {
        lock (_stateLock)
        {
            if (_modulePresentationSubscription is not null)
            {
                throw new InvalidOperationException("A HUD module presentation subscription is already active.");
            }
        }

        RetryPendingPresentationRemovals(module);

        var subscription = new ModulePresentationSubscription(this, module);
        lock (_stateLock)
        {
            if (_modulePresentationSubscription is not null)
            {
                throw new InvalidOperationException("A HUD module presentation subscription is already active.");
            }

            _modulePresentationSubscription = subscription;
        }

        module.PresentationInvalidated += subscription.Handler;
    }

    private void Unsubscribe(IHudModule module)
    {
        lock (_stateLock)
        {
            ModulePresentationSubscription? subscription = _modulePresentationSubscription;
            if (subscription is not null && ReferenceEquals(subscription.Module, module))
            {
                _modulePresentationSubscription = null;
                if (ReferenceEquals(_pendingPresentationInvalidationSubscription, subscription))
                {
                    _pendingPresentationInvalidationSubscription = null;
                }

                _pendingModulePresentationRemovals.Add(subscription);
            }
        }

        RetryPendingPresentationRemovals(module);
    }

    private void RetryPendingPresentationRemovals(IHudModule module)
    {
        ModulePresentationSubscription[] subscriptions;
        lock (_stateLock)
        {
            subscriptions = _pendingModulePresentationRemovals
                .Where(subscription => ReferenceEquals(subscription.Module, module))
                .ToArray();
        }

        Exception? firstFailure = null;
        foreach (ModulePresentationSubscription subscription in subscriptions)
        {
            try
            {
                module.PresentationInvalidated -= subscription.Handler;
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
                continue;
            }

            lock (_stateLock)
            {
                _pendingModulePresentationRemovals.Remove(subscription);
            }
        }

        if (firstFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

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
        return ResolveRuntimeConfiguration(configuredIds, _configuredDefaultHudId);
    }

    private (List<string> EnabledIds, string EffectiveDefaultId) ResolveRuntimeConfiguration(
        IEnumerable<string> configuredIds,
        string configuredDefaultHudId)
    {
        var enabledIds = new List<string>();
        foreach (string? id in configuredIds)
        {
            if (!string.IsNullOrWhiteSpace(id)
                && !enabledIds.Contains(id, StringComparer.Ordinal)
                && _registry.TryGet(id, out _))
            {
                enabledIds.Add(id);
            }
        }

        EnsureRuntimeModuleAvailable(enabledIds);
        return (enabledIds, ResolveEffectiveDefault(enabledIds, configuredDefaultHudId));
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
        return ResolveEffectiveDefault(enabledIds, _configuredDefaultHudId);
    }

    private string ResolveEffectiveDefault(
        IReadOnlyList<string> enabledIds,
        string configuredDefaultHudId)
    {
        if (enabledIds.Contains(configuredDefaultHudId, StringComparer.Ordinal))
        {
            return configuredDefaultHudId;
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
        ModulePresentationSubscription? pendingSubscription =
            _pendingPresentationInvalidationSubscription;
        _pendingPresentationInvalidationSubscription = null;
        return pendingSubscription is not null
            && ReferenceEquals(pendingSubscription, _modulePresentationSubscription);
    }

    private bool TakePublishablePendingPresentationInvalidation() =>
        _isInitialized
        && !_isShuttingDown
        && !_isTransitioning
        && _activePublicationCount == 0
        && TakePendingPresentationInvalidation();

    private bool TryEnterPublication() =>
        _publicationGate.Wait(0);

    private async Task EnterLifecycleMutationAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            _lifecycleMutationWaiterCount++;
        }

        try
        {
            while (true)
            {
                await _publicationGate.WaitAsync(cancellationToken);
                Task? publicationIdleTask;
                lock (_stateLock)
                {
                    if (_activePublicationCount == 0)
                    {
                        return;
                    }

                    publicationIdleTask = _publicationIdleSource?.Task;
                }

                if (publicationIdleTask is null)
                {
                    _publicationGate.Release();
                    throw new InvalidOperationException("The publication idle signal is missing.");
                }

                _publicationGate.Release();
                await publicationIdleTask.WaitAsync(cancellationToken);
            }
        }
        catch
        {
            bool scheduleDrain;
            lock (_stateLock)
            {
                _lifecycleMutationWaiterCount--;
                scheduleDrain = TrySchedulePresentationInvalidationDrainLocked();
            }

            if (scheduleDrain)
            {
                SchedulePresentationInvalidationDrain();
            }

            throw;
        }
    }

    private void ExitLifecycleMutation()
    {
        bool scheduleDrain;
        lock (_stateLock)
        {
            _lifecycleMutationWaiterCount--;
            scheduleDrain = TrySchedulePresentationInvalidationDrainLocked();
        }

        if (scheduleDrain)
        {
            SchedulePresentationInvalidationDrain();
        }
    }

    private void SchedulePresentationInvalidationDrain()
    {
        try
        {
            _scheduleDeferredPublication(ObservePresentationInvalidationDrainAsync);
        }
        catch (Exception exception)
        {
            lock (_stateLock)
            {
                _isPresentationInvalidationDrainScheduled = false;
            }

            ReportDeferredPublicationFailure(exception);
        }
    }

    private async Task ObservePresentationInvalidationDrainAsync()
    {
        try
        {
            await DrainPendingPresentationInvalidationAsync();
        }
        catch (Exception exception)
        {
            ReportDeferredPublicationFailure(exception);
        }
    }

    private void ReportDeferredPublicationFailure(Exception exception)
    {
        try
        {
            _reportDeferredPublicationFailure(exception);
        }
        catch (Exception reportingException)
        {
            var aggregateException = new AggregateException(
                "Reporting a deferred HUD publication failure also failed.",
                exception,
                reportingException);
            try
            {
                System.Diagnostics.Trace.TraceError(aggregateException.ToString());
            }
            catch (Exception)
            {
                // The observer must not fault after both configured error sinks have failed.
            }
        }
    }

    private async Task DrainPendingPresentationInvalidationAsync()
    {
        bool notifyPresentation;
        await _publicationGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (_stateLock)
            {
                _isPresentationInvalidationDrainScheduled = false;
                notifyPresentation = TakePublishablePendingPresentationInvalidation();
                if (notifyPresentation)
                {
                    BeginPublicationLocked(PublicationKind.Presentation);
                }
            }
        }
        finally
        {
            _publicationGate.Release();
        }

        if (notifyPresentation)
        {
            PublishPresentationNotification();
        }
    }

    private async Task UpdateStateAsync(Action stateMutation)
    {
        await _publicationGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (_stateLock)
            {
                stateMutation();
            }
        }
        finally
        {
            _publicationGate.Release();
        }
    }

    private async Task PublishStateChangedAsync(
        Action stateMutation,
        bool publishPendingPresentation = true,
        ICollection<Exception>? publicationExceptions = null)
    {
        bool notifyPresentation;
        await _publicationGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (_stateLock)
            {
                stateMutation();
                notifyPresentation = publishPendingPresentation
                    && TakePublishablePendingPresentationInvalidation();
                BeginPublicationLocked(PublicationKind.LifecycleState);
            }
        }
        finally
        {
            _publicationGate.Release();
        }

        PublishStateNotification(
            PublicationKind.LifecycleState,
            notifyPresentation,
            publicationExceptions);
    }

    private void PublishStateNotification(
        PublicationKind publicationKind,
        bool publishPresentation,
        ICollection<Exception>? publicationExceptions = null)
    {
        List<Exception>? exceptions = null;
        PublicationKind activeKind = publicationKind;
        try
        {
            InvokeSubscribers(StateChanged, ref exceptions);

            if (publishPresentation)
            {
                PublicationKind presentationKind = activeKind == PublicationKind.LifecycleState
                    ? PublicationKind.LifecyclePresentation
                    : PublicationKind.Presentation;
                ChangePublicationKind(activeKind, presentationKind);
                activeKind = presentationKind;
                InvokeSubscribers(PresentationChanged, ref exceptions);
            }

            if (publicationExceptions is null)
            {
                ThrowPublicationFailures(exceptions);
            }
            else if (exceptions is not null)
            {
                foreach (Exception exception in exceptions)
                {
                    publicationExceptions.Add(exception);
                }
            }
        }
        finally
        {
            EndPublication(activeKind);
        }
    }

    private void PublishPresentationNotification()
    {
        List<Exception>? exceptions = null;
        try
        {
            InvokeSubscribers(PresentationChanged, ref exceptions);
            ThrowPublicationFailures(exceptions);
        }
        finally
        {
            EndPublication(PublicationKind.Presentation);
        }
    }

    private void BeginPublicationLocked(PublicationKind publicationKind)
    {
        if (_activePublicationCount == 0)
        {
            _publicationIdleSource = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _activePublicationCount++;
        AdjustPublicationKindLocked(publicationKind, 1);
    }

    private void ChangePublicationKind(PublicationKind currentKind, PublicationKind nextKind)
    {
        lock (_stateLock)
        {
            AdjustPublicationKindLocked(currentKind, -1);
            AdjustPublicationKindLocked(nextKind, 1);
        }
    }

    private void EndPublication(PublicationKind publicationKind)
    {
        TaskCompletionSource? publicationIdleSource = null;
        bool scheduleDrain = false;
        lock (_stateLock)
        {
            AdjustPublicationKindLocked(publicationKind, -1);
            _activePublicationCount--;
            if (_activePublicationCount < 0)
            {
                throw new InvalidOperationException("The active publication count is invalid.");
            }

            if (_activePublicationCount == 0)
            {
                publicationIdleSource = _publicationIdleSource;
                _publicationIdleSource = null;
                scheduleDrain = TrySchedulePresentationInvalidationDrainLocked();
            }
        }

        publicationIdleSource?.TrySetResult();
        if (scheduleDrain)
        {
            SchedulePresentationInvalidationDrain();
        }
    }

    private bool TrySchedulePresentationInvalidationDrainLocked()
    {
        if (_activePublicationCount > 0
            || _lifecycleMutationWaiterCount > 0
            || _pendingPresentationInvalidationSubscription is null
            || !_isInitialized
            || _isShuttingDown
            || _isTransitioning
            || _isPresentationInvalidationDrainScheduled)
        {
            return false;
        }

        _isPresentationInvalidationDrainScheduled = true;
        return true;
    }

    private void AdjustPublicationKindLocked(PublicationKind publicationKind, int delta)
    {
        switch (publicationKind)
        {
            case PublicationKind.LifecycleState:
            case PublicationKind.LifecyclePresentation:
                _activeLifecycleStatePublicationCount += delta;
                break;
            case PublicationKind.Presentation:
            case PublicationKind.PresentationState:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(publicationKind));
        }
    }

    private void InvokeSubscribers(EventHandler? subscribers, ref List<Exception>? exceptions)
    {
        if (subscribers is null)
        {
            return;
        }

        foreach (EventHandler subscriber in subscribers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                exceptions ??= [];
                exceptions.Add(exception);
            }
        }
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

    private static void ThrowPublicationFailures(IReadOnlyList<Exception>? exceptions)
    {
        if (exceptions is null || exceptions.Count == 0)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException("One or more HUD router subscribers failed.", exceptions);
    }

    private enum PublicationKind
    {
        PresentationState,
        LifecycleState,
        LifecyclePresentation,
        Presentation
    }

    private sealed class ModulePresentationSubscription
    {
        private readonly HudRouter _router;

        public ModulePresentationSubscription(HudRouter router, IHudModule module)
        {
            _router = router;
            Module = module;
            Handler = HandlePresentationInvalidated;
        }

        public IHudModule Module { get; }

        public EventHandler Handler { get; }

        private void HandlePresentationInvalidated(object? sender, EventArgs eventArgs) =>
            _router.OnModulePresentationInvalidated(this, sender, eventArgs);
    }
}
