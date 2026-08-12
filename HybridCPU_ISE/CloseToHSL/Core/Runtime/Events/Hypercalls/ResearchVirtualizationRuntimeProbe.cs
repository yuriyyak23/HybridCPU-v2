#if TESTING
using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

internal enum ResearchVirtualizationProbeOperation : byte
{
    None = 0,
    StateMinimalLiveness = 1,
}

internal readonly record struct ResearchVirtualizationProbeIdentity(
    int VirtualThreadId,
    int OwnerContextId,
    ulong DomainTag,
    ulong AddressSpaceTag,
    ulong CarrierAttemptId,
    ulong ReplayEpoch,
    ulong CapabilityGeneration,
    ulong EvidenceGeneration,
    ulong RestoreGeneration)
{
    internal bool IsComplete =>
        VirtualThreadId >= 0 &&
        OwnerContextId >= 0 &&
        DomainTag != 0 &&
        AddressSpaceTag != 0 &&
        CarrierAttemptId != 0 &&
        ReplayEpoch != 0 &&
        CapabilityGeneration != 0 &&
        EvidenceGeneration != 0 &&
        RestoreGeneration != 0;
}

internal enum ResearchVirtualizationProbeAdmissionDecision : byte
{
    IssuedForResearchExecution = 0,
    DeniedUnsupportedOperation = 1,
    DeniedIncompleteIdentity = 2,
    DeniedE1Carrier = 3,
    DeniedCarrierIdentityMismatch = 4,
    DeniedRuntimeIdentitySnapshot = 5,
}

internal readonly record struct ResearchVirtualizationProbeAdmissionResult(
    ResearchVirtualizationProbeAdmissionDecision Decision,
    SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate? Certificate,
    string Reason)
{
    internal bool IsIssued =>
        Decision == ResearchVirtualizationProbeAdmissionDecision.IssuedForResearchExecution &&
        Certificate is not null;
}

internal enum ResearchVirtualizationProbeExecutionDecision : byte
{
    Executed = 0,
    DeniedForeignOwner = 1,
    DeniedStalePolicyGeneration = 2,
    DeniedDuplicateAttempt = 3,
    DeniedStaleRuntimeContext = 4,
    DeniedStaleAdmission = 5,
}

internal readonly record struct ResearchVirtualizationProbeExecutionResult(
    ResearchVirtualizationProbeExecutionDecision Decision,
    ResearchVirtualizationRuntimeOwner.ExecutionReceipt? Receipt,
    string Reason)
{
    internal bool Succeeded =>
        Decision == ResearchVirtualizationProbeExecutionDecision.Executed &&
        Receipt is not null;
}

/// <summary>
/// TESTING-only operation context owner. It materializes one opaque identity snapshot
/// so capability/evidence/restore generations cannot be supplied as loose booleans.
/// </summary>
internal sealed class ResearchVirtualizationOperationContext
{
    private readonly object _contextSeal = new();
    private ulong _contextGeneration = 1;

    internal ResearchVirtualizationOperationContext(
        int virtualThreadId,
        int ownerContextId,
        ulong domainTag,
        ulong addressSpaceTag,
        ulong capabilityGeneration,
        ulong evidenceGeneration,
        ulong restoreGeneration)
    {
        if (virtualThreadId < 0 || ownerContextId < 0 || domainTag == 0 || addressSpaceTag == 0 ||
            capabilityGeneration == 0 || evidenceGeneration == 0 || restoreGeneration == 0)
            throw new ArgumentOutOfRangeException(nameof(virtualThreadId),
                "The research runtime context requires materialized VT/domain/address-space/capability/evidence/restore identities.");

        VirtualThreadId = virtualThreadId;
        OwnerContextId = ownerContextId;
        DomainTag = domainTag;
        AddressSpaceTag = addressSpaceTag;
        CapabilityGeneration = capabilityGeneration;
        EvidenceGeneration = evidenceGeneration;
        RestoreGeneration = restoreGeneration;
    }

    internal sealed class IdentitySnapshot
    {
        internal IdentitySnapshot(
            object contextSeal,
            ulong contextGeneration,
            ResearchVirtualizationProbeIdentity identity)
        {
            ContextSeal = contextSeal;
            ContextGeneration = contextGeneration;
            Identity = identity;
        }

        internal object ContextSeal { get; }
        internal ulong ContextGeneration { get; }
        internal ResearchVirtualizationProbeIdentity Identity { get; }
    }

