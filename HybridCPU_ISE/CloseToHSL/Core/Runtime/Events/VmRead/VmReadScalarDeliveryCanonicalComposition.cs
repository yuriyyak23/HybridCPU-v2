using System.Threading;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VmReadScalarDeliveryDecision : byte
{
    Prepared = 0,
    Disabled = 1,
    PolicyDenied = 2,
    AdmissionDenied = 3,
    FieldDenied = 4,
    DestinationDenied = 5,
    ProjectionDenied = 6,
    StaleReceipt = 7,
    DuplicateReceipt = 8,
    SourceDenied = 9,
}

internal readonly record struct VmReadScalarDeliveryResult(
    VmReadScalarDeliveryDecision Decision,
    VmReadScalarResultReceipt? Receipt,
    string Reason)
{
    internal bool IsPrepared => Decision == VmReadScalarDeliveryDecision.Prepared && Receipt is not null;
    internal bool BackendExecutionAuthorized => false;
    internal bool UnderlyingVirtualizationMutationAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool VmxRetireEffectAuthorized => false;
}

internal interface IVmReadScalarResultReceiptOwner
{
    bool ValidateLive(VmReadScalarResultReceipt receipt, ulong? currentRestoreGeneration = null);
    bool ValidateConsumedBinding(VmReadScalarResultReceipt receipt, ulong currentRestoreGeneration);
}

internal sealed class VmReadScalarAttemptBinding
{
    internal VmReadScalarAttemptBinding(
        SafetyVerifier.VirtualizationAdmissionCertificate certificate) =>
        Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));

    internal SafetyVerifier.VirtualizationAdmissionCertificate Certificate { get; }
    internal ulong AttemptId => Certificate.AttemptId;
    internal ulong IssuerGeneration => Certificate.IssuerGeneration;
    internal ulong BundleIdentity => Certificate.BundleIdentity;
    internal ulong ReplayEpoch => Certificate.ReplayEpoch;
    internal ulong DomainTag => Certificate.DomainTag;
    internal VmxOperationKind Operation => Certificate.Operation;
}

internal sealed class VmReadScalarResultReceipt
{
    private readonly IVmReadScalarResultReceiptOwner _owner;
    private readonly VmReadScalarAttemptBinding _attempt;
    private int _consumed;

    internal VmReadScalarResultReceipt(
        IVmReadScalarResultReceiptOwner owner,
        VmReadScalarAttemptBinding attempt,
        string decisionId,
        object sourceOwner,
        ulong sourceEpoch,
        ExecutionDomainRuntime.SourceCapture? executionDomainCapture,
        ulong profileGeneration,
        ulong attemptId,
        ulong issuerGeneration,
        ulong bundleIdentity,
        ulong replayEpoch,
        ulong restoreGeneration,
        ulong domainTag,
        ulong addressSpaceTag,
        PrivilegedExecutionStateEpoch descriptorEpoch,
        VmcsField field,
        byte destinationRegister,
        ulong value)
        : this(owner, attempt, decisionId, sourceOwner, sourceEpoch,
            executionDomainCapture, memoryDomainCapture: null,
            profileGeneration, attemptId, issuerGeneration, bundleIdentity,
            replayEpoch, restoreGeneration, domainTag, addressSpaceTag,
            descriptorEpoch, field, destinationRegister, value)
    {
    }

    internal VmReadScalarResultReceipt(
        IVmReadScalarResultReceiptOwner owner,
        VmReadScalarAttemptBinding attempt,
        string decisionId,
        object sourceOwner,
        ulong sourceEpoch,
        MemoryDomainRuntime.SourceCapture memoryDomainCapture,
        ulong profileGeneration,
        ulong attemptId,
        ulong issuerGeneration,
        ulong bundleIdentity,
        ulong replayEpoch,
        ulong restoreGeneration,
        ulong domainTag,
        ulong addressSpaceTag,
        PrivilegedExecutionStateEpoch descriptorEpoch,
        VmcsField field,
        byte destinationRegister,
        ulong value)
        : this(owner, attempt, decisionId, sourceOwner, sourceEpoch,
            executionDomainCapture: null, memoryDomainCapture,
            profileGeneration, attemptId, issuerGeneration, bundleIdentity,
            replayEpoch, restoreGeneration, domainTag, addressSpaceTag,
            descriptorEpoch, field, destinationRegister, value)
    {
    }

