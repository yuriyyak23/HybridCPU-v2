namespace YAKSys_Hybrid_CPU.Core;

internal sealed class MemoryOwnedVmReadScalarDeliveryCanonicalComposition :
    IVmReadScalarResultReceiptOwner
{
    private readonly object _sync = new();
    private readonly VmxCompatibilityAdmissionService _admission = new();
    private readonly MemoryDomainRuntime _memoryRuntime;
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

    internal MemoryOwnedVmReadScalarDeliveryCanonicalComposition(
        MemoryDomainRuntime memoryRuntime,
        DomainRuntimeContext context,
        RootAuthorityDescriptor root,
        EvidencePolicyDescriptor evidence,
        ulong restoreGeneration,
        in MemoryOwnedVmReadScalarDeliveryPolicyLookup lookup)
    {
        _memoryRuntime = memoryRuntime ?? throw new ArgumentNullException(nameof(memoryRuntime));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _restoreGeneration = restoreGeneration;
        PolicyResolution = MemoryOwnedVmReadScalarDeliveryAcceptedPolicyResolver.Resolve(lookup);
    }

    internal MemoryOwnedVmReadScalarDeliveryPolicyResolution PolicyResolution { get; }
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

    internal void ReplaceAfterRestore(DomainRuntimeContext context, ulong restoreGeneration)
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
                    "Exact memory-owned scalar-delivery profile is disabled.");
            if (!PolicyResolution.IsResolved)
                return Deny(VmReadScalarDeliveryDecision.PolicyDenied, PolicyResolution.Reason);
            if (restoreGeneration != _restoreGeneration)
            {
                _enabled = false;
                AdvanceGeneration();
                return Deny(VmReadScalarDeliveryDecision.StaleReceipt,
                    "Restore generation changed; exact memory-owned profile was disabled pending source rebind.");
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
                    (VmcsField.GuestCr3 or VmcsField.EptPointer or
                     VmcsField.Vpid or VmcsField.Cr3TargetCount))
                return Deny(VmReadScalarDeliveryDecision.FieldDenied,
                    "Only exact GuestCr3, EptPointer, Vpid, or Cr3TargetCount is admitted.");
            if (carrier.Rd is 0 or > 31 || carrier.Rs2 != 0)
                return Deny(VmReadScalarDeliveryDecision.DestinationDenied,
                    "VMREAD scalar delivery requires x1-x31 and reserved x0 Rs2.");

            VmcsField field = unchecked((VmcsField)(ushort)fieldSelector);
            MemoryDomainSourceCaptureResult source = _memoryRuntime.CaptureVmReadScalarSource(
                _context.Memory,
                _context.DomainTag,
                _context.AddressSpaceTag,
                field);
            if (!source.IsCaptured || source.Capture is null ||
                !_memoryRuntime.IsAuthenticCapture(source.Capture))
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
                        ? "Canonical compatibility projection mismatched the atomic MemoryDomain source capture."
                        : projection.Reason);

            var receipt = new VmReadScalarResultReceipt(
                this,
                attempt,
                MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
                source.Capture.SourceOwner,
                source.Capture.AddressSpaceGeneration,
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
                "Exact scalar value was captured from authoritative MemoryDomain translation state and bound to canonical delivery.");
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
        MemoryDomainRuntime.SourceCapture? capture = receipt.MemoryDomainCapture;
        return _enabled && PolicyResolution.IsResolved &&
            receipt.DecisionId == MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId &&
            receipt.ProfileGeneration == _generation && receipt.AttemptId != 0 &&
            receipt.DomainTag == _context.DomainTag &&
            receipt.AddressSpaceTag == _context.AddressSpaceTag &&
            receipt.RestoreGeneration == _restoreGeneration &&
            (!currentRestoreGeneration.HasValue ||
                receipt.RestoreGeneration == currentRestoreGeneration.Value) &&
            capture is not null && _memoryRuntime.IsAuthenticCapture(capture) &&
            ReferenceEquals(receipt.SourceOwner, capture.SourceOwner) &&
            receipt.SourceEpoch == capture.AddressSpaceGeneration &&
            receipt.Field == capture.Field && receipt.Value == capture.Value &&
            receipt.DomainTag == capture.DomainTag &&
            receipt.AddressSpaceTag == capture.RuntimeAddressSpaceTag;
    }

    private bool HasCurrentSourceBinding(DomainRuntimeContext context) =>
        context.DomainTag != 0 && context.AddressSpaceTag != 0 &&
        context.Memory is not null &&
        ReferenceEquals(context.Memory, _memoryRuntime.CurrentTranslationSource) &&
        _memoryRuntime.CurrentAddressSpaceGeneration != 0 &&
        context.Memory.TranslationControl.AddressSpaceGeneration ==
            _memoryRuntime.CurrentAddressSpaceGeneration &&
        context.Memory.TranslationControl.DomainTag == context.DomainTag;

    private void AdvanceGeneration()
    {
        unchecked { _generation++; }
        if (_generation == 0) _generation = 1;
    }

    private static VmReadScalarDeliveryResult Deny(
        VmReadScalarDeliveryDecision decision,
        string reason) => new(decision, null, reason);
}
