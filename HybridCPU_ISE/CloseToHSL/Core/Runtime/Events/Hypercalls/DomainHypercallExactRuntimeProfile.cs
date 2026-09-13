namespace YAKSys_Hybrid_CPU.Core;

internal enum DomainHypercallExactActivationDecision : byte
{
    Activated = 0,
    AlreadyActive = 1,
    DeniedNonExactProfile = 2,
    DeniedInvalidDomain = 3,
    ProvisioningFailed = 4,
}

internal enum DomainHypercallKillSwitchDecision : byte
{
    DisabledFaultOnly = 0,
    AlreadyDisabledFaultOnly = 1,
    TransitionDrainTimedOut = 2,
    RegistryDrainFailed = 3,
    RevocationFailed = 4,
}

internal enum DomainHypercallKillSwitchStep : byte
{
    NewE2Closed = 0,
    TransitionsQuiescent = 1,
    RegistriesQuiescent = 2,
    ExactBindingAndGrantRevoked = 3,
    DeterministicFaultOnlyFallbackRestored = 4,
}

internal readonly record struct DomainHypercallExactActivationResult(
    DomainHypercallExactActivationDecision Decision,
    DomainHypercallLifecycleSnapshot Lifecycle,
    bool ExactBindingPresent,
    bool ExactGrantLive,
    string Reason)
{
    internal bool IsActivated =>
        Decision == DomainHypercallExactActivationDecision.Activated &&
        Lifecycle.State == DomainHypercallLifecycleState.ActiveExactProfile &&
        ExactBindingPresent && ExactGrantLive;
}

internal readonly record struct DomainHypercallKillSwitchResult(
    DomainHypercallKillSwitchDecision Decision,
    DomainHypercallLifecycleSnapshot Lifecycle,
    DomainHypercallLiveAuthorityCounts Counts,
    bool ExactBindingPresent,
    bool ExactGrantLive,
    IReadOnlyList<DomainHypercallKillSwitchStep> Trace,
    string Reason)
{
    internal bool IsDeterministicFaultOnly =>
        Decision is DomainHypercallKillSwitchDecision.DisabledFaultOnly or
            DomainHypercallKillSwitchDecision.AlreadyDisabledFaultOnly &&
        Lifecycle.State == DomainHypercallLifecycleState.DisabledFaultOnly &&
        Counts.IsDrained && !ExactBindingPresent && !ExactGrantLive;
}

/// <summary>
/// Default-disabled provisioning contour for one exact domain profile. This
/// object coordinates existing neutral owners and owns no E2/E3/E5/E6 authority.
/// VMX compatibility remains an unbound, deterministic fault-only frontend.
/// </summary>
internal sealed class DomainHypercallExactRuntimeProfile
{
    private readonly object _sync = new();
    private readonly ulong _domainTag;
    private readonly MicroOpScheduler _scheduler;
    private readonly DomainHypercallRetireOwner _retireOwner;
    private readonly DomainHypercallLifecycleGate _lifecycleGate;
    private RuntimeCapabilityGrantOwner? _capabilityOwner;
    private RuntimeCapabilityGrantLease? _capabilityLease;
    private VirtualizationRestoreGenerationOwner? _restoreOwner;
    private DomainHypercallRuntimeExecutor? _executor;
    private DomainHypercallCompletionOwner? _completionOwner;
    private DomainHypercallCanonicalComposition? _composition;
    private DomainHypercallDrainLifecycleOwner? _drainOwner;

    internal DomainHypercallExactRuntimeProfile(
        ulong domainTag,
        MicroOpScheduler scheduler,
        DomainHypercallRetireOwner retireOwner)
    {
        if (domainTag == 0 || domainTag > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(domainTag));
        _domainTag = domainTag;
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _retireOwner = retireOwner ?? throw new ArgumentNullException(nameof(retireOwner));
        _lifecycleGate = new DomainHypercallLifecycleGate(domainTag);
    }

    internal ulong DomainTag => _domainTag;
    internal DomainHypercallLifecycleGate LifecycleGate => _lifecycleGate;
    internal DomainHypercallDrainLifecycleOwner? DrainOwner => _drainOwner;
    internal DomainHypercallCanonicalComposition? Composition => _composition;
    internal DomainHypercallRuntimeExecutor? Executor => _executor;
    internal DomainHypercallCompletionOwner? CompletionOwner => _completionOwner;
    internal VirtualizationRestoreGenerationOwner? RestoreOwner => _restoreOwner;