    private VmReadScalarResultReceipt(
        IVmReadScalarResultReceiptOwner owner,
        VmReadScalarAttemptBinding attempt,
        string decisionId,
        object sourceOwner,
        ulong sourceEpoch,
        ExecutionDomainRuntime.SourceCapture? executionDomainCapture,
        MemoryDomainRuntime.SourceCapture? memoryDomainCapture,
        ulong profileGeneration,
        ulong attemptId,
        ulong issuerGeneration,
        ulong bundleIdentity,
        ulong replayEpoch,
        ulong restoreGeneration,
        ulong domainTag,
        ulong addressSpaceTag,
        PrivilegedExecutionStateEpoch descriptorEpoch,
        VmcsField field,
        byte destinationRegister,
        ulong value)
    {
        _owner = owner;
        _attempt = attempt;
        DecisionId = decisionId;
        SourceOwner = sourceOwner;
        SourceEpoch = sourceEpoch;
        ExecutionDomainCapture = executionDomainCapture;
        MemoryDomainCapture = memoryDomainCapture;
        ProfileGeneration = profileGeneration;
        AttemptId = attemptId;
        IssuerGeneration = issuerGeneration;
        BundleIdentity = bundleIdentity;
        ReplayEpoch = replayEpoch;
        RestoreGeneration = restoreGeneration;
        DomainTag = domainTag;
        AddressSpaceTag = addressSpaceTag;
        DescriptorEpoch = descriptorEpoch;
        Field = field;
        DestinationRegister = destinationRegister;
        Value = value;
    }

    internal string DecisionId { get; }
    internal object SourceOwner { get; }
    internal ulong SourceEpoch { get; }
    internal ExecutionDomainRuntime.SourceCapture? ExecutionDomainCapture { get; }
    internal MemoryDomainRuntime.SourceCapture? MemoryDomainCapture { get; }
    internal ulong ProfileGeneration { get; }
    internal ulong AttemptId { get; }
    internal ulong IssuerGeneration { get; }
    internal ulong BundleIdentity { get; }
    internal ulong ReplayEpoch { get; }
    internal ulong RestoreGeneration { get; }
    internal ulong DomainTag { get; }
    internal ulong AddressSpaceTag { get; }
    internal PrivilegedExecutionStateEpoch DescriptorEpoch { get; }
    internal VmcsField Field { get; }
    internal byte DestinationRegister { get; }
    internal ulong Value { get; }
    internal bool IsConsumed => Volatile.Read(ref _consumed) != 0;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool VmxRetireEffectAuthorized => false;

    internal bool MatchesCarrier(VmxMicroOp carrier, ulong fieldSelector)
    {
        return fieldSelector <= ushort.MaxValue &&
            unchecked((VmcsField)(ushort)fieldSelector) == Field &&
            ReferenceEquals(carrier.VirtualizationAdmission, _attempt.Certificate) &&
            _attempt.Operation == VmxOperationKind.VmRead &&
            _attempt.AttemptId == AttemptId &&
            _attempt.IssuerGeneration == IssuerGeneration &&
            _attempt.BundleIdentity == BundleIdentity &&
            _attempt.ReplayEpoch == ReplayEpoch &&
            _attempt.Certificate.VirtualThreadId == carrier.VirtualThreadId &&
            _attempt.Certificate.OwnerContextId == carrier.OwnerContextId &&
            _attempt.DomainTag == carrier.Placement.DomainTag &&
            DomainTag == carrier.Placement.DomainTag &&
            DestinationRegister == carrier.Rd && carrier.Rs2 == 0 &&
            carrier.TryResolveFrozenOperation(out VmxOperationKind operation) &&
            operation == VmxOperationKind.VmRead;
    }

