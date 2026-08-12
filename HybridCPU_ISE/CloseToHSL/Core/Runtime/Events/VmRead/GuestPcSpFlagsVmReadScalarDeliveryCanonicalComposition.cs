namespace YAKSys_Hybrid_CPU.Core;

internal sealed class GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition :
    IVmReadScalarResultReceiptOwner
{
    private readonly object _sync = new();
    private readonly VmxCompatibilityAdmissionService _admission = new();
    private readonly ExecutionDomainRuntime _executionRuntime;
    private readonly RootAuthorityDescriptor _root;
    private readonly EvidencePolicyDescriptor _evidence;
    private DomainRuntimeContext _context;
    private ulong _restoreGeneration;
    private ulong _generation = 1;
    private bool _enabled;
    private bool _hasReplayObservation;
    private bool _replayActive;
    private ulong _replayEpoch;
    private ulong _replayCachedPc;
    private ulong _completedReplays;

    internal GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition(
        ExecutionDomainRuntime executionRuntime,
        DomainRuntimeContext context,
        RootAuthorityDescriptor root,
        EvidencePolicyDescriptor evidence,
        ulong restoreGeneration,
        in GuestPcSpFlagsVmReadScalarDeliveryPolicyLookup lookup)
    {
        _executionRuntime = executionRuntime ?? throw new ArgumentNullException(nameof(executionRuntime));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _restoreGeneration = restoreGeneration;
        PolicyResolution = GuestPcSpFlagsVmReadScalarDeliveryAcceptedPolicyResolver.Resolve(lookup);
    }

    internal GuestPcSpFlagsVmReadScalarDeliveryPolicyResolution PolicyResolution { get; }
    internal bool IsEnabled { get { lock (_sync) return _enabled; } }
    internal ulong Generation { get { lock (_sync) return _generation; } }

    internal bool EnableExact()
    {
        lock (_sync)
        {
            if (!PolicyResolution.IsResolved || _enabled || !HasCurrentSourceBinding(_context))
                return false;
            _enabled = true;
            return true;
        }
    }

    internal void Disable()
    {
        lock (_sync)
        {
            _enabled = false;
            AdvanceGeneration();
        }
    }

    /// <summary>
    /// Publishes a replacement immutable context for future captures. This is not a
    /// profile-generation event: a receipt already issued from an atomic capture keeps
    /// its scalar value and does not re-read the replacement source at retire.
    /// </summary>
    internal bool RefreshSourceContext(DomainRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_sync)
        {
            if (context.DomainTag != _context.DomainTag ||
                context.AddressSpaceTag != _context.AddressSpaceTag ||
                !HasCurrentSourceBinding(context))
                return false;
            _context = context;
            return true;
        }
    }

    internal void ReplaceAfterRestore(
        DomainRuntimeContext context,
        ulong restoreGeneration)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_sync)
        {
            _context = context;
            _restoreGeneration = restoreGeneration;
            _enabled = false;
            AdvanceGeneration();
        }
    }

    internal void ObserveReplayPhase(in ReplayPhaseContext phase)
    {
        lock (_sync)
        {
            bool changed = _hasReplayObservation &&
                (_replayActive != phase.IsActive ||
                 _replayEpoch != phase.EpochId ||
                 _replayCachedPc != phase.CachedPc ||
                 _completedReplays != phase.CompletedReplays);
            _hasReplayObservation = true;
            _replayActive = phase.IsActive;
            _replayEpoch = phase.EpochId;
            _replayCachedPc = phase.CachedPc;
            _completedReplays = phase.CompletedReplays;
            if (changed)
                AdvanceGeneration();
        }
    }

    internal VmReadScalarDeliveryResult Prepare(
        ReplayPhaseContext replayPhase,
        VmxMicroOp carrier,
        VmReadScalarAttemptBinding? attempt,
        ulong fieldSelector,
        ulong restoreGeneration)
    {
        lock (_sync)
        {
            if (!_enabled)
                return Deny(VmReadScalarDeliveryDecision.Disabled,
                    "Exact GuestPc/GuestSp/GuestFlags scalar-delivery profile is disabled.");
            if (!PolicyResolution.IsResolved)
                return Deny(VmReadScalarDeliveryDecision.PolicyDenied, PolicyResolution.Reason);
            if (restoreGeneration != _restoreGeneration)
            {
                _enabled = false;
                AdvanceGeneration();
                return Deny(VmReadScalarDeliveryDecision.StaleReceipt,
                    "Restore generation changed; exact profile was disabled pending source rebind.");
            }
            if (!_hasReplayObservation || !_replayActive || _replayEpoch != replayPhase.EpochId ||
                attempt is null || attempt.Operation != VmxOperationKind.VmRead ||
                attempt.AttemptId == 0 || attempt.BundleIdentity == 0 ||
                attempt.ReplayEpoch != replayPhase.EpochId ||
                attempt.DomainTag != _context.DomainTag ||
                carrier.VirtualizationAdmission != attempt.Certificate)
                return Deny(VmReadScalarDeliveryDecision.AdmissionDenied,
                    "Live canonical VMREAD E1 identity is required.");
            if (fieldSelector > ushort.MaxValue ||
                unchecked((VmcsField)(ushort)fieldSelector) is not
                    (VmcsField.GuestPc or VmcsField.GuestSp or VmcsField.GuestFlags))
                return Deny(VmReadScalarDeliveryDecision.FieldDenied,
                    "Only exact GuestPc, GuestSp, or GuestFlags is admitted.");
            if (carrier.Rd is 0 or > 31 || carrier.Rs2 != 0)
                return Deny(VmReadScalarDeliveryDecision.DestinationDenied,
                    "VMREAD scalar delivery requires x1-x31 and reserved x0 Rs2.");

            VmcsField field = unchecked((VmcsField)(ushort)fieldSelector);
            ExecutionDomainSourceCaptureResult source = _executionRuntime.CaptureVmReadScalarSource(
                _context.Execution,
                _context.DomainTag,
                _context.AddressSpaceTag,
                field);
            if (!source.IsCaptured || source.Capture is null ||
                !_executionRuntime.IsAuthenticCapture(source.Capture))
                return Deny(VmReadScalarDeliveryDecision.SourceDenied, source.Reason);

            VmxCompatibilityVmReadAdmissionResult projection = _admission.AdmitVmReadProjection(
                new(
                    _context,
                    _root,
                    _evidence,
                    Descriptor: null,
                    FieldId: (ushort)field,
                    DestinationRegister: carrier.Rd,
                    FieldSelectorRegister: carrier.Rs1,
                    ReservedRegister: carrier.Rs2,
                    DescriptorValidated: true,
                    CapabilityValidated: true,
                    SchedulingValidated: true,
                    NoEmissionValidated: true,
                    ProjectionEvidenceValidated: true));
            if (!projection.IsReadOnlyValueProjected ||
                unchecked((ulong)projection.Value) != source.Capture.Value)
                return Deny(VmReadScalarDeliveryDecision.ProjectionDenied,
                    projection.IsReadOnlyValueProjected
                        ? "Canonical compatibility projection mismatched the atomic ExecutionDomain source capture."
                        : projection.Reason);

            var receipt = new VmReadScalarResultReceipt(
                this,
                attempt,
                GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
                source.Capture.SourceOwner,
                source.Capture.SourceEpoch.Value,
                source.Capture,
                _generation,
                attempt.AttemptId,
                attempt.IssuerGeneration,
                attempt.BundleIdentity,
                attempt.ReplayEpoch,
                restoreGeneration,
                _context.DomainTag,
                _context.AddressSpaceTag,
                descriptorEpoch: default,
                field,
                carrier.Rd,
                source.Capture.Value);
            return new(VmReadScalarDeliveryDecision.Prepared, receipt,
                "Exact scalar value was captured from the authoritative ExecutionDomain read-only state and bound to canonical delivery.");
        }
    }

    public bool ValidateLive(
        VmReadScalarResultReceipt receipt,
        ulong? currentRestoreGeneration = null)
    {
        lock (_sync)
            return ValidateBinding(receipt, currentRestoreGeneration) && !receipt.IsConsumed;
    }

    public bool ValidateConsumedBinding(
        VmReadScalarResultReceipt receipt,
        ulong currentRestoreGeneration)
    {
        lock (_sync)
            return ValidateBinding(receipt, currentRestoreGeneration);
    }

    private bool ValidateBinding(
        VmReadScalarResultReceipt receipt,
        ulong? currentRestoreGeneration)
    {
        ExecutionDomainRuntime.SourceCapture? capture = receipt.ExecutionDomainCapture;
        return _enabled && PolicyResolution.IsResolved &&
            receipt.DecisionId == GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId &&
            receipt.ProfileGeneration == _generation && receipt.AttemptId != 0 &&
            receipt.DomainTag == _context.DomainTag &&
            receipt.AddressSpaceTag == _context.AddressSpaceTag &&
            receipt.RestoreGeneration == _restoreGeneration &&
            (!currentRestoreGeneration.HasValue ||
                receipt.RestoreGeneration == currentRestoreGeneration.Value) &&
            capture is not null && _executionRuntime.IsAuthenticCapture(capture) &&
            ReferenceEquals(receipt.SourceOwner, capture.SourceOwner) &&
            receipt.SourceEpoch == capture.SourceEpoch.Value &&
            receipt.Field == capture.Field && receipt.Value == capture.Value &&
            receipt.DomainTag == capture.DomainTag &&
            receipt.AddressSpaceTag == capture.AddressSpaceTag;
    }

    private bool HasCurrentSourceBinding(DomainRuntimeContext context) =>
        context.DomainTag != 0 && context.AddressSpaceTag != 0 &&
        context.Execution is not null &&
        ReferenceEquals(context.Execution, _executionRuntime.CurrentSourceDescriptor) &&
        _executionRuntime.CurrentSourceEpoch.IsMaterialized &&
        context.Execution.ReadOnlyState.StateEpoch == _executionRuntime.CurrentSourceEpoch.Value;

    private void AdvanceGeneration()
    {
        unchecked { _generation++; }
        if (_generation == 0) _generation = 1;
    }

    private static VmReadScalarDeliveryResult Deny(
        VmReadScalarDeliveryDecision decision,
        string reason) => new(decision, null, reason);
}
