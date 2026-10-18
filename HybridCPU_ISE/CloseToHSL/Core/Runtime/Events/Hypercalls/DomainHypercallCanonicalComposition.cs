namespace YAKSys_Hybrid_CPU.Core;

using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

internal enum DomainHypercallCompositionDecision : byte
{
    Prepared = 0,
    Disabled = 1,
    MissingCanonicalVerifier = 2,
    InvalidRuntimeBinding = 3,
    E2Denied = 4,
    DuplicateCarrier = 5,
    Draining = 6,
}

internal readonly record struct DomainHypercallCompositionResult(
    DomainHypercallCompositionDecision Decision,
    SafetyVerifier.VirtualizationOperationAdmissionCertificate? E2,
    string Reason)
{
    internal bool IsPrepared =>
        Decision == DomainHypercallCompositionDecision.Prepared && E2 is not null;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Neutral owner binding for the exact Phase-38 operation. The binding is
/// production-compiled but not globally registered: only the canonical scheduler
/// seam may consume it. Disabling is irreversible and revokes its grant generation.
/// </summary>
internal sealed class DomainHypercallCanonicalComposition
{
    internal sealed class ExecutionDispatch
    {
        private readonly DomainHypercallCanonicalComposition _owner;
        private readonly VmxMicroOp _carrier;
        private readonly SafetyVerifier _verifier;
        private readonly SafetyVerifier.VirtualizationOperationAdmissionCertificate _e2;

        private ExecutionDispatch(
            DomainHypercallCanonicalComposition owner,
            VmxMicroOp carrier,
            SafetyVerifier verifier,
            SafetyVerifier.VirtualizationOperationAdmissionCertificate e2)
        {
            _owner = owner;
            _carrier = carrier;
            _verifier = verifier;
            _e2 = e2;
        }

        internal DomainHypercallExecutionResult Execute(
            VmxMicroOp carrier,
            out DomainHypercallCompletionPublicationResult? publication)
        {
            publication = null;
            if (!ReferenceEquals(carrier, _carrier))
            {
                return new(
                    DomainHypercallExecutionDecision.InvalidAdmission,
                    null,
                    "Exact hypercall dispatch cannot be transplanted to another carrier.");
            }

            return _owner.Execute(_verifier, _e2, out publication);
        }

        internal static ExecutionDispatch Create(
            DomainHypercallCanonicalComposition owner,
            VmxMicroOp carrier,
            SafetyVerifier verifier,
            SafetyVerifier.VirtualizationOperationAdmissionCertificate e2) =>
            new(owner, carrier, verifier, e2);
    }

    private readonly object _sync = new();
    private readonly DomainRuntimeContext _context;
    private readonly RootAuthorityDescriptor _root;
    private readonly RuntimeCapabilityGrantOwner _capabilityOwner;
    private readonly RuntimeCapabilityGrantLease _capabilityLease;
    private readonly VirtualizationRestoreGenerationOwner _restoreOwner;
    private readonly DomainHypercallRuntimeExecutor _executor;
    private readonly DomainHypercallCompletionOwner? _completionOwner;
    private readonly DomainHypercallLifecycleGate _lifecycleGate;
    private bool _enabled;
    private bool _draining;

    internal DomainHypercallCanonicalComposition(
        DomainRuntimeContext context,
        RootAuthorityDescriptor root,
        RuntimeCapabilityGrantOwner capabilityOwner,
        RuntimeCapabilityGrantLease capabilityLease,
        VirtualizationRestoreGenerationOwner restoreOwner,
        DomainHypercallRuntimeExecutor executor,
        DomainHypercallCompletionOwner? completionOwner = null,
        DomainHypercallRetireOwner? retireOwner = null,
        DomainHypercallLifecycleGate? lifecycleGate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(capabilityOwner);
        ArgumentNullException.ThrowIfNull(capabilityLease);
        ArgumentNullException.ThrowIfNull(restoreOwner);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(lifecycleGate);
        if (lifecycleGate.DomainTag != context.DomainTag || !lifecycleGate.Observe().AcceptsNewE2)
            throw new InvalidOperationException("Exact composition requires the active matching per-domain lifecycle gate.");

        _context = context;
        _root = root;
        _capabilityOwner = capabilityOwner;
        _capabilityLease = capabilityLease;
        _restoreOwner = restoreOwner;
        _executor = executor;
        _completionOwner = completionOwner;
        _lifecycleGate = lifecycleGate;
        if (completionOwner is not null)
        {
            _executor.BindCompletionOwner(completionOwner);
            if (retireOwner is not null)
                completionOwner.BindRetireOwner(retireOwner);
        }
        _enabled = true;
    }

    internal bool IsEnabled
    {
        get
        {
            lock (_sync)
                return _enabled;
        }
    }

    internal DomainHypercallCompositionResult Prepare(
        SafetyVerifier verifier,
        ReplayPhaseContext replayPhase,
        SmtBundleMetadata4Way bundleMetadata,
        VmxMicroOp carrier,
        int sourceSlotId,
        int workingSlotId,
        SafetyVerifier.VirtualizationAdmissionCertificate e1,
        VirtualizationOperandSnapshot operand)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(e1);
        ArgumentNullException.ThrowIfNull(operand);

        lock (_sync)
        {
            if (!_enabled)
                return Deny(DomainHypercallCompositionDecision.Disabled, "Exact hypercall composition is disabled.");
            if (_draining)
                return Deny(DomainHypercallCompositionDecision.Draining, "DrainOnly gate has closed new exact E2 admission.");
            if (_executor.Mode != ExactProbeExecutionMode.ExactProbeOnly ||
                !_capabilityOwner.IsLive(_capabilityLease) ||
                _restoreOwner.CurrentGeneration == 0)
            {
                return Deny(
                    DomainHypercallCompositionDecision.InvalidRuntimeBinding,
                    "Exact hypercall composition requires a live exact owner binding and enabled executor.");
            }

            VirtualizationE2Result issued = verifier.IssueVirtualizationE2(new(
                replayPhase,
                bundleMetadata,
                carrier,
                sourceSlotId,
                workingSlotId,
                e1,
                Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot,
                operand,
                _context,
                _root,
                _capabilityOwner,
                _capabilityLease,
                _restoreOwner,
                _lifecycleGate));
            if (!issued.IsIssued || issued.Certificate is null)
                return new(DomainHypercallCompositionDecision.E2Denied, null, issued.Reason);

            ExecutionDispatch dispatch = ExecutionDispatch.Create(this, carrier, verifier, issued.Certificate);
            try
            {
                carrier.AttachExactHypercallExecutionDispatch(dispatch);
            }
            catch (InvalidOperationException ex)
            {
                verifier.RevokeVirtualizationE2(issued.Certificate);
                return Deny(DomainHypercallCompositionDecision.DuplicateCarrier, ex.Message);
            }

            return new(
                DomainHypercallCompositionDecision.Prepared,
                issued.Certificate,
                "Canonical E1/operand seam prepared exact E2 for execute-stage consumption.");
        }
    }

    internal void Disable()
    {
        _lifecycleGate.BeginDrain();
        lock (_sync)
        {
            if (!_enabled)
                return;

            _enabled = false;
            _capabilityOwner.RevokeAll();
        }
    }

    internal void BeginDrain()
    {
        _lifecycleGate.BeginDrain();
        lock (_sync)
        {
            if (_enabled)
                _draining = true;
        }
    }

    internal bool ResumeAfterValidatedRestore(string specDigest)
    {
        lock (_sync)
        {
            if (!_enabled ||
                !string.Equals(specDigest, Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, StringComparison.Ordinal))
                return false;
            if (!_lifecycleGate.ResumeAfterValidatedRestore(specDigest))
                return false;
            _draining = false;
            return true;
        }
    }

    internal bool IsDraining
    {
        get
        {
            lock (_sync)
                return _draining;
        }
    }

    internal DomainHypercallLifecycleGate LifecycleGate => _lifecycleGate;

    private DomainHypercallExecutionResult Execute(
        SafetyVerifier verifier,
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2,
        out DomainHypercallCompletionPublicationResult? publication)
    {
        publication = null;
        lock (_sync)
        {
            if (!_enabled)
            {
                verifier.RevokeVirtualizationE2(e2);
                return new(
                    DomainHypercallExecutionDecision.Disabled,
                    null,
                    "Exact hypercall composition was disabled before execute.");
            }

            DomainHypercallExecutionResult execution =
                _executor.ExecuteExactProbe(verifier, e2, _restoreOwner, _lifecycleGate);
            if (execution.IsExecuted && execution.Receipt is not null && _completionOwner is not null)
            {
                publication = _completionOwner.PublishExactProbe(
                    _executor,
                    execution.Receipt,
                    _restoreOwner,
                    _context,
                    _root,
                    _lifecycleGate);
            }

            return execution;
        }
    }

    private static DomainHypercallCompositionResult Deny(
        DomainHypercallCompositionDecision decision,
        string reason) => new(decision, null, reason);
}