    internal bool TryValidateSpeculative(ulong? currentRestoreGeneration = null) =>
        _owner.ValidateLive(this, currentRestoreGeneration);

    internal bool TryConsumeAtRetire(ulong currentRestoreGeneration)
    {
        if (!_owner.ValidateLive(this, currentRestoreGeneration))
            return false;
        return Interlocked.CompareExchange(ref _consumed, 1, 0) == 0 &&
            _owner.ValidateConsumedBinding(this, currentRestoreGeneration);
    }
}

internal sealed class VmReadScalarDeliveryCanonicalComposition : IVmReadScalarResultReceiptOwner
{
    private readonly object _sync = new();
    private readonly VmxCompatibilityAdmissionService _admission = new();
    private readonly DomainRuntimeContext _context;
    private readonly RootAuthorityDescriptor _root;
    private readonly EvidencePolicyDescriptor _evidence;
    private readonly bool _conformanceProven;
    private PrivilegedExecutionStateDescriptor _descriptor;
    private PrivilegedExecutionStateEpoch _currentEpoch;
    private ulong _restoreGeneration;
    private ulong _generation = 1;
    private bool _enabled;
    private bool _hasReplayObservation;
    private bool _replayActive;
    private ulong _replayEpoch;
    private ulong _replayCachedPc;
    private ulong _completedReplays;