    internal DomainHypercallExactActivationResult Activate(
        DomainHypercallExactActivationRequest request)
    {
        lock (_sync)
        {
            DomainHypercallLifecycleSnapshot current = _lifecycleGate.Observe();
            if (current.State == DomainHypercallLifecycleState.ActiveExactProfile)
                return ActivationResult(
                    DomainHypercallExactActivationDecision.AlreadyActive,
                    "The exact per-domain profile is already active.");
            if (!request.IsPhase38Exact)
                return ActivationResult(
                    DomainHypercallExactActivationDecision.DeniedNonExactProfile,
                    "Activation denied every profile except the exact accepted Phase-38 operation.");
            if (_scheduler.HasExactVirtualizationComposition)
                return ActivationResult(
                    DomainHypercallExactActivationDecision.ProvisioningFailed,
                    "Scheduler already has an exact composition binding; replacement is denied.");
            if (_domainTag == 0 || _domainTag > ushort.MaxValue)
                return ActivationResult(
                    DomainHypercallExactActivationDecision.DeniedInvalidDomain,
                    "Exact activation requires one non-zero neutral domain tag within trap transport width.");

            try
            {
                CapabilityGrant grant = CreateExactGrant(_domainTag);
                var capabilityOwner = new RuntimeCapabilityGrantOwner();
                RuntimeCapabilityGrantLease lease = capabilityOwner.Issue(grant);
                var restoreOwner = new VirtualizationRestoreGenerationOwner();
                var executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
                var completionOwner = new DomainHypercallCompletionOwner();
                DomainRuntimeContext context = CreateExactDomainContext(_domainTag, grant);
                var root = new RootAuthorityDescriptor(
                    RootAuthorityClass.RuntimeRoot,
                    authorityEpoch: 1,
                    RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                    allowCompatibilityFrontendActivation: false,
                    allowAuthoritativeStateMutation: false);

                if (!_lifecycleGate.TryActivateExact(request))
                {
                    capabilityOwner.RevokeAll();
                    return ActivationResult(
                        DomainHypercallExactActivationDecision.ProvisioningFailed,
                        "Exact lifecycle activation failed closed before binding publication.");
                }

                var composition = new DomainHypercallCanonicalComposition(
                    context,
                    root,
                    capabilityOwner,
                    lease,
                    restoreOwner,
                    executor,
                    completionOwner,
                    _retireOwner,
                    _lifecycleGate);
                SafetyVerifier verifier = _scheduler.ExactVirtualizationCanonicalVerifier ??
                    throw new InvalidOperationException("Canonical SafetyVerifier is unavailable.");
                var drainOwner = new DomainHypercallDrainLifecycleOwner(
                    _domainTag,
                    composition,
                    verifier,
                    executor,
                    completionOwner,
                    _retireOwner,
                    restoreOwner);
                _scheduler.ConfigureExactVirtualizationComposition(composition);

                _capabilityOwner = capabilityOwner;
                _capabilityLease = lease;
                _restoreOwner = restoreOwner;
                _executor = executor;
                _completionOwner = completionOwner;
                _composition = composition;
                _drainOwner = drainOwner;
                return ActivationResult(
                    DomainHypercallExactActivationDecision.Activated,
                    "Activated only the exact accepted no-state probe profile for one neutral domain.");
            }
            catch (Exception ex)
            {
                _composition?.Disable();
                _capabilityOwner?.RevokeAll();
                _lifecycleGate.BeginDrain();
                _lifecycleGate.RevokeExactBinding();
                if (_composition is not null)
                    _scheduler.DisableExactVirtualizationComposition();
                ClearBinding();
                return ActivationResult(
                    DomainHypercallExactActivationDecision.ProvisioningFailed,
                    $"Exact activation rolled back fail-closed: {ex.GetType().Name}.");
            }
        }
    }