    /// <summary>
    /// Opaque TESTING-only lease captured before canonical issue. It binds the
    /// runtime-owned identity and authority generations without guessing the E1
    /// attempt/replay identity that will be materialized at the issue boundary.
    /// </summary>
    internal sealed class MaterializationLease
    {
        internal MaterializationLease(
            object contextSeal,
            ulong contextGeneration,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag,
            ulong addressSpaceTag,
            ulong capabilityGeneration,
            ulong evidenceGeneration,
            ulong restoreGeneration)
        {
            ContextSeal = contextSeal;
            ContextGeneration = contextGeneration;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            DomainTag = domainTag;
            AddressSpaceTag = addressSpaceTag;
            CapabilityGeneration = capabilityGeneration;
            EvidenceGeneration = evidenceGeneration;
            RestoreGeneration = restoreGeneration;
        }

        internal object ContextSeal { get; }
        internal ulong ContextGeneration { get; }
        internal int VirtualThreadId { get; }
        internal int OwnerContextId { get; }
        internal ulong DomainTag { get; }
        internal ulong AddressSpaceTag { get; }
        internal ulong CapabilityGeneration { get; }
        internal ulong EvidenceGeneration { get; }
        internal ulong RestoreGeneration { get; }
    }

    private int VirtualThreadId { get; }
    private int OwnerContextId { get; }
    private ulong DomainTag { get; }
    private ulong AddressSpaceTag { get; }
    private ulong CapabilityGeneration { get; set; }
    private ulong EvidenceGeneration { get; set; }
    private ulong RestoreGeneration { get; set; }

    internal MaterializationLease CaptureMaterializationLease()
    {
        lock (_contextSeal)
        {
            return new(
                _contextSeal,
                _contextGeneration,
                VirtualThreadId,
                OwnerContextId,
                DomainTag,
                AddressSpaceTag,
                CapabilityGeneration,
                EvidenceGeneration,
                RestoreGeneration);
        }
    }

    internal IdentitySnapshot? Materialize(
        MaterializationLease lease,
        ulong carrierAttemptId,
        ulong replayEpoch)
    {
        ArgumentNullException.ThrowIfNull(lease);

        lock (_contextSeal)
        {
            if (!ReferenceEquals(lease.ContextSeal, _contextSeal) ||
                lease.ContextGeneration != _contextGeneration ||
                lease.VirtualThreadId != VirtualThreadId ||
                lease.OwnerContextId != OwnerContextId ||
                lease.DomainTag != DomainTag ||
                lease.AddressSpaceTag != AddressSpaceTag ||
                lease.CapabilityGeneration != CapabilityGeneration ||
                lease.EvidenceGeneration != EvidenceGeneration ||
                lease.RestoreGeneration != RestoreGeneration)
            {
                return null;
            }

            return new(
                _contextSeal,
                _contextGeneration,
                new ResearchVirtualizationProbeIdentity(
                    lease.VirtualThreadId,
                    lease.OwnerContextId,
                    lease.DomainTag,
                    lease.AddressSpaceTag,
                    carrierAttemptId,
                    replayEpoch,
                    lease.CapabilityGeneration,
                    lease.EvidenceGeneration,
                    lease.RestoreGeneration));
        }
    }

    internal IdentitySnapshot Capture(ulong carrierAttemptId, ulong replayEpoch)
    {
        lock (_contextSeal)
        {
            return new(
                _contextSeal,
                _contextGeneration,
                new ResearchVirtualizationProbeIdentity(
                    VirtualThreadId,
                    OwnerContextId,
                    DomainTag,
                    AddressSpaceTag,
                    carrierAttemptId,
                    replayEpoch,
                    CapabilityGeneration,
                    EvidenceGeneration,
                    RestoreGeneration));
        }
    }

    internal bool IsLive(IdentitySnapshot snapshot)
    {
        lock (_contextSeal)
        {
            return snapshot is not null &&
                   ReferenceEquals(snapshot.ContextSeal, _contextSeal) &&
                   snapshot.ContextGeneration == _contextGeneration;
        }
    }

    internal bool IsLive(object contextSeal, ulong contextGeneration)
    {
        lock (_contextSeal)
        {
            return ReferenceEquals(contextSeal, _contextSeal) &&
                   contextGeneration == _contextGeneration;
        }
    }

    internal void Invalidate()
    {
        lock (_contextSeal)
        {
            _contextGeneration = checked(_contextGeneration + 1);
        }
    }

    internal void AdvanceCapabilityGeneration()
    {
        lock (_contextSeal)
        {
            CapabilityGeneration = checked(CapabilityGeneration + 1);
            _contextGeneration = checked(_contextGeneration + 1);
        }
    }