    internal VmReadScalarDeliveryCanonicalComposition(
        DomainRuntimeContext context,
        RootAuthorityDescriptor root,
        EvidencePolicyDescriptor evidence,
        PrivilegedExecutionStateDescriptor descriptor,
        PrivilegedExecutionStateEpoch currentEpoch,
        ulong restoreGeneration,
        in VmReadScalarDeliveryPolicyLookup lookup,
        bool conformanceProven = true)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _descriptor = descriptor;
        _currentEpoch = currentEpoch;
        _restoreGeneration = restoreGeneration;
        _conformanceProven = conformanceProven;
        PolicyResolution = VmReadScalarDeliveryAcceptedPolicyResolver.Resolve(lookup);
    }

    internal VmReadScalarDeliveryPolicyResolution PolicyResolution { get; }
    internal bool IsEnabled { get { lock (_sync) return _enabled; } }
    internal ulong Generation { get { lock (_sync) return _generation; } }

    internal bool EnableExact()
    {
        lock (_sync)
        {
            if (!PolicyResolution.IsResolved || _enabled)
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

    internal void ReplaceAfterRestore(
        PrivilegedExecutionStateDescriptor descriptor,
        PrivilegedExecutionStateEpoch currentEpoch,
        ulong restoreGeneration)
    {
        lock (_sync)
        {
            _descriptor = descriptor;
            _currentEpoch = currentEpoch;
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
        SafetyVerifier.VirtualizationAdmissionCertificate? e1,
        ulong fieldSelector,
        ulong restoreGeneration)
    {
        lock (_sync)
        {
            if (!_enabled)
                return Deny(VmReadScalarDeliveryDecision.Disabled, "Exact scalar-delivery profile is disabled.");
            if (!PolicyResolution.IsResolved)
                return Deny(VmReadScalarDeliveryDecision.PolicyDenied, PolicyResolution.Reason);
            if (restoreGeneration != _restoreGeneration)
            {
                _enabled = false;
                AdvanceGeneration();
                return Deny(
                    VmReadScalarDeliveryDecision.StaleReceipt,
                    "Restore generation changed before descriptor/epoch revalidation; exact profile was disabled.");
            }
            if (!_hasReplayObservation || !_replayActive || _replayEpoch != replayPhase.EpochId ||
                e1 is null || e1.Operation != VmxOperationKind.VmRead ||
                e1.AttemptId == 0 || e1.BundleIdentity == 0 || e1.ReplayEpoch != replayPhase.EpochId ||
                e1.DomainTag != _context.DomainTag || carrier.VirtualizationAdmission != e1)
                return Deny(VmReadScalarDeliveryDecision.AdmissionDenied, "Live canonical VMREAD E1 identity is required.");
            if (fieldSelector > ushort.MaxValue ||
                unchecked((VmcsField)(ushort)fieldSelector) is not (VmcsField.GuestCr0 or VmcsField.GuestCr4))
                return Deny(VmReadScalarDeliveryDecision.FieldDenied, "Only exact GuestCr0 or GuestCr4 is admitted.");
            if (carrier.Rd is 0 or > 31 || carrier.Rs2 != 0)
                return Deny(VmReadScalarDeliveryDecision.DestinationDenied, "VMREAD scalar delivery requires x1-x31 and reserved x0 Rs2.");

            VmcsField field = unchecked((VmcsField)(ushort)fieldSelector);
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
                    ProjectionEvidenceValidated: true,
                    PrivilegedExecutionState: _descriptor,
                    CurrentPrivilegedExecutionStateEpoch: _currentEpoch,
                    PrivilegedExecutionStateConformanceProven: _conformanceProven));
            if (!projection.IsReadOnlyValueProjected)
                return Deny(VmReadScalarDeliveryDecision.ProjectionDenied, projection.Reason);

            var receipt = new VmReadScalarResultReceipt(
                this,
                new VmReadScalarAttemptBinding(e1),
                VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId,
                _descriptor,
                _currentEpoch.Current,
                executionDomainCapture: null,
                _generation,
                e1.AttemptId,
                e1.IssuerGeneration,
                e1.BundleIdentity,
                e1.ReplayEpoch,
                restoreGeneration,
                _context.DomainTag,
                _context.AddressSpaceTag,
                _currentEpoch,
                field,
                carrier.Rd,
                unchecked((ulong)projection.Value));
            return new(VmReadScalarDeliveryDecision.Prepared, receipt,
                "Exact read-only scalar value was projected from the neutral privileged execution-state owner.");
        }
    }

    public bool ValidateLive(VmReadScalarResultReceipt receipt, ulong? currentRestoreGeneration = null)
    {
        lock (_sync)
        {
            return ValidateBinding(receipt, currentRestoreGeneration) && !receipt.IsConsumed;
        }
    }

    public bool ValidateConsumedBinding(
        VmReadScalarResultReceipt receipt,
        ulong currentRestoreGeneration)
    {
        lock (_sync)
        {
            return ValidateBinding(receipt, currentRestoreGeneration);
        }
    }

    private bool ValidateBinding(
        VmReadScalarResultReceipt receipt,
        ulong? currentRestoreGeneration)
    {
        return _enabled && PolicyResolution.IsResolved &&
                receipt.DecisionId == VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId &&
                receipt.SourceEpoch == _currentEpoch.Current &&
                receipt.ProfileGeneration == _generation && receipt.AttemptId != 0 &&
                receipt.DomainTag == _context.DomainTag &&
                receipt.AddressSpaceTag == _context.AddressSpaceTag &&
                receipt.DescriptorEpoch == _currentEpoch &&
                receipt.RestoreGeneration == _restoreGeneration &&
                (!currentRestoreGeneration.HasValue ||
                    receipt.RestoreGeneration == currentRestoreGeneration.Value);
    }

    private void AdvanceGeneration()
    {
        unchecked { _generation++; }
        if (_generation == 0) _generation = 1;
    }

    private static VmReadScalarDeliveryResult Deny(
        VmReadScalarDeliveryDecision decision,
        string reason) => new(decision, null, reason);
}