    internal DomainHypercallKillSwitchResult KillSwitch(TimeSpan transitionTimeout)
    {
        lock (_sync)
        {
            if (_composition is null || _drainOwner is null)
            {
                return new(
                    DomainHypercallKillSwitchDecision.AlreadyDisabledFaultOnly,
                    _lifecycleGate.Observe(),
                    new(0, 0, 0, 0, 0),
                    ExactBindingPresent: false,
                    ExactGrantLive: false,
                    [DomainHypercallKillSwitchStep.DeterministicFaultOnlyFallbackRestored],
                    "Exact binding is absent; compatibility remains deterministic fault-only.");
            }

            DomainHypercallDrainLifecycleOwner drainOwner = _drainOwner;
            DomainHypercallCanonicalComposition composition = _composition;
            RuntimeCapabilityGrantOwner? capabilityOwner = _capabilityOwner;
            RuntimeCapabilityGrantLease? capabilityLease = _capabilityLease;
            composition.BeginDrain();
            var trace = new List<DomainHypercallKillSwitchStep>
            {
                DomainHypercallKillSwitchStep.NewE2Closed,
            };
            DomainHypercallDrainResult drained = drainOwner.CancelAndCheckpoint(transitionTimeout);
            if (!drained.IsCheckpointReady)
            {
                return new(
                    drained.Counts.TransitionsInFlight != 0
                        ? DomainHypercallKillSwitchDecision.TransitionDrainTimedOut
                        : DomainHypercallKillSwitchDecision.RegistryDrainFailed,
                    _lifecycleGate.Observe(),
                    drained.Counts,
                    ExactBindingPresent: true,
                    ExactGrantLive: capabilityOwner?.IsLive(capabilityLease) == true,
                    trace,
                    drained.Reason);
            }

            trace.Add(DomainHypercallKillSwitchStep.TransitionsQuiescent);
            trace.Add(DomainHypercallKillSwitchStep.RegistriesQuiescent);
            _scheduler.DisableExactVirtualizationComposition();
            capabilityOwner?.RevokeAll();
            bool revoked = _lifecycleGate.RevokeExactBinding();
            bool grantLive = capabilityOwner?.IsLive(capabilityLease) == true;
            if (!revoked || grantLive)
            {
                return new(
                    DomainHypercallKillSwitchDecision.RevocationFailed,
                    _lifecycleGate.Observe(),
                    drained.Counts,
                    ExactBindingPresent: _composition is not null,
                    ExactGrantLive: grantLive,
                    trace,
                    "Exact binding/grant revocation failed closed in draining state.");
            }

            trace.Add(DomainHypercallKillSwitchStep.ExactBindingAndGrantRevoked);
            ClearBinding();
            trace.Add(DomainHypercallKillSwitchStep.DeterministicFaultOnlyFallbackRestored);
            return new(
                DomainHypercallKillSwitchDecision.DisabledFaultOnly,
                _lifecycleGate.Observe(),
                drained.Counts,
                ExactBindingPresent: false,
                ExactGrantLive: false,
                trace,
                "Kill switch restored the unbound deterministic compatibility fault-only fallback.");
        }
    }

    private DomainHypercallExactActivationResult ActivationResult(
        DomainHypercallExactActivationDecision decision,
        string reason) => new(
            decision,
            _lifecycleGate.Observe(),
            ExactBindingPresent: _composition is not null,
            ExactGrantLive: _capabilityOwner?.IsLive(_capabilityLease) == true,
            reason);

    private void ClearBinding()
    {
        _composition = null;
        _drainOwner = null;
        _executor = null;
        _completionOwner = null;
        _restoreOwner = null;
        _capabilityLease = null;
        _capabilityOwner = null;
    }

    private static CapabilityGrant CreateExactGrant(ulong domainTag) => new(
        RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
        CapabilityGrantScope.DomainGranted,
        isGranted: true,
        ownerDomainId: domainTag,
        CapabilityDelegationPolicy.NonDelegable,
        CapabilityRevocationPolicy.RuntimeRevocable,
        CapabilityMigrationClass.DomainLocal,
        CapabilityEvidenceVisibility.HostOnly,
        CapabilityFrontendProjectionPolicy.NeverProject);

    private static DomainRuntimeContext CreateExactDomainContext(
        ulong domainTag,
        CapabilityGrant grant) => new(
            new ExecutionDomainDescriptor(
                domainTag,
                new BundleLegalityDescriptor(),
                schedulingBudget: null,
                extension: null,
                compatibilityProjectionEnabled: false),
            memory: null,
            io: null,
            new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
            secureCompute: null,
            domainTag,
            addressSpaceTag: 0);
}