    internal void AdvanceEvidenceGeneration()
    {
        lock (_contextSeal)
        {
            EvidenceGeneration = checked(EvidenceGeneration + 1);
            _contextGeneration = checked(_contextGeneration + 1);
        }
    }

    internal void AdvanceRestoreGeneration()
    {
        lock (_contextSeal)
        {
            RestoreGeneration = checked(RestoreGeneration + 1);
            _contextGeneration = checked(_contextGeneration + 1);
        }
    }
}

/// <summary>
/// TESTING-only neutral research owner for a state-minimal virtualization probe.
/// It owns policy and deterministic execution, but SafetyVerifier alone issues admission.
/// </summary>
internal sealed class ResearchVirtualizationRuntimeOwner
{
    private readonly object _ownerSeal = new();
    private readonly object _stateGate = new();
    private readonly HashSet<ulong> _consumedAttempts = new();
    private ulong _policyGeneration = 1;

    internal sealed class PolicySnapshot
    {
        internal PolicySnapshot(
            object ownerSeal,
            ulong policyGeneration,
            ResearchVirtualizationProbeOperation operation)
        {
            OwnerSeal = ownerSeal;
            PolicyGeneration = policyGeneration;
            Operation = operation;
        }

        internal object OwnerSeal { get; }
        internal ulong PolicyGeneration { get; }
        internal ResearchVirtualizationProbeOperation Operation { get; }
    }

    internal sealed class ExecutionReceipt
    {
        internal ExecutionReceipt(
            ulong policyGeneration,
            ulong verifierGeneration,
            ResearchVirtualizationProbeOperation operation,
            ResearchVirtualizationProbeIdentity identity)
        {
            PolicyGeneration = policyGeneration;
            VerifierGeneration = verifierGeneration;
            Operation = operation;
            Identity = identity;
        }

        internal ulong PolicyGeneration { get; }
        internal ulong VerifierGeneration { get; }
        internal ResearchVirtualizationProbeOperation Operation { get; }
        internal ResearchVirtualizationProbeIdentity Identity { get; }
        internal int PayloadLength => 0;
        internal int StateMutationCount => 0;
        internal bool CompletionPublicationAuthorized => false;
        internal bool RetirePublicationAuthorized => false;
    }

    internal PolicySnapshot CapturePolicy()
    {
        lock (_stateGate)
        {
            return new(_ownerSeal, _policyGeneration, ResearchVirtualizationProbeOperation.StateMinimalLiveness);
        }
    }

    internal ResearchVirtualizationProbeExecutionResult Execute(
        SafetyVerifier verifier,
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate,
        ResearchVirtualizationOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(context);

        lock (_stateGate)
        {
            if (!ReferenceEquals(certificate.OwnerSeal, _ownerSeal))
                return Deny(
                    ResearchVirtualizationProbeExecutionDecision.DeniedForeignOwner,
                    "The operation certificate names a different neutral research owner.");

            if (certificate.OwnerPolicyGeneration != _policyGeneration)
                return Deny(
                    ResearchVirtualizationProbeExecutionDecision.DeniedStalePolicyGeneration,
                    "The neutral research-owner policy changed before execution.");

            if (!context.IsLive(certificate.ContextSeal, certificate.ContextGeneration))
                return Deny(
                    ResearchVirtualizationProbeExecutionDecision.DeniedStaleRuntimeContext,
                    "The runtime identity context was foreign or invalidated before execution.");

            if (!verifier.IsResearchVirtualizationOperationAdmissionLive(certificate))
                return Deny(
                    ResearchVirtualizationProbeExecutionDecision.DeniedStaleAdmission,
                    "SafetyVerifier revoked the E1/E2 admission generation before execution.");

            if (!_consumedAttempts.Add(certificate.Identity.CarrierAttemptId))
                return Deny(
                    ResearchVirtualizationProbeExecutionDecision.DeniedDuplicateAttempt,
                    "The admitted carrier attempt was already consumed.");

            return new(
                ResearchVirtualizationProbeExecutionDecision.Executed,
                new ExecutionReceipt(
                    _policyGeneration,
                    certificate.VerifierGeneration,
                    certificate.Operation,
                    certificate.Identity),
                "The neutral research probe executed without payload, state mutation, completion or retire publication.");
        }
    }

    internal void InvalidatePolicy()
    {
        lock (_stateGate)
        {
            _policyGeneration = checked(_policyGeneration + 1);
        }
    }

    private static ResearchVirtualizationProbeExecutionResult Deny(
        ResearchVirtualizationProbeExecutionDecision decision,
        string reason) => new(decision, null, reason);
}

#endif
